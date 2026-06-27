// Meta Search and Control Center (c) 2026 Dennis Michael Heine
// SQL Database Connector - Supports MySQL, MSSQL, PostgreSQL
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MSCC.Localization;
using MSCC.Models;

namespace MSCC.Connectors;

/// <summary>
/// SQL Database Connector for searching across MySQL, MSSQL, and PostgreSQL databases.
/// Supports searching all fields in specified tables or custom SQL queries.
/// </summary>
public class SqlDatabaseConnector : IDataSourceConnector, IDisposable
{
    private static readonly Regex DirectSelectRegex = new(@"\bselect\b[\s\S]+?(?:;|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ExistingLimitRegex = new(@"\b(limit\s+\d+|top\s+\d+|fetch\s+first\s+\d+\s+rows)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex NumberRegex = new(@"\b(\d{1,4})\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private string _connectionString = string.Empty;
    private DatabaseType _databaseType = DatabaseType.MSSQL;
    private string _tables = string.Empty;
    private string _customQuery = string.Empty;
    private bool _useCustomQuery;
    private bool _isInitialized;
    private DbConnection? _connection;
    private static Strings L => Strings.Instance;

    public string Id => "sql-database-connector";
    public string Name => L.Connector_SQL_Name;
    public string Description => L.Connector_SQL_Description;
    public string Version => "1.0.0";

    public IEnumerable<ConnectorParameter> ConfigurationParameters =>
    [
        new ConnectorParameter
        {
            Name = "ConnectionString",
            DisplayName = L.Connector_SQL_ConnectionString,
            Description = L.Connector_SQL_ConnectionString_Desc,
            ParameterType = "string",
            IsRequired = true
        },
        new ConnectorParameter
        {
            Name = "DatabaseType",
            DisplayName = L.Connector_SQL_DatabaseType,
            Description = L.Connector_SQL_DatabaseType_Desc,
            ParameterType = "string",
            IsRequired = true,
            DefaultValue = "MSSQL"
        },
        new ConnectorParameter
        {
            Name = "Tables",
            DisplayName = L.Connector_SQL_Tables,
            Description = L.Connector_SQL_Tables_Desc,
            ParameterType = "string",
            IsRequired = false,
            DefaultValue = "*"
        },
        new ConnectorParameter
        {
            Name = "CustomQuery",
            DisplayName = L.Connector_SQL_CustomQuery,
            Description = L.Connector_SQL_CustomQuery_Desc,
            ParameterType = "string",
            IsRequired = false
        }
    ];

    public Task<bool> InitializeAsync(Dictionary<string, string> configuration)
    {
        try
        {
            if (!configuration.TryGetValue("ConnectionString", out var connectionString) || 
                string.IsNullOrEmpty(connectionString))
            {
                Debug.WriteLine("[SqlDatabaseConnector] ConnectionString is required");
                return Task.FromResult(false);
            }
            _connectionString = connectionString;

            _databaseType = configuration.TryGetValue("DatabaseType", out var dbType)
                ? ParseDatabaseType(dbType, connectionString)
                : InferDatabaseType(connectionString);

            _tables = configuration.TryGetValue("Tables", out var tables) ? tables : "*";

            if (configuration.TryGetValue("CustomQuery", out var customQuery) && 
                !string.IsNullOrWhiteSpace(customQuery))
            {
                _customQuery = customQuery;
                _useCustomQuery = true;
            }

            _isInitialized = true;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlDatabaseConnector] Initialization failed: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public async Task<IEnumerable<SearchResult>> SearchAsync(
        string searchTerm,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SearchResult>();

        if (!_isInitialized || string.IsNullOrWhiteSpace(searchTerm))
            return results;

        Debug.WriteLine($"[SqlDatabaseConnector] Searching for '{searchTerm}' in {_databaseType}");

        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            if (_useCustomQuery)
            {
                results = await ExecuteCustomQueryAsync(connection, searchTerm, maxResults, cancellationToken);
            }
            else
            {
                var tablesToSearch = await GetTablesToSearchAsync(connection, cancellationToken);
                
                foreach (var table in tablesToSearch)
                {
                    if (cancellationToken.IsCancellationRequested || results.Count >= maxResults)
                        break;

                    var tableResults = await SearchTableAsync(
                        connection, table, searchTerm, 
                        maxResults - results.Count, cancellationToken);
                    
                    results.AddRange(tableResults);
                }
            }

            Debug.WriteLine($"[SqlDatabaseConnector] Found {results.Count} results");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlDatabaseConnector] Search error: {ex.Message}");
        }

        return results.Take(maxResults);
    }

    public bool SupportsLiveRag => true;

    public async Task<LiveRagRetrievalResult> RetrieveLiveRagContextAsync(
        LiveRagQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new LiveRagRetrievalResult
        {
            IsNativeLiveRag = true,
            SourceName = Name,
            ConnectorId = Id
        };

        if (!_isInitialized)
        {
            result.Success = false;
            result.ErrorMessage = "SQL connector is not initialized.";
            return result;
        }

        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var directQuery = TryExtractSafeSelectQuery(request);
            if (!string.IsNullOrWhiteSpace(directQuery))
            {
                try
                {
                    await ExecuteLiveRagSqlQueryAsync(
                        connection,
                        directQuery,
                        "CustomSelect",
                        request,
                        result,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SqlDatabaseConnector] Direct Live RAG SQL failed: {ex.Message}");
                }
            }

            if (result.ContextItems.Count == 0)
            {
                var schema = await GetLiveRagSchemaAsync(connection, cancellationToken);
                var plans = BuildLiveRagQueryPlans(request, schema).ToList();

                foreach (var plan in plans)
                {
                    if (result.ContextItems.Count >= Math.Max(1, request.MaxContextItems))
                        break;

                    try
                    {
                        await ExecuteLiveRagSqlQueryAsync(
                            connection,
                            plan.Sql,
                            plan.TableName,
                            request,
                            result,
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SqlDatabaseConnector] Planned Live RAG SQL failed for {plan.TableName}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            Debug.WriteLine($"[SqlDatabaseConnector] Live RAG SQL error: {ex.Message}");
        }

        if (result.ContextItems.Count == 0 && result.Success)
        {
            var fallback = await LiveRagConnectorHelpers.RetrieveFromSearchAsync(
                this,
                request,
                SearchAsync,
                (searchResult, retrievalQuery, liveRequest) => LiveRagConnectorHelpers.CreateContextItem(
                    searchResult,
                    retrievalQuery,
                    liveRequest,
                    LiveRagConnectorHelpers.BuildMetadataContent(
                        searchResult,
                        "Type",
                        "DatabaseType",
                        "MatchingColumns")),
                native: false,
                cancellationToken);

            result.ExecutedQueries.AddRange(fallback.ExecutedQueries);
            result.ContextItems.AddRange(fallback.ContextItems);
            if (!fallback.Success)
            {
                result.Success = false;
                result.ErrorMessage = fallback.ErrorMessage;
            }
        }

        if (result.ContextItems.Count == 0 && string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            result.ErrorMessage = "No SQL rows matched the live RAG request.";
        }

        return result;
    }

    public async Task<LiveRagSourceProfile> DescribeLiveRagCapabilitiesAsync(
        DataSource dataSource,
        CancellationToken cancellationToken = default)
    {
        var profile = new LiveRagSourceProfile
        {
            DataSourceId = dataSource.Id,
            SourceName = dataSource.Name,
            ConnectorId = Id,
            Description = dataSource.Description,
            SupportsNativeLiveRag = true,
            SupportedOperations =
            [
                LiveRagOperationType.StructuredQuery,
                LiveRagOperationType.TopN,
                LiveRagOperationType.Aggregate,
                LiveRagOperationType.KeywordSearch
            ],
            MaxOperations = 3,
            MaxResults = 50,
            Metadata =
            {
                ["databaseType"] = _databaseType.ToString()
            }
        };

        if (!_isInitialized)
            return profile;

        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            var schema = await GetLiveRagSchemaAsync(connection, cancellationToken);

            profile.Targets = schema.Select(table => table.Name).ToList();
            profile.Fields = schema
                .SelectMany(table => table.Columns.Select(column => $"{table.Name}.{column.Name}"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(200)
                .ToList();
            profile.Metadata["schema"] = schema.Select(table => new
            {
                table = table.Name,
                columns = table.Columns.Select(column => new { column.Name, column.DataType })
            }).ToList();
        }
        catch (Exception ex)
        {
            profile.Metadata["schemaError"] = ex.Message;
        }

        return profile;
    }

    public async Task<LiveRagRetrievalResult> RetrieveLiveRagContextByOperationsAsync(
        LiveRagQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Operations.Count == 0)
            return await RetrieveLiveRagContextAsync(request, cancellationToken);

        var result = new LiveRagRetrievalResult
        {
            IsNativeLiveRag = true,
            SourceName = Name,
            ConnectorId = Id
        };

        if (!_isInitialized)
        {
            result.Success = false;
            result.ErrorMessage = "SQL connector is not initialized.";
            return result;
        }

        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            var schema = await GetLiveRagSchemaAsync(connection, cancellationToken);

            foreach (var operation in request.Operations.Take(Math.Max(1, request.MaxOperationsPerSource)))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var trace = new LiveRagExecutionTrace
                {
                    OperationId = operation.Id,
                    DataSourceId = operation.DataSourceId,
                    SourceName = operation.SourceName,
                    ConnectorId = Id,
                    OperationType = operation.Type,
                    Accepted = true,
                    StartedAt = DateTime.Now
                };

                var beforeCount = result.ContextItems.Count;
                try
                {
                    if (operation.Type == LiveRagOperationType.KeywordSearch)
                    {
                        var keywordResult = await ExecuteKeywordLiveRagOperationAsync(
                            operation,
                            request,
                            cancellationToken);

                        result.ExecutedQueries.AddRange(keywordResult.ExecutedQueries);
                        result.ContextItems.AddRange(keywordResult.ContextItems);
                        result.ExecutedOperations.Add(operation);
                        trace.ResultCount = keywordResult.ContextItems.Count;
                        trace.Reason = "Executed bounded SQL keyword live operation.";
                        continue;
                    }

                    var sql = BuildSqlForLiveRagOperation(operation, request, schema);
                    if (string.IsNullOrWhiteSpace(sql))
                    {
                        trace.Accepted = false;
                        trace.Reason = "Could not map operation to a safe SQL SELECT.";
                        result.RejectedOperations.Add(operation);
                        result.Diagnostics.Add(trace);
                        continue;
                    }

                    await ExecuteLiveRagSqlQueryAsync(
                        connection,
                        sql,
                        operation.Target ?? "LiveRagQuery",
                        request,
                        result,
                        cancellationToken);

                    foreach (var item in result.ContextItems.Skip(beforeCount))
                    {
                        item.OperationId = operation.Id;
                        item.OperationType = operation.Type;
                    }

                    result.ExecutedOperations.Add(operation);
                    trace.ResultCount = result.ContextItems.Count - beforeCount;
                    trace.Reason = "Executed safe read-only SQL live operation.";
                }
                catch (Exception ex)
                {
                    trace.Accepted = false;
                    trace.ErrorMessage = ex.Message;
                    trace.Reason = "SQL live operation failed.";
                    result.RejectedOperations.Add(operation);
                }
                finally
                {
                    trace.CompletedAt = DateTime.Now;
                    result.Diagnostics.Add(trace);
                }

                if (result.ContextItems.Count >= Math.Max(1, request.MaxContextItems))
                    break;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        if (result.ContextItems.Count == 0 && result.RejectedOperations.Count > 0)
        {
            result.Success = false;
            result.ErrorMessage ??= "No SQL live RAG operation produced context.";
        }

        return result;
    }

    private async Task<LiveRagRetrievalResult> ExecuteKeywordLiveRagOperationAsync(
        LiveRagOperation operation,
        LiveRagQueryRequest request,
        CancellationToken cancellationToken)
    {
        var operationRequest = new LiveRagQueryRequest
        {
            Question = string.IsNullOrWhiteSpace(operation.Query)
                ? request.Question
                : operation.Query,
            SearchTerms = operation.SearchTerms.Count > 0
                ? operation.SearchTerms
                : LiveRagConnectorHelpers.CreateSearchTerms(operation.Query, request.SearchTerms, request.MaxSearchTerms),
            Mode = operation.IsDegradedFallback
                ? LiveRagMode.DegradedKeywordFallback
                : request.Mode,
            MaxSearchTerms = request.MaxSearchTerms,
            MaxResultsPerSearchTerm = Math.Clamp(
                operation.Limit <= 0 ? request.MaxResultsPerSearchTerm : operation.Limit,
                1,
                Math.Max(1, request.MaxCandidateItems)),
            MaxContextItems = request.MaxContextItems,
            MaxCharactersPerItem = request.MaxCharactersPerItem,
            IncludeMetadata = request.IncludeMetadata,
            MaxOperationsPerSource = request.MaxOperationsPerSource,
            MaxCandidateItems = request.MaxCandidateItems,
            Options = new Dictionary<string, string>(request.Options, StringComparer.OrdinalIgnoreCase)
        };

        var keywordResult = await LiveRagConnectorHelpers.RetrieveFromSearchAsync(
            this,
            operationRequest,
            SearchAsync,
            (searchResult, retrievalQuery, liveRequest) => LiveRagConnectorHelpers.CreateContextItem(
                searchResult,
                retrievalQuery,
                liveRequest,
                LiveRagConnectorHelpers.BuildMetadataContent(
                    searchResult,
                    "Type",
                    "DatabaseType",
                    "MatchingColumns")),
            native: true,
            cancellationToken);

        foreach (var item in keywordResult.ContextItems)
        {
            item.OperationId = operation.Id;
            item.OperationType = operation.Type;
        }

        return keywordResult;
    }

    private static DatabaseType ParseDatabaseType(string? configuredType, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(configuredType))
            return InferDatabaseType(connectionString);

        var normalizedType = configuredType.Trim().ToUpperInvariant();
        if (normalizedType is "MYSQL" or "MARIADB")
            return DatabaseType.MySQL;
        if (normalizedType is "POSTGRESQL" or "POSTGRES")
            return DatabaseType.PostgreSQL;

        if (normalizedType is "MSSQL" or "SQLSERVER" or "SQL SERVER")
        {
            var inferred = InferDatabaseType(connectionString);
            if (inferred != DatabaseType.MSSQL && LooksLikeDefaultSqlServerSelection(configuredType))
                return inferred;
        }

        return DatabaseType.MSSQL;
    }

    private static bool LooksLikeDefaultSqlServerSelection(string configuredType)
    {
        return string.Equals(configuredType.Trim(), "MSSQL", StringComparison.OrdinalIgnoreCase);
    }

    private static DatabaseType InferDatabaseType(string connectionString)
    {
        var normalized = connectionString.Replace(" ", string.Empty).ToLowerInvariant();

        if (normalized.Contains("userid=", StringComparison.Ordinal) ||
            normalized.Contains("uid=", StringComparison.Ordinal) ||
            normalized.Contains("allowloadlocalinfile=", StringComparison.Ordinal) ||
            normalized.Contains("treattinyasboolean=", StringComparison.Ordinal) ||
            normalized.Contains("port=3306", StringComparison.Ordinal))
        {
            return DatabaseType.MySQL;
        }

        if (normalized.Contains("host=", StringComparison.Ordinal) ||
            normalized.Contains("username=", StringComparison.Ordinal) ||
            normalized.Contains("searchpath=", StringComparison.Ordinal) ||
            normalized.Contains("port=5432", StringComparison.Ordinal))
        {
            return DatabaseType.PostgreSQL;
        }

        return DatabaseType.MSSQL;
    }

    private DbConnection CreateConnection()
    {
        return _databaseType switch
        {
            DatabaseType.MySQL => CreateMySqlConnection(),
            DatabaseType.PostgreSQL => CreateNpgsqlConnection(),
            _ => CreateSqlServerConnection()
        };
    }

    private DbConnection CreateSqlServerConnection()
    {
        var assemblyName = "Microsoft.Data.SqlClient";
        var typeName = "Microsoft.Data.SqlClient.SqlConnection";
        
        try
        {
            var assembly = System.Reflection.Assembly.Load(assemblyName);
            var type = assembly.GetType(typeName);
            if (type != null)
            {
                var connection = Activator.CreateInstance(type, _connectionString) as DbConnection;
                return connection ?? throw new InvalidOperationException("Failed to create SQL Server connection");
            }
        }
        catch
        {
            var fallbackType = Type.GetType("System.Data.SqlClient.SqlConnection, System.Data.SqlClient");
            if (fallbackType != null)
            {
                var connection = Activator.CreateInstance(fallbackType, _connectionString) as DbConnection;
                return connection ?? throw new InvalidOperationException("Failed to create SQL Server connection");
            }
        }
        
        throw new InvalidOperationException("SQL Server provider not found. Please install Microsoft.Data.SqlClient NuGet package.");
    }

    private DbConnection CreateMySqlConnection()
    {
        // Try MySqlConnector first (recommended)
        try
        {
            var assembly = System.Reflection.Assembly.Load("MySqlConnector");
            var type = assembly.GetType("MySqlConnector.MySqlConnection");
            if (type != null)
            {
                var connection = Activator.CreateInstance(type, _connectionString) as DbConnection;
                return connection ?? throw new InvalidOperationException("Failed to create MySQL connection");
            }
        }
        catch { }

        // Try MySql.Data as fallback
        try
        {
            var assembly = System.Reflection.Assembly.Load("MySql.Data");
            var type = assembly.GetType("MySql.Data.MySqlClient.MySqlConnection");
            if (type != null)
            {
                var connection = Activator.CreateInstance(type, _connectionString) as DbConnection;
                return connection ?? throw new InvalidOperationException("Failed to create MySQL connection");
            }
        }
        catch { }
        
        throw new InvalidOperationException("MySQL provider not found. Please install MySqlConnector NuGet package.");
    }

    private DbConnection CreateNpgsqlConnection()
    {
        try
        {
            var assembly = System.Reflection.Assembly.Load("Npgsql");
            var type = assembly.GetType("Npgsql.NpgsqlConnection");
            if (type != null)
            {
                var connection = Activator.CreateInstance(type, _connectionString) as DbConnection;
                return connection ?? throw new InvalidOperationException("Failed to create PostgreSQL connection");
            }
        }
        catch { }
        
        throw new InvalidOperationException("PostgreSQL provider not found. Please install Npgsql NuGet package.");
    }

    private async Task<List<string>> GetTablesToSearchAsync(DbConnection connection, CancellationToken ct)
    {
        var tables = new List<string>();

        if (_tables != "*" && !string.IsNullOrWhiteSpace(_tables))
        {
            tables.AddRange(_tables.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()));
            return tables;
        }

        var query = _databaseType switch
        {
            DatabaseType.MySQL => "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'",
            DatabaseType.PostgreSQL => "SELECT tablename FROM pg_catalog.pg_tables WHERE schemaname = 'public'",
            _ => "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'"
        };

        using var command = connection.CreateCommand();
        command.CommandText = query;

        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private async Task<List<SearchResult>> SearchTableAsync(
        DbConnection connection, string tableName, string searchTerm, int maxResults, CancellationToken ct)
    {
        var results = new List<SearchResult>();

        try
        {
            var columns = await GetTableColumnsAsync(connection, tableName, ct);
            if (columns.Count == 0) return results;

            var whereClause = BuildSearchWhereClause(columns);
            var quotedTableName = QuoteIdentifier(tableName);
            
            var query = _databaseType switch
            {
                DatabaseType.MSSQL => $"SELECT TOP {maxResults} * FROM {quotedTableName} WHERE {whereClause}",
                _ => $"SELECT * FROM {quotedTableName} WHERE {whereClause} LIMIT {maxResults}"
            };

            using var command = connection.CreateCommand();
            command.CommandText = query;
            
            var param = command.CreateParameter();
            param.ParameterName = "@SearchTerm";
            param.Value = $"%{searchTerm}%";
            command.Parameters.Add(param);

            using var reader = await command.ExecuteReaderAsync(ct);
            
            while (await reader.ReadAsync(ct))
            {
                if (results.Count >= maxResults) break;
                results.Add(CreateSearchResultFromRow(reader, tableName, searchTerm, columns));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlDatabaseConnector] Error searching table {tableName}: {ex.Message}");
        }

        return results;
    }

    private async Task<List<ColumnInfo>> GetTableColumnsAsync(DbConnection connection, string tableName, CancellationToken ct)
    {
        var columns = new List<ColumnInfo>();

        var query = _databaseType switch
        {
            DatabaseType.MySQL => "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @TableName",
            DatabaseType.PostgreSQL => "SELECT column_name, data_type FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @TableName",
            _ => "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TableName"
        };

        using var command = connection.CreateCommand();
        command.CommandText = query;
        
        var param = command.CreateParameter();
        param.ParameterName = "@TableName";
        param.Value = tableName;
        command.Parameters.Add(param);

        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            columns.Add(new ColumnInfo { Name = reader.GetString(0), DataType = reader.GetString(1) });
        }

        return columns;
    }

    private string BuildSearchWhereClause(List<ColumnInfo> columns)
    {
        var conditions = new List<string>();
        
        foreach (var column in columns)
        {
            var quotedColumn = QuoteIdentifier(column.Name);
            var castExpression = BuildCastExpression(quotedColumn);
            conditions.Add($"{castExpression} LIKE @SearchTerm");
        }

        if (conditions.Count == 0)
        {
            return "1=0";
        }

        return string.Join(" OR ", conditions);
    }

    private string BuildCastExpression(string quotedColumn) => _databaseType switch
    {
        DatabaseType.PostgreSQL => $"CAST({quotedColumn} AS TEXT)",
        DatabaseType.MySQL => $"CAST({quotedColumn} AS CHAR)",
        _ => $"CAST({quotedColumn} AS NVARCHAR(MAX))"
    };

    private string QuoteIdentifier(string identifier) => _databaseType switch
    {
        DatabaseType.MySQL => $"`{identifier}`",
        DatabaseType.PostgreSQL => $"\"{identifier}\"",
        _ => $"[{identifier}]"
    };

    private SearchResult CreateSearchResultFromRow(DbDataReader reader, string tableName, string searchTerm, List<ColumnInfo> columns)
    {
        var metadata = new Dictionary<string, object>
        {
            ["Type"] = "SqlRecord", ["TableName"] = tableName, ["DatabaseType"] = _databaseType.ToString()
        };

        var matchingColumns = new List<string>();
        var title = "";
        var description = new StringBuilder();

        for (int i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
            metadata[columnName] = value ?? DBNull.Value;

            if (value != null)
            {
                var stringValue = value.ToString() ?? "";
                if (stringValue.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    matchingColumns.Add(columnName);

                if (string.IsNullOrEmpty(title) && !string.IsNullOrWhiteSpace(stringValue))
                    title = stringValue.Length > 100 ? stringValue[..100] + "..." : stringValue;

                if (description.Length < 200)
                {
                    if (description.Length > 0) description.Append(" | ");
                    description.Append($"{columnName}: {(stringValue.Length > 50 ? stringValue[..50] + "..." : stringValue)}");
                }
            }
        }

        metadata["MatchingColumns"] = string.Join(", ", matchingColumns);

        return new SearchResult
        {
            Title = string.IsNullOrEmpty(title) ? $"[{tableName}] {L.Connector_SQL_Record}" : title,
            Description = description.ToString(),
            SourceName = $"SQL - {tableName}",
            ConnectorId = Id,
            OriginalReference = $"{tableName}:{Guid.NewGuid():N}",
            RelevanceScore = Math.Min(100, 50 + (matchingColumns.Count * 10)),
            Metadata = metadata
        };
    }

    private async Task<List<TableInfo>> GetLiveRagSchemaAsync(DbConnection connection, CancellationToken ct)
    {
        var tables = await GetTablesToSearchAsync(connection, ct);
        var schema = new List<TableInfo>();
        const int maxSchemaTables = 50;

        foreach (var table in tables.Take(maxSchemaTables))
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                var columns = await GetTableColumnsAsync(connection, table, ct);
                if (columns.Count > 0)
                {
                    schema.Add(new TableInfo
                    {
                        Name = table,
                        Columns = columns
                    });
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SqlDatabaseConnector] Live RAG schema read failed for {table}: {ex.Message}");
            }
        }

        return schema;
    }

