// Meta Search and Control Center (c) 2026 Dennis Michael Heine
// SQL Database Connector - Supports MySQL, MSSQL, PostgreSQL
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text;
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

            if (configuration.TryGetValue("DatabaseType", out var dbType))
            {
                _databaseType = dbType.ToUpperInvariant() switch
                {
                    "MYSQL" => DatabaseType.MySQL,
                    "POSTGRESQL" or "POSTGRES" => DatabaseType.PostgreSQL,
                    _ => DatabaseType.MSSQL
                };
            }

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

            var whereClause = BuildSearchWhereClause(columns, searchTerm);
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

    private string BuildSearchWhereClause(List<ColumnInfo> columns, string searchTerm)
    {
        var conditions = new List<string>();
        
        foreach (var column in columns)
        {
            if (IsSearchableColumn(column.DataType))
            {
                var quotedColumn = QuoteIdentifier(column.Name);
                var castExpression = _databaseType switch
                {
                    DatabaseType.PostgreSQL => $"CAST({quotedColumn} AS TEXT)",
                    _ => $"CAST({quotedColumn} AS NVARCHAR(MAX))"
                };
                conditions.Add($"{castExpression} LIKE @SearchTerm");
            }
        }

        if (conditions.Count == 0)
        {
            if (columns.Count > 0)
            {
                var quotedColumn = QuoteIdentifier(columns[0].Name);
                conditions.Add($"CAST({quotedColumn} AS NVARCHAR(MAX)) LIKE @SearchTerm");
            }
            else return "1=0";
        }

        return string.Join(" OR ", conditions);
    }

    private static bool IsSearchableColumn(string dataType)
    {
        var searchableTypes = new[] { "varchar", "nvarchar", "char", "nchar", "text", "ntext",
            "character varying", "character", "longtext", "mediumtext", "tinytext", "xml", "json" };
        return searchableTypes.Any(t => dataType.StartsWith(t, StringComparison.OrdinalIgnoreCase));
    }

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
        new() { Id = "copy-json", Name = L.Connector_SQL_CopyJson, Icon = "[JSON]", Description = L.Connector_SQL_CopyJson_Desc },
        new() { Id = "copy-insert", Name = L.Connector_SQL_CopyInsert, Icon = "[SQL]", Description = L.Connector_SQL_CopyInsert_Desc }
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
}
