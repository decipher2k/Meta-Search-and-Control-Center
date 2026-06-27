//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.IO;
using MSCC.Models;

namespace MSCC.Services;

/// <summary>
/// Local MCP Streamable HTTP server exposing MSCC data sources as MCP tools/resources.
/// </summary>
public sealed class McpServerService : IDisposable
{
    private const string ProtocolVersion = "2025-06-18";
    private readonly DataSourceManager _dataSourceManager;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private HttpListener? _listener;
    private TcpListener? _tcpListener;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;
    private int _port;

    public McpServerService(DataSourceManager dataSourceManager)
    {
        _dataSourceManager = dataSourceManager;
    }

    public bool IsRunning => _listener?.IsListening == true || _tcpListener != null;
    public string EndpointUrl => $"http://localhost:{_port}/mcp";

    public Task<bool> StartAsync(int port)
    {
        port = Math.Clamp(port, 1024, 65535);

        if (IsRunning && _port == port)
            return Task.FromResult(true);

        Stop();

        try
        {
            _port = port;
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Start();
            _serverTask = Task.Run(() => ListenAsync(_cts.Token));
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MCP] Failed to start server: {ex.Message}");
            Stop();
            return Task.FromResult(StartTcpFallback(port));
        }
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
            _tcpListener?.Stop();
        }
        catch
        {
            // Ignore shutdown errors.
        }
        finally
        {
            _listener = null;
            _tcpListener = null;
            _cts?.Dispose();
            _cts = null;
            _serverTask = null;
        }
    }

    private bool StartTcpFallback(int port)
    {
        try
        {
            _port = port;
            _cts = new CancellationTokenSource();
            _tcpListener = new TcpListener(IPAddress.Loopback, port);
            _tcpListener.Start();
            _serverTask = Task.Run(() => ListenTcpAsync(_cts.Token));
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MCP] Failed to start TCP fallback: {ex.Message}");
            Stop();
            return false;
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener?.IsListening == true)
        {
            HttpListenerContext? context = null;
            try
            {
                context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context, cancellationToken), cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MCP] Listener error: {ex.Message}");
                if (context != null)
                    await WriteHttpErrorAsync(context.Response, 500, "MCP listener error");
            }
        }
    }

    private async Task ListenTcpAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _tcpListener != null)
        {
            try
            {
                var client = await _tcpListener.AcceptTcpClientAsync(cancellationToken);
                _ = Task.Run(() => HandleTcpClientAsync(client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MCP] TCP listener error: {ex.Message}");
            }
        }
    }

    private async Task HandleTcpClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var tcpClient = client;

        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);

            var requestLine = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                await WriteTcpErrorAsync(stream, 400, "Bad request", cancellationToken);
                return;
            }

            var requestParts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (requestParts.Length < 2)
            {
                await WriteTcpErrorAsync(stream, 400, "Bad request", cancellationToken);
                return;
            }

            var method = requestParts[0];
            var path = requestParts[1].Split('?')[0].TrimEnd('/');
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            while (true)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line == null || line.Length == 0)
                    break;

                var separator = line.IndexOf(':');
                if (separator > 0)
                    headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }

            if (!IsAllowedOrigin(headers.TryGetValue("Origin", out var origin) ? origin : null))
            {
                await WriteTcpErrorAsync(stream, 403, "Forbidden origin", cancellationToken);
                return;
            }

            if (!string.Equals(path, "/mcp", StringComparison.OrdinalIgnoreCase))
            {
                await WriteTcpErrorAsync(stream, 404, "Not found", cancellationToken);
                return;
            }

            if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                await WriteTcpErrorAsync(stream, 405, "Method not allowed", cancellationToken);
                return;
            }

            var body = headers.TryGetValue("Transfer-Encoding", out var transferEncoding) &&
                transferEncoding.Contains("chunked", StringComparison.OrdinalIgnoreCase)
                    ? await ReadChunkedBodyAsync(reader, cancellationToken)
                    : await ReadContentLengthBodyAsync(reader, headers, cancellationToken);

            if (string.IsNullOrWhiteSpace(body))
            {
                await WriteTcpJsonAsync(stream, CreateError(null, -32700, "Empty JSON-RPC request"), 200, cancellationToken);
                return;
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var responses = new List<object>();
                foreach (var item in root.EnumerateArray())
                {
                    var rpcResponse = await HandleJsonRpcAsync(item, cancellationToken);
                    if (rpcResponse != null)
                        responses.Add(rpcResponse);
                }

                if (responses.Count == 0)
                {
                    await WriteTcpTextAsync(stream, string.Empty, 202, "text/plain", cancellationToken);
                    return;
                }

                await WriteTcpJsonAsync(stream, responses, 200, cancellationToken);
                return;
            }

            var result = await HandleJsonRpcAsync(root, cancellationToken);
            if (result == null)
            {
                await WriteTcpTextAsync(stream, string.Empty, 202, "text/plain", cancellationToken);
                return;
            }

            await WriteTcpJsonAsync(stream, result, 200, cancellationToken);
        }
        catch (JsonException)
        {
            try
            {
                using var stream = client.GetStream();
                await WriteTcpJsonAsync(stream, CreateError(null, -32700, "Parse error"), 200, cancellationToken);
            }
            catch { }
        }
        catch (Exception ex)
        {
            try
            {
                using var stream = client.GetStream();
                await WriteTcpJsonAsync(stream, CreateError(null, -32603, ex.Message), 200, cancellationToken);
            }
            catch { }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            if (!IsAllowedOrigin(request))
            {
                await WriteHttpErrorAsync(response, 403, "Forbidden origin");
                return;
            }

            var path = request.Url?.AbsolutePath.TrimEnd('/');
            if (!string.Equals(path, "/mcp", StringComparison.OrdinalIgnoreCase))
            {
                await WriteHttpErrorAsync(response, 404, "Not found");
                return;
            }

            if (request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                await WriteHttpErrorAsync(response, 405, "SSE stream is not implemented; use HTTP POST.");
                return;
            }

            if (!request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                await WriteHttpErrorAsync(response, 405, "Method not allowed");
                return;
            }

            using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
            var body = await reader.ReadToEndAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
            {
                await WriteJsonAsync(response, CreateError(null, -32700, "Empty JSON-RPC request"), cancellationToken);
                return;
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var responses = new List<object>();
                foreach (var item in root.EnumerateArray())
                {
                    var rpcResponse = await HandleJsonRpcAsync(item, cancellationToken);
                    if (rpcResponse != null)
                        responses.Add(rpcResponse);
                }

                if (responses.Count == 0)
                {
                    response.StatusCode = 202;
                    response.Close();
                    return;
                }

                await WriteJsonAsync(response, responses, cancellationToken);
                return;
            }

            var result = await HandleJsonRpcAsync(root, cancellationToken);
            if (result == null)
            {
                response.StatusCode = 202;
                response.Close();
                return;
            }

            await WriteJsonAsync(response, result, cancellationToken);
        }
        catch (JsonException)
        {
            await WriteJsonAsync(response, CreateError(null, -32700, "Parse error"), cancellationToken);
        }
        catch (Exception ex)
        {
            await WriteJsonAsync(response, CreateError(null, -32603, ex.Message), cancellationToken);
        }
    }

    private async Task<object?> HandleJsonRpcAsync(JsonElement request, CancellationToken cancellationToken)
    {
        if (request.ValueKind != JsonValueKind.Object)
            return CreateError(null, -32600, "Invalid request");

        var id = ReadId(request);
        var hasId = request.TryGetProperty("id", out _);

        if (!request.TryGetProperty("method", out var methodElement) ||
            methodElement.ValueKind != JsonValueKind.String)
        {
            return hasId ? CreateError(id, -32600, "Missing method") : null;
        }

        var method = methodElement.GetString() ?? "";
        var parameters = request.TryGetProperty("params", out var paramsElement)
            ? paramsElement
            : default;

        if (!hasId)
        {
            if (method == "notifications/initialized")
                return null;

            return null;
        }

        return method switch
        {
            "initialize" => CreateResult(id, HandleInitialize(parameters)),
            "ping" => CreateResult(id, new { }),
            "tools/list" => CreateResult(id, new { tools = BuildToolList() }),
            "tools/call" => CreateResult(id, await HandleToolCallAsync(parameters, cancellationToken)),
            "resources/list" => CreateResult(id, new { resources = BuildResourceList() }),
            "resources/read" => CreateResult(id, await HandleResourceReadAsync(parameters, cancellationToken)),
            _ => CreateError(id, -32601, $"Method not found: {method}")
        };
    }

    private object HandleInitialize(JsonElement parameters)
    {
        var requestedVersion = TryGetString(parameters, "protocolVersion");
        var negotiatedVersion = string.IsNullOrWhiteSpace(requestedVersion)
            ? ProtocolVersion
            : requestedVersion;

        return new
        {
            protocolVersion = negotiatedVersion,
            capabilities = new
            {
                tools = new { listChanged = true },
                resources = new { subscribe = false, listChanged = true }
            },
            serverInfo = new
            {
                name = "mscc-mcp-server",
                title = "MSCC Plugin Live RAG Server",
                version = "1.0.0"
            },
            instructions = "Use mscc.live_rag_search or data-source specific tools to query MSCC plugins live. The server returns bounded context chunks and never dumps entire data sources."
        };
    }

    private List<object> BuildToolList()
    {
        var tools = new List<object>
        {
            new
            {
                name = "mscc.list_data_sources",
                title = "List MSCC Data Sources",
                description = "Lists currently configured MSCC data sources and groups.",
                inputSchema = new { type = "object", additionalProperties = false }
            },
            new
            {
                name = "mscc.live_rag_search",
                title = "Live RAG Search",
                description = "Runs a live RAG query through selected MSCC data source plugins. Query execution happens inside plugins on demand.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Natural language query or focused retrieval question." },
                        searchTerms = new { type = "array", items = new { type = "string" }, description = "Optional concrete keyword phrases, synonyms, file names, table/field names, or API search terms derived from the query." },
                        dataSourceIds = new { type = "array", items = new { type = "string" }, description = "Optional data source IDs. Omit to search all enabled sources." },
                        groupIds = new { type = "array", items = new { type = "string" }, description = "Optional group IDs." },
                        maxResultsPerSearchTerm = new { type = "integer", description = "Maximum raw plugin results per query." },
                        maxOperationsPerSource = new { type = "integer", description = "Maximum validated live operations per source." },
                        maxCandidateItems = new { type = "integer", description = "Maximum candidate rows/chunks retrieved before final evidence clipping." },
                        maxContextItemsPerSource = new { type = "integer", description = "Maximum context chunks per source." },
                        maxContextItemsTotal = new { type = "integer", description = "Maximum total context chunks." },
                        maxCharactersPerItem = new { type = "integer", description = "Maximum characters per context chunk." },
                        includeMetadata = new { type = "boolean", description = "Whether metadata should be returned." },
                        useAiPlanning = new { type = "boolean", description = "Whether the server may use the configured AI model to create a live retrieval plan. Defaults to true." }
                    },
                    required = new[] { "query" }
                }
            },
            new
            {
                name = "mscc.keyword_search",
                title = "Keyword Search",
                description = "Runs the classic MSCC keyword search through selected data source plugins.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        searchTerm = new { type = "string", description = "Keyword or search expression." },
                        dataSourceIds = new { type = "array", items = new { type = "string" } },
                        groupIds = new { type = "array", items = new { type = "string" } },
                        maxResultsPerSource = new { type = "integer" }
                    },
                    required = new[] { "searchTerm" }
                }
            }
        };

        foreach (var source in GlobalState.DataSources.Where(source => source.IsEnabled))
        {
            tools.Add(new
            {
                name = BuildDataSourceToolName(source.Id),
                title = $"Live RAG: {source.Name}",
                description = $"Runs live RAG only against '{source.Name}' ({source.ConnectorId}).",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Focused retrieval query." },
                        searchTerms = new { type = "array", items = new { type = "string" }, description = "Optional concrete keyword phrases or synonyms for this data source." },
                        maxContextItems = new { type = "integer", description = "Maximum chunks from this data source." },
                        maxCharactersPerItem = new { type = "integer", description = "Maximum characters per context chunk." },
                        includeMetadata = new { type = "boolean", description = "Whether metadata should be returned." },
                        useAiPlanning = new { type = "boolean", description = "Whether the server may use the configured AI model to create a live retrieval plan. Defaults to true." }
                    },
                    required = new[] { "query" }
                }
            });
        }

        return tools;
    }

    private async Task<object> HandleToolCallAsync(JsonElement parameters, CancellationToken cancellationToken)
    {
        var name = TryGetString(parameters, "name");
        var arguments = parameters.TryGetProperty("arguments", out var args) && args.ValueKind == JsonValueKind.Object
            ? args
            : default;

        if (string.IsNullOrWhiteSpace(name))
            return CreateToolError("Tool name is required.");

        if (name == "mscc.list_data_sources")
            return CreateToolResult(BuildDataSourcesPayload(), isError: false);

        if (name == "mscc.live_rag_search")
            return await CallLiveRagToolAsync(arguments, null, cancellationToken);

        if (name == "mscc.keyword_search")
            return await CallKeywordSearchToolAsync(arguments, cancellationToken);

        var dataSource = GlobalState.DataSources
            .FirstOrDefault(source => source.IsEnabled && BuildDataSourceToolName(source.Id) == name);

        if (dataSource != null)
            return await CallLiveRagToolAsync(arguments, dataSource.Id, cancellationToken);

        return CreateToolError($"Unknown tool: {name}");
    }

    private async Task<object> CallLiveRagToolAsync(
        JsonElement arguments,
        string? forcedDataSourceId,
        CancellationToken cancellationToken)
    {
        var query = TryGetString(arguments, "query");
        if (string.IsNullOrWhiteSpace(query))
            return CreateToolError("Argument 'query' is required.");

        var dataSourceIds = forcedDataSourceId != null
            ? new List<string> { forcedDataSourceId }
            : TryGetStringArray(arguments, "dataSourceIds");
        var groupIds = forcedDataSourceId != null
            ? new List<string>()
            : TryGetStringArray(arguments, "groupIds");

        if (dataSourceIds.Count == 0 && groupIds.Count == 0)
        {
            dataSourceIds = GlobalState.DataSources
                .Where(source => source.IsEnabled)
                .Select(source => source.Id)
                .ToList();
        }

        var searchTerms = TryGetStringArray(arguments, "searchTerms");
        if (searchTerms.Count == 0)
            searchTerms.Add(query);

        var aiService = new AiSearchService();
        var orchestrator = new LiveRagOrchestrator(_dataSourceManager, aiService);
        var result = await orchestrator.GetLiveRagContextAsync(
            query,
            dataSourceIds,
            groupIds,
            maxResultsPerOperation: TryGetInt(arguments, "maxResultsPerSearchTerm", 20, 1, 100),
            maxContextItemsPerSource: TryGetInt(arguments, forcedDataSourceId != null ? "maxContextItems" : "maxContextItemsPerSource", 10, 1, 100),
            maxContextItemsTotal: TryGetInt(arguments, "maxContextItemsTotal", 40, 1, 200),
            maxCharactersPerItem: TryGetInt(arguments, "maxCharactersPerItem", 2500, 250, 20000),
            includeMetadata: TryGetBool(arguments, "includeMetadata", true),
            seedSearchTerms: searchTerms,
            useAiPlanning: TryGetBool(arguments, "useAiPlanning", true),
            cancellationToken: cancellationToken);

        var payload = new
        {
            success = result.Success,
            question = result.Question,
            searchTerms = result.SearchTerms,
            mode = result.Mode.ToString(),
            degradedFallback = result.IsDegradedFallback,
            error = result.ErrorMessage,
            plan = result.PlanOperations.Select(operation => new
            {
                operation.Id,
                operation.DataSourceId,
                operation.SourceName,
                operation.ConnectorId,
                type = operation.Type.ToString(),
                operation.Query,
                operation.SearchTerms,
                operation.Target,
                operation.SelectFields,
                operation.Filters,
                operation.SortField,
                operation.SortDescending,
                operation.Limit,
                operation.Rationale,
                operation.IsDegradedFallback,
                operation.Options
            }),
            evidence = result.ContextItems.Select((item, index) => new
            {
                sourceNumber = index + 1,
                title = item.Title,
                content = item.Content,
                sourceName = item.SourceName,
                connectorId = item.ConnectorId,
                reference = item.OriginalReference,
                relevanceScore = item.RelevanceScore,
                nativeLiveRag = item.FromNativeLiveRag,
                retrievalQuery = item.RetrievalQuery,
                operationId = item.OperationId,
                operationType = item.OperationType?.ToString(),
                metadata = item.Metadata
            }),
            contextItems = result.ContextItems.Select((item, index) => new
            {
                sourceNumber = index + 1,
                title = item.Title,
                content = item.Content,
                sourceName = item.SourceName,
                connectorId = item.ConnectorId,
                reference = item.OriginalReference,
                relevanceScore = item.RelevanceScore,
                nativeLiveRag = item.FromNativeLiveRag,
                retrievalQuery = item.RetrievalQuery,
                operationId = item.OperationId,
                operationType = item.OperationType?.ToString(),
                metadata = item.Metadata
            }),
            executedOperations = result.ExecutionTrace.Select(trace => new
            {
                trace.OperationId,
                trace.DataSourceId,
                trace.SourceName,
                trace.ConnectorId,
                type = trace.OperationType.ToString(),
                trace.Accepted,
                trace.Reason,
                trace.ResultCount,
                trace.ErrorMessage,
                trace.StartedAt,
                trace.CompletedAt
            }),
            diagnostics = result.SourceResults.Select(source => new
            {
                sourceName = source.SourceName,
                connectorId = source.ConnectorId,
                dataSourceId = source.DataSourceId,
                nativeLiveRag = source.IsNativeLiveRag,
                contextCount = source.ContextItems.Count,
                executedQueries = source.ExecutedQueries,
                executedOperations = source.ExecutedOperations.Select(operation => operation.Id),
                rejectedOperations = source.RejectedOperations.Select(operation => new
                {
                    operation.Id,
                    type = operation.Type.ToString(),
                    operation.Options
                }),
                error = source.ErrorMessage
            }),
            orchestrationDiagnostics = result.Diagnostics
        };

        return CreateToolResult(payload, isError: !result.Success);
    }

    private async Task<object> CallKeywordSearchToolAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var searchTerm = TryGetString(arguments, "searchTerm");
        if (string.IsNullOrWhiteSpace(searchTerm))
            return CreateToolError("Argument 'searchTerm' is required.");

        var dataSourceIds = TryGetStringArray(arguments, "dataSourceIds");
        var groupIds = TryGetStringArray(arguments, "groupIds");
        if (dataSourceIds.Count == 0 && groupIds.Count == 0)
        {
            dataSourceIds = GlobalState.DataSources
                .Where(source => source.IsEnabled)
                .Select(source => source.Id)
                .ToList();
        }

        var searchService = new SearchService(_dataSourceManager);
        var query = await searchService.ExecuteSearchAsync(
            searchTerm,
            dataSourceIds,
            groupIds,
            cancellationToken: cancellationToken);

        var results = await searchService.GetSearchResultsAsync(
            query,
            TryGetInt(arguments, "maxResultsPerSource", 25, 1, 100),
            cancellationToken);

        var payload = new
        {
            searchTerm,
            resultCount = results.Count,
            results = results.Select(result => new
            {
                result.Title,
                result.Description,
                result.SourceName,
                result.ConnectorId,
                reference = result.OriginalReference,
                result.RelevanceScore,
                result.Metadata
            })
        };

        return CreateToolResult(payload, isError: false);
    }

    private List<object> BuildResourceList()
    {
        var resources = new List<object>
        {
            new
            {
                uri = "mscc://data-sources",
                name = "MSCC Data Sources",
                title = "MSCC Data Sources",
                description = "Current MSCC data source catalog.",
                mimeType = "application/json"
            }
        };

        foreach (var source in GlobalState.DataSources)
        {
            resources.Add(new
            {
                uri = $"mscc://data-sources/{source.Id}",
                name = source.Name,
                title = source.Name,
                description = source.Description,
                mimeType = "application/json"
            });
        }

        foreach (var group in GlobalState.Groups)
        {
            resources.Add(new
            {
                uri = $"mscc://groups/{group.Id}",
                name = group.Name,
                title = group.Name,
                description = group.Description,
                mimeType = "application/json"
            });
        }

        return resources;
    }

    private Task<object> HandleResourceReadAsync(JsonElement parameters, CancellationToken cancellationToken)
    {
        var uri = TryGetString(parameters, "uri");
        if (string.IsNullOrWhiteSpace(uri))
            return Task.FromResult<object>(new { contents = Array.Empty<object>() });

        object payload;
        if (uri == "mscc://data-sources")
        {
            payload = BuildDataSourcesPayload();
        }
        else if (uri.StartsWith("mscc://data-sources/", StringComparison.OrdinalIgnoreCase))
        {
            var id = uri["mscc://data-sources/".Length..];
            var source = GlobalState.DataSources.FirstOrDefault(item => item.Id == id);
            payload = source == null
                ? new { error = "Data source not found", id }
                : CreateDataSourcePayload(source);
        }
        else if (uri.StartsWith("mscc://groups/", StringComparison.OrdinalIgnoreCase))
        {
            var id = uri["mscc://groups/".Length..];
            var group = GlobalState.Groups.FirstOrDefault(item => item.Id == id);
            payload = group == null
                ? new { error = "Group not found", id }
                : new
                {
                    group.Id,
                    group.Name,
                    group.Description,
                    group.Color,
                    dataSources = GlobalState.GetDataSourcesByGroup(group.Id).Select(CreateDataSourcePayload)
                };
        }
        else
        {
            payload = new { error = "Unsupported resource URI", uri };
        }

        return Task.FromResult<object>(new
        {
            contents = new[]
            {
                new
                {
                    uri,
                    mimeType = "application/json",
                    text = JsonSerializer.Serialize(payload, _jsonOptions)
                }
            }
        });
    }

    private object BuildDataSourcesPayload()
    {
        return new
        {
            endpoint = EndpointUrl,
            dataSources = GlobalState.DataSources.Select(CreateDataSourcePayload),
            groups = GlobalState.Groups.Select(group => new
            {
                group.Id,
                group.Name,
                group.Description,
                group.Color
            })
        };
    }

    private static object CreateDataSourcePayload(DataSource source)
    {
        var connector = GlobalState.Connectors.TryGetValue(source.ConnectorId, out var connectorTemplate)
            ? connectorTemplate
            : null;

        return new
        {
            source.Id,
            source.Name,
            source.Description,
            source.ConnectorId,
            source.GroupId,
            source.IsEnabled,
            supportsLiveRag = connectorTemplate?.SupportsLiveRag ?? false,
            connectorName = connector?.Name ?? source.ConnectorId
        };
    }

    private static object CreateToolResult(object payload, bool isError)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new
        {
            content = new[]
            {
                new { type = "text", text = json }
            },
            structuredContent = payload,
            isError
        };
    }

    private static object CreateToolError(string message)
    {
        return new
        {
            content = new[]
            {
                new { type = "text", text = message }
            },
            isError = true
        };
    }

    private static string BuildDataSourceToolName(string dataSourceId)
    {
        var safe = new string(dataSourceId
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_')
            .ToArray());
        return $"mscc.live_rag.{safe}";
    }

    private static bool IsAllowedOrigin(HttpListenerRequest request)
    {
        return IsAllowedOrigin(request.Headers["Origin"]);
    }

    private static bool IsAllowedOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return true;

        return Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
               (uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase));
    }

    private static object? ReadId(JsonElement root)
    {
        if (!root.TryGetProperty("id", out var id))
            return null;

        return id.ValueKind switch
        {
            JsonValueKind.String => id.GetString(),
            JsonValueKind.Number when id.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => id.GetDouble(),
            JsonValueKind.Null => null,
            _ => id.Clone()
        };
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static List<string> TryGetStringArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            return new List<string>();
        }

        return array
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? "")
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int TryGetInt(JsonElement element, string propertyName, int defaultValue, int min, int max)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value) ||
            !value.TryGetInt32(out var intValue))
        {
            return defaultValue;
        }

        return Math.Clamp(intValue, min, max);
    }

    private static bool TryGetBool(JsonElement element, string propertyName, bool defaultValue)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value))
        {
            return defaultValue;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => defaultValue
        };
    }

    private static object CreateResult(object? id, object result)
    {
        return new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = result
        };
    }

    private static object CreateError(object? id, int code, string message)
    {
        return new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new
            {
                code,
                message
            }
        };
    }

    private async Task WriteJsonAsync(HttpListenerResponse response, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        response.StatusCode = 200;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken);
        response.Close();
    }

    private static async Task WriteHttpErrorAsync(HttpListenerResponse response, int statusCode, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        response.StatusCode = statusCode;
        response.ContentType = "text/plain; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    private async Task WriteTcpJsonAsync(
        NetworkStream stream,
        object payload,
        int statusCode,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        await WriteTcpTextAsync(stream, json, statusCode, "application/json; charset=utf-8", cancellationToken);
    }

    private static async Task<string> ReadContentLengthBodyAsync(
        StreamReader reader,
        Dictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        var contentLength = headers.TryGetValue("Content-Length", out var contentLengthText) &&
            int.TryParse(contentLengthText, out var parsedLength)
                ? parsedLength
                : 0;

        if (contentLength <= 0)
            return string.Empty;

        var buffer = new char[contentLength];
        var read = 0;
        while (read < buffer.Length)
        {
            var next = await reader.ReadAsync(buffer.AsMemory(read, buffer.Length - read), cancellationToken);
            if (next == 0)
                break;
            read += next;
        }

        return new string(buffer, 0, read);
    }

    private static async Task<string> ReadChunkedBodyAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var body = new StringBuilder();

        while (true)
        {
            var sizeLine = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(sizeLine))
                continue;

            var semicolonIndex = sizeLine.IndexOf(';');
            if (semicolonIndex >= 0)
                sizeLine = sizeLine[..semicolonIndex];

            if (!int.TryParse(sizeLine.Trim(), System.Globalization.NumberStyles.HexNumber, null, out var chunkSize))
                break;

            if (chunkSize == 0)
            {
                while (true)
                {
                    var trailer = await reader.ReadLineAsync(cancellationToken);
                    if (string.IsNullOrEmpty(trailer))
                        break;
                }
                break;
            }

            var buffer = new char[chunkSize];
            var read = 0;
            while (read < chunkSize)
            {
                var next = await reader.ReadAsync(buffer.AsMemory(read, chunkSize - read), cancellationToken);
                if (next == 0)
                    break;
                read += next;
            }

            body.Append(buffer, 0, read);
            await reader.ReadLineAsync(cancellationToken);
        }

        return body.ToString();
    }

    private static Task WriteTcpErrorAsync(
        NetworkStream stream,
        int statusCode,
        string message,
        CancellationToken cancellationToken)
    {
        return WriteTcpTextAsync(stream, message, statusCode, "text/plain; charset=utf-8", cancellationToken);
    }

    private static async Task WriteTcpTextAsync(
        NetworkStream stream,
        string body,
        int statusCode,
        string contentType,
        CancellationToken cancellationToken)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var header =
            $"HTTP/1.1 {statusCode} {GetStatusText(statusCode)}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {bodyBytes.Length}\r\n" +
            "Connection: close\r\n" +
            "\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes, cancellationToken);
        if (bodyBytes.Length > 0)
            await stream.WriteAsync(bodyBytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static string GetStatusText(int statusCode) => statusCode switch
    {
        200 => "OK",
        202 => "Accepted",
        400 => "Bad Request",
        403 => "Forbidden",
        404 => "Not Found",
        405 => "Method Not Allowed",
        _ => "Error"
    };

    public void Dispose()
    {
        Stop();
    }
}