    private IEnumerable<SqlLiveRagPlan> BuildLiveRagQueryPlans(LiveRagQueryRequest request, List<TableInfo> schema)
    {
        if (schema.Count == 0)
            yield break;

        var foldedText = FoldText(BuildRetrievalText(request));
        var limit = DetectRequestedLimit(foldedText, request);
        var descending = !ContainsAny(foldedText, "kleinste", "kleinsten", "niedrigste", "niedrigsten", "lowest", "least", "smallest", "asc");

        var rankedTables = schema
            .Select(table => new
            {
                Table = table,
                Score = ScoreTableForQuestion(table, foldedText, schema.Count)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Table.Name)
            .Take(Math.Max(1, request.MaxSearchTerms));

        foreach (var rankedTable in rankedTables)
        {
            var table = rankedTable.Table;
            var orderColumn = ResolveOrderColumn(table, foldedText);
            var selectedColumns = ResolveSelectedColumns(table, foldedText, orderColumn);
            var sql = BuildSelectSql(table.Name, selectedColumns, orderColumn, descending, limit);

            yield return new SqlLiveRagPlan(table.Name, sql);
        }
    }

    private string BuildSqlForLiveRagOperation(
        LiveRagOperation operation,
        LiveRagQueryRequest request,
        List<TableInfo> schema)
    {
        if (operation.Type is LiveRagOperationType.StructuredQuery or LiveRagOperationType.Aggregate &&
            IsSafeReadOnlySelect(operation.Query))
        {
            return EnsureSelectLimit(operation.Query.Trim().TrimEnd(';'), operation.Limit);
        }

        if (operation.Type == LiveRagOperationType.Aggregate)
        {
            var aggregateSql = BuildAggregateSqlForLiveRagOperation(operation, request, schema);
            if (!string.IsNullOrWhiteSpace(aggregateSql))
                return aggregateSql;
        }

        var foldedText = FoldText($"{operation.Query} {operation.Target} {string.Join(" ", operation.SearchTerms)}");
        var table = ResolveOperationTable(operation, schema, foldedText);
        if (table == null)
        {
            var planned = BuildLiveRagQueryPlans(new LiveRagQueryRequest
            {
                Question = string.IsNullOrWhiteSpace(operation.Query) ? request.Question : operation.Query,
                SearchTerms = operation.SearchTerms,
                MaxSearchTerms = request.MaxSearchTerms,
                MaxResultsPerSearchTerm = operation.Limit <= 0 ? request.MaxResultsPerSearchTerm : operation.Limit,
                MaxContextItems = request.MaxContextItems,
                MaxCharactersPerItem = request.MaxCharactersPerItem,
                IncludeMetadata = request.IncludeMetadata
            }, schema).FirstOrDefault();

            return planned?.Sql ?? string.Empty;
        }

        operation.Target = table.Name;

        var orderColumn = ResolveColumn(table, operation.SortField)
            ?? ResolveOrderColumn(table, foldedText);
        var selectedColumns = ResolveSelectedColumnsForOperation(table, operation, foldedText, orderColumn);

        return BuildSelectSql(
            table.Name,
            selectedColumns,
            orderColumn,
            operation.SortDescending,
            operation.Limit <= 0 ? request.MaxResultsPerSearchTerm : operation.Limit);
    }

    private string BuildAggregateSqlForLiveRagOperation(
        LiveRagOperation operation,
        LiveRagQueryRequest request,
        List<TableInfo> schema)
    {
        var foldedText = FoldText($"{operation.Query} {operation.Target} {string.Join(" ", operation.SearchTerms)}");
        var table = ResolveOperationTable(operation, schema, foldedText);
        if (table == null)
            return string.Empty;

        operation.Target = table.Name;
        var aggregateFunction = ResolveAggregateFunction(foldedText);

        if (aggregateFunction == "COUNT")
        {
            return $"SELECT COUNT(*) AS {QuoteIdentifier("count")} FROM {QuoteIdentifier(table.Name)}";
        }

        var aggregateColumn = ResolveColumn(table, operation.SortField)
            ?? operation.SelectFields
                .Select(field => ResolveColumn(table, field))
                .FirstOrDefault(column => column != null && IsNumericColumn(column))
            ?? ResolveOrderColumn(table, foldedText)
            ?? table.Columns.FirstOrDefault(IsNumericColumn);

        if (aggregateColumn == null)
        {
            var planned = BuildLiveRagQueryPlans(new LiveRagQueryRequest
            {
                Question = operation.Query,
                SearchTerms = operation.SearchTerms,
                MaxSearchTerms = request.MaxSearchTerms,
                MaxResultsPerSearchTerm = operation.Limit <= 0 ? request.MaxResultsPerSearchTerm : operation.Limit,
                MaxContextItems = request.MaxContextItems,
                MaxCharactersPerItem = request.MaxCharactersPerItem,
                IncludeMetadata = request.IncludeMetadata
            }, schema).FirstOrDefault();

            return planned?.Sql ?? string.Empty;
        }

        var alias = $"{aggregateFunction.ToLowerInvariant()}_{aggregateColumn.Name}";
        return $"SELECT {aggregateFunction}({QuoteIdentifier(aggregateColumn.Name)}) AS {QuoteIdentifier(alias)} FROM {QuoteIdentifier(table.Name)}";
    }

    private static string ResolveAggregateFunction(string foldedText)
    {
        if (ContainsAny(foldedText, "durchschnitt", "average", "avg", "mittelwert"))
            return "AVG";
        if (ContainsAny(foldedText, "sum", "summe", "total", "gesamt"))
            return "SUM";
        if (ContainsAny(foldedText, "minimum", "min", "kleinste", "niedrigste", "lowest", "least"))
            return "MIN";
        if (ContainsAny(foldedText, "maximum", "max", "groesste", "hoechste", "largest", "highest"))
            return "MAX";
        return "COUNT";
    }

    private static TableInfo? ResolveOperationTable(
        LiveRagOperation operation,
        List<TableInfo> schema,
        string foldedText)
    {
        if (!string.IsNullOrWhiteSpace(operation.Target))
        {
            var direct = schema.FirstOrDefault(table =>
                string.Equals(table.Name, operation.Target, StringComparison.OrdinalIgnoreCase));
            if (direct != null)
                return direct;
        }

        return schema
            .Select(table => new
            {
                Table = table,
                Score = IdentifierAppears(foldedText, table.Name) || TableConceptAppears(table.Name, foldedText)
                    ? 100
                    : table.Columns.Count(column => IdentifierAppears(foldedText, column.Name) || ColumnConceptAppears(column, foldedText))
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Table.Name)
            .Select(item => item.Table)
            .FirstOrDefault();
    }

    private static ColumnInfo? ResolveColumn(TableInfo table, string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            return null;

        columnName = columnName.Trim().Trim('`', '"', '[', ']');
        var lastDot = columnName.LastIndexOf('.');
        if (lastDot >= 0 && lastDot + 1 < columnName.Length)
            columnName = columnName[(lastDot + 1)..].Trim().Trim('`', '"', '[', ']');

        return table.Columns.FirstOrDefault(column =>
            string.Equals(column.Name, columnName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FoldText(column.Name), FoldText(columnName), StringComparison.OrdinalIgnoreCase));
    }

    private static List<ColumnInfo> ResolveSelectedColumnsForOperation(
        TableInfo table,
        LiveRagOperation operation,
        string foldedText,
        ColumnInfo? orderColumn)
    {
        var selected = new List<ColumnInfo>();

        foreach (var field in operation.SelectFields)
        {
            var column = ResolveColumn(table, field);
            if (column != null)
                AddColumn(selected, column);
        }

        if (selected.Count == 0)
            selected = ResolveSelectedColumns(table, foldedText, orderColumn);

        if (orderColumn != null)
            AddColumn(selected, orderColumn);

        return selected.Take(10).ToList();
    }

    private string TryExtractSafeSelectQuery(LiveRagQueryRequest request)
    {
        foreach (var text in new[] { request.Question }.Concat(request.SearchTerms))
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var match = DirectSelectRegex.Match(text);
            if (!match.Success)
                continue;

            var sql = match.Value.Trim().TrimEnd(';').Trim();
            if (!IsSafeReadOnlySelect(sql))
                continue;

            return EnsureSelectLimit(sql, DetectRequestedLimit(FoldText(text), request));
        }

        return string.Empty;
    }

    private string EnsureSelectLimit(string sql, int limit)
    {
        if (ExistingLimitRegex.IsMatch(sql))
            return sql;

        return _databaseType switch
        {
            DatabaseType.MSSQL => Regex.Replace(sql, @"^\s*select\s+", $"SELECT TOP {limit} ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            _ => $"{sql} LIMIT {limit}"
        };
    }

    private static bool IsSafeReadOnlySelect(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return false;

        var foldedSql = FoldText(sql);
        if (!foldedSql.TrimStart().StartsWith("select ", StringComparison.Ordinal))
            return false;

        if (foldedSql.Contains(';') ||
            foldedSql.Contains("--", StringComparison.Ordinal) ||
            foldedSql.Contains("/*", StringComparison.Ordinal) ||
            foldedSql.Contains("*/", StringComparison.Ordinal))
        {
            return false;
        }

        var forbidden = new[]
        {
            " insert ", " update ", " delete ", " drop ", " alter ", " truncate ",
            " create ", " replace ", " grant ", " revoke ", " call ", " exec ",
            " execute ", " merge ", " into outfile ", " load_file "
        };

        return !forbidden.Any(keyword => foldedSql.Contains(keyword, StringComparison.Ordinal));
    }

    private async Task ExecuteLiveRagSqlQueryAsync(
        DbConnection connection,
        string sql,
        string tableName,
        LiveRagQueryRequest request,
        LiveRagRetrievalResult result,
        CancellationToken ct)
    {
        result.ExecutedQueries.Add(sql);

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = await command.ExecuteReaderAsync(ct);
        var rowNumber = 0;

        while (await reader.ReadAsync(ct))
        {
            rowNumber++;
            if (result.ContextItems.Count >= Math.Max(1, request.MaxContextItems))
                break;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var contentBuilder = new StringBuilder();

            for (var i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var value = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.ToString() ?? "";
                values[columnName] = value;

                if (contentBuilder.Length > 0)
                    contentBuilder.Append(" | ");
                contentBuilder.Append(columnName).Append(": ").Append(value);
            }

            var content = LiveRagConnectorHelpers.NormalizeWhitespace(contentBuilder.ToString());
            if (request.MaxCharactersPerItem > 0 && content.Length > request.MaxCharactersPerItem)
                content = content[..request.MaxCharactersPerItem] + "...";

            var metadata = request.IncludeMetadata
                ? values.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            metadata["Type"] = "SqlLiveRagRecord";
            metadata["TableName"] = tableName;
            metadata["DatabaseType"] = _databaseType.ToString();

            result.ContextItems.Add(new LiveRagContextItem
            {
                Title = BuildRowTitle(values, tableName, rowNumber),
                Content = content,
                SourceName = $"SQL - {tableName}",
                ConnectorId = Id,
                OriginalReference = $"{tableName}:{rowNumber}:{Guid.NewGuid():N}",
                RelevanceScore = Math.Max(70, 100 - rowNumber),
                FromNativeLiveRag = true,
                RetrievalQuery = sql,
                Metadata = metadata
            });
        }
    }

    private string BuildSelectSql(
        string tableName,
        List<ColumnInfo> selectedColumns,
        ColumnInfo? orderColumn,
        bool descending,
        int limit)
    {
        var quotedTable = QuoteIdentifier(tableName);
        var selectList = selectedColumns.Count == 0
            ? "*"
            : string.Join(", ", selectedColumns.Select(column => QuoteIdentifier(column.Name)));
        var orderClause = orderColumn == null
            ? string.Empty
            : $" ORDER BY {QuoteIdentifier(orderColumn.Name)} {(descending ? "DESC" : "ASC")}";

        return _databaseType switch
        {
            DatabaseType.MSSQL => $"SELECT TOP {limit} {selectList} FROM {quotedTable}{orderClause}",
            _ => $"SELECT {selectList} FROM {quotedTable}{orderClause} LIMIT {limit}"
        };
    }

    private static int ScoreTableForQuestion(TableInfo table, string foldedText, int schemaTableCount)
    {
        var score = schemaTableCount == 1 ? 5 : 0;

        if (IdentifierAppears(foldedText, table.Name))
            score += 100;
        else if (TableConceptAppears(table.Name, foldedText))
            score += 90;

        foreach (var column in table.Columns)
        {
            if (IdentifierAppears(foldedText, column.Name))
                score += 20;
            else if (ColumnConceptAppears(column, foldedText))
                score += 12;
        }

        return score;
    }

    private static ColumnInfo? ResolveOrderColumn(TableInfo table, string foldedText)
    {
        var mentionedNumericColumn = table.Columns
            .Where(column => IsNumericColumn(column) && (IdentifierAppears(foldedText, column.Name) || ColumnConceptAppears(column, foldedText)))
            .OrderByDescending(column => IdentifierAppears(foldedText, column.Name) ? 2 : 1)
            .FirstOrDefault();

        if (mentionedNumericColumn != null)
            return mentionedNumericColumn;

        if (ContainsAny(foldedText, "top", "meiste", "meisten", "groesste", "groessten", "hoechste", "hoechsten", "largest", "highest", "most"))
        {
            return table.Columns.FirstOrDefault(column => IsNumericColumn(column));
        }

        return null;
    }

    private static List<ColumnInfo> ResolveSelectedColumns(TableInfo table, string foldedText, ColumnInfo? orderColumn)
    {
        var selected = new List<ColumnInfo>();

        foreach (var column in table.Columns.Where(IsNameLikeColumn))
            AddColumn(selected, column);

        foreach (var column in table.Columns.Where(column => IdentifierAppears(foldedText, column.Name) || ColumnConceptAppears(column, foldedText)))
            AddColumn(selected, column);

        if (orderColumn != null)
            AddColumn(selected, orderColumn);

        if (selected.Count == 0)
        {
            foreach (var column in table.Columns.Take(8))
                AddColumn(selected, column);
        }

        return selected.Take(10).ToList();
    }

    private static void AddColumn(List<ColumnInfo> columns, ColumnInfo column)
    {
        if (!columns.Any(existing => string.Equals(existing.Name, column.Name, StringComparison.OrdinalIgnoreCase)))
            columns.Add(column);
    }

    private static bool IsNameLikeColumn(ColumnInfo column)
    {
        var name = FoldText(column.Name);
        return name is "name" or "title" or "city" or "city_name" or "cityname" or "stadt" or "stadtname" ||
               name.EndsWith("_name", StringComparison.Ordinal) ||
               name.EndsWith("name", StringComparison.Ordinal);
    }

    private static bool IsNumericColumn(ColumnInfo column)
    {
        var type = FoldText(column.DataType);
        return type.Contains("int", StringComparison.Ordinal) ||
               type.Contains("decimal", StringComparison.Ordinal) ||
               type.Contains("numeric", StringComparison.Ordinal) ||
               type.Contains("number", StringComparison.Ordinal) ||
               type.Contains("double", StringComparison.Ordinal) ||
               type.Contains("float", StringComparison.Ordinal) ||
               type.Contains("real", StringComparison.Ordinal) ||
               type.Contains("money", StringComparison.Ordinal);
    }

    private static bool ColumnConceptAppears(ColumnInfo column, string foldedText)
    {
        var columnName = FoldText(column.Name);

        if ((columnName.Contains("population", StringComparison.Ordinal) ||
             columnName.Contains("inhabitant", StringComparison.Ordinal) ||
             columnName.Contains("einwohner", StringComparison.Ordinal) ||
             columnName is "pop" or "pop_total") &&
            ContainsAny(foldedText, "population", "inhabitants", "einwohner", "einwohnerzahl", "bevoelkerung"))
        {
            return true;
        }

        if (IsNameLikeColumn(column) && ContainsAny(foldedText, "name", "namen", "stadtname", "city name", "city_name"))
            return true;

        return false;
    }

    private static bool TableConceptAppears(string tableName, string foldedText)
    {
        var foldedName = FoldText(tableName);
        if ((foldedName.Contains("city", StringComparison.Ordinal) ||
             foldedName.Contains("cities", StringComparison.Ordinal) ||
             foldedName.Contains("stadt", StringComparison.Ordinal)) &&
            ContainsAny(foldedText, "city", "cities", "stadt", "staedte"))
        {
            return true;
        }

        return false;
    }

    private static int DetectRequestedLimit(string foldedText, LiveRagQueryRequest request)
    {
        var match = Regex.Match(foldedText, @"\btop\s+(\d{1,3})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
            match = Regex.Match(foldedText, @"\blimit\s+(\d{1,3})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
            match = Regex.Match(foldedText, @"\b(\d{1,3})\s+(?:staedte|cities|rows|zeilen|records|eintraege)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
            match = NumberRegex.Match(foldedText);

        if (match.Success && int.TryParse(match.Groups[1].Value, out var requested))
            return Math.Clamp(requested, 1, Math.Max(1, request.MaxResultsPerSearchTerm));

        return Math.Clamp(Math.Min(request.MaxContextItems, request.MaxResultsPerSearchTerm), 1, 50);
    }

    private static string BuildRetrievalText(LiveRagQueryRequest request)
    {
        return string.Join(" ", new[] { request.Question }.Concat(request.SearchTerms));
    }

    private static bool IdentifierAppears(string foldedText, string identifier)
    {
        var foldedIdentifier = FoldText(identifier);
        if (string.IsNullOrWhiteSpace(foldedIdentifier))
            return false;

        var escaped = Regex.Escape(foldedIdentifier);
        if (Regex.IsMatch(foldedText, $@"(?<![a-z0-9_]){escaped}(?![a-z0-9_])", RegexOptions.CultureInvariant))
            return true;

        var spaced = foldedIdentifier.Replace("_", " ");
        if (!string.Equals(spaced, foldedIdentifier, StringComparison.Ordinal) &&
            foldedText.Contains(spaced, StringComparison.Ordinal))
        {
            return true;
        }

        var singular = ToSimpleSingular(foldedIdentifier);
        return !string.Equals(singular, foldedIdentifier, StringComparison.Ordinal) &&
               Regex.IsMatch(foldedText, $@"(?<![a-z0-9_]){Regex.Escape(singular)}(?![a-z0-9_])", RegexOptions.CultureInvariant);
    }

    private static string ToSimpleSingular(string value)
    {
        if (value.EndsWith("ies", StringComparison.Ordinal) && value.Length > 3)
            return value[..^3] + "y";
        if (value.EndsWith("s", StringComparison.Ordinal) && value.Length > 2)
            return value[..^1];
        return value;
    }

    private static bool ContainsAny(string text, params string[] candidates)
    {
        return candidates.Any(candidate => text.Contains(FoldText(candidate), StringComparison.Ordinal));
    }

    private static string FoldText(string text)
    {
        return LiveRagConnectorHelpers.FoldText(text);
    }

    private static string BuildRowTitle(Dictionary<string, string> values, string tableName, int rowNumber)
    {
        foreach (var preferred in new[] { "name", "title", "city", "city_name", "cityname", "stadt", "stadtname" })
        {
            var match = values.FirstOrDefault(kvp => string.Equals(kvp.Key, preferred, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.Value) && match.Value != "NULL")
                return match.Value;
        }

        var firstValue = values.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && value != "NULL");
        return string.IsNullOrWhiteSpace(firstValue)
            ? $"{tableName} row {rowNumber}"
            : firstValue.Length > 100 ? firstValue[..100] + "..." : firstValue;
    }

    private async Task<List<SearchResult>> ExecuteCustomQueryAsync(DbConnection connection, string searchTerm, int maxResults, CancellationToken ct)
    {
        var results = new List<SearchResult>();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = _customQuery.Replace("@SearchTerm", "@SearchTermParam");
            
            var param = command.CreateParameter();
            param.ParameterName = "@SearchTermParam";
            param.Value = searchTerm;
            command.Parameters.Add(param);

            var wildcardParam = command.CreateParameter();
            wildcardParam.ParameterName = "@SearchTermWildcard";
            wildcardParam.Value = $"%{searchTerm}%";
            command.Parameters.Add(wildcardParam);

            using var reader = await command.ExecuteReaderAsync(ct);
            
            var columns = new List<ColumnInfo>();
            for (int i = 0; i < reader.FieldCount; i++)
                columns.Add(new ColumnInfo { Name = reader.GetName(i), DataType = reader.GetDataTypeName(i) });

            while (await reader.ReadAsync(ct))
            {
                if (results.Count >= maxResults) break;
                results.Add(CreateSearchResultFromRow(reader, "CustomQuery", searchTerm, columns));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlDatabaseConnector] Custom query error: {ex.Message}");
        }

        return results;
    }

    public async Task<bool> TestConnectionAsync()
    {
        if (!_isInitialized) return false;
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            return connection.State == ConnectionState.Open;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlDatabaseConnector] Connection test failed: {ex.Message}");
            return false;
        }
    }

    public DetailViewConfiguration GetDetailViewConfiguration(SearchResult result)
    {
        return new DetailViewConfiguration
        {
            ViewType = DetailViewType.Custom,
            DisplayProperties = ["TableName", "MatchingColumns"],
            Actions = GetSqlActions()
        };
    }

    private List<ResultAction> GetSqlActions() =>
    [
        new() { Id = "copy-json", Name = L.Connector_SQL_CopyJson, Icon = "\uD83E\uDDFE", Description = L.Connector_SQL_CopyJson_Desc },
        new() { Id = "copy-insert", Name = L.Connector_SQL_CopyInsert, Icon = "\uD83D\uDDC3", Description = L.Connector_SQL_CopyInsert_Desc }
    ];

    public FrameworkElement? CreateCustomDetailView(SearchResult result)
    {
        var stackPanel = new StackPanel { Margin = new Thickness(8) };

        var tableName = result.Metadata.GetValueOrDefault("TableName")?.ToString() ?? "Unknown";
        var dbType = result.Metadata.GetValueOrDefault("DatabaseType")?.ToString() ?? "";
        
        var header = new TextBlock
        {
            Text = $"[{dbType}] {tableName}",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(52, 73, 94)),
            Margin = new Thickness(0, 0, 0, 12)
        };
        stackPanel.Children.Add(header);

        var matchingCols = result.Metadata.GetValueOrDefault("MatchingColumns")?.ToString();
        if (!string.IsNullOrEmpty(matchingCols))
        {
            var matchBlock = new TextBlock
            {
                Text = $"{L.Connector_SQL_MatchesIn}: {matchingCols}",
                FontSize = 12,
                FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            stackPanel.Children.Add(matchBlock);
        }

        var grid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var rowIndex = 0;
        var excludeKeys = new[] { "Type", "TableName", "DatabaseType", "MatchingColumns" };

        foreach (var kvp in result.Metadata)
        {
            if (excludeKeys.Contains(kvp.Key)) continue;

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var nameBlock = new TextBlock
            {
                Text = kvp.Key,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Margin = new Thickness(0, 2, 8, 2),
                Foreground = new SolidColorBrush(Color.FromRgb(52, 73, 94))
            };
            Grid.SetRow(nameBlock, rowIndex);
            Grid.SetColumn(nameBlock, 0);
            grid.Children.Add(nameBlock);

            var valueText = kvp.Value?.ToString() ?? "(NULL)";
            if (valueText.Length > 200) valueText = valueText[..200] + "...";

            var valueBlock = new TextBlock
            {
                Text = valueText,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 2),
                Foreground = kvp.Value == null || kvp.Value == DBNull.Value
                    ? new SolidColorBrush(Colors.Gray)
                    : new SolidColorBrush(Colors.Black)
            };
            Grid.SetRow(valueBlock, rowIndex);
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(valueBlock);

            rowIndex++;
        }

        var scrollViewer = new ScrollViewer
        {
            Content = grid,
            MaxHeight = 300,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        stackPanel.Children.Add(scrollViewer);
        return stackPanel;
    }

    public async Task<bool> ExecuteActionAsync(SearchResult result, string actionId)
    {
        return await Task.Run(() =>
        {
            try
            {
                var excludeKeys = new[] { "Type", "TableName", "DatabaseType", "MatchingColumns" };
                var data = result.Metadata
                    .Where(kvp => !excludeKeys.Contains(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                switch (actionId)
                {
                    case "copy-json":
                        var json = System.Text.Json.JsonSerializer.Serialize(data, 
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(json));
                        return true;

                    case "copy-insert":
                        var tableName = result.Metadata.GetValueOrDefault("TableName")?.ToString() ?? "TableName";
                        var columns = string.Join(", ", data.Keys);
                        var values = string.Join(", ", data.Values.Select(v => 
                            v == null || v == DBNull.Value ? "NULL" : $"'{v.ToString()?.Replace("'", "''")}'"));
                        var insertSql = $"INSERT INTO {tableName} ({columns}) VALUES ({values});";
                        Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(insertSql));
                        return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SqlDatabaseConnector] Action error: {ex.Message}");
            }

            return false;
        });
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
        _isInitialized = false;
        GC.SuppressFinalize(this);
    }

    private enum DatabaseType { MSSQL, MySQL, PostgreSQL }
    private class ColumnInfo { public string Name { get; set; } = string.Empty; public string DataType { get; set; } = string.Empty; }
    private class TableInfo { public string Name { get; set; } = string.Empty; public List<ColumnInfo> Columns { get; set; } = new(); }
    private sealed record SqlLiveRagPlan(string TableName, string Sql);
}
