using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using MSCC.Connectors;
using MSCC.Models;
using MSCC.Services;

namespace MSCC.Tests.Integration;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
public class McpServerServiceTests
{
    private string _tempDirectory = string.Empty;
    private DataSourceManager _dataSourceManager = null!;
    private McpServerService _mcpServer = null!;
    private List<DataSource> _originalDataSources = new();
    private List<DataSourceGroup> _originalGroups = new();
    private List<SearchQuery> _originalQueries = new();
    private Dictionary<string, IDataSourceConnector> _originalConnectors = new();

    [SetUp]
    public async Task SetUp()
    {
        _originalDataSources = GlobalState.DataSources.ToList();
        _originalGroups = GlobalState.Groups.ToList();
        _originalQueries = GlobalState.Queries.ToList();
        _originalConnectors = GlobalState.Connectors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        GlobalState.DataSources.Clear();
        GlobalState.Groups.Clear();
        GlobalState.Connectors.Clear();
        GlobalState.Queries.Clear();

        _tempDirectory = Path.Combine(Path.GetTempPath(), "mscc-mcp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirectory, "rag-smoke-test.txt"),
            "This document proves that MCP live RAG can retrieve plugin data on demand.");

        _dataSourceManager = new DataSourceManager();
        _dataSourceManager.RegisterDefaultConnectors();

        await _dataSourceManager.CreateDataSourceAsync(
            "MCP Test Files",
            "filesystem-connector",
            new Dictionary<string, string>
            {
                ["BasePath"] = _tempDirectory,
                ["SearchPattern"] = "*.txt",
                ["IncludeSubdirectories"] = "true"
            });

        _mcpServer = new McpServerService(_dataSourceManager);
    }

    [TearDown]
    public void TearDown()
    {
        _mcpServer.Dispose();
        _dataSourceManager.Dispose();

        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);

        GlobalState.DataSources.Clear();
        foreach (var item in _originalDataSources)
            GlobalState.DataSources.Add(item);

        GlobalState.Groups.Clear();
        foreach (var item in _originalGroups)
            GlobalState.Groups.Add(item);

        GlobalState.Connectors.Clear();
        foreach (var item in _originalConnectors)
            GlobalState.Connectors[item.Key] = item.Value;

        GlobalState.Queries.Clear();
        foreach (var item in _originalQueries)
            GlobalState.Queries.Add(item);
    }

    [Test]
    public async Task McpServer_ExposesToolsAndRunsLiveRagTool()
    {
        var port = GetFreePort();
        Assert.That(await _mcpServer.StartAsync(port), Is.True);

        using var client = new HttpClient();
        var endpoint = _mcpServer.EndpointUrl;

        var initialize = await PostRpcAsync(client, endpoint, new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new
            {
                protocolVersion = "2025-06-18",
                capabilities = new { },
                clientInfo = new { name = "mscc-test", version = "1.0.0" }
            }
        });

        Assert.That(initialize.RootElement.GetProperty("result").GetProperty("capabilities").TryGetProperty("tools", out _), Is.True);

        var toolsList = await PostRpcAsync(client, endpoint, new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/list"
        });

        var tools = toolsList.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToList();
        Assert.That(tools.Any(tool => tool.GetProperty("name").GetString() == "mscc.live_rag_search"), Is.True);
        Assert.That(tools.Any(tool => tool.GetProperty("name").GetString() == "mscc.list_data_sources"), Is.True);

        var listSources = await PostRpcAsync(client, endpoint, new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "tools/call",
            @params = new
            {
                name = "mscc.list_data_sources",
                arguments = new { }
            }
        });

        var listText = listSources.RootElement
            .GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        Assert.That(listText, Does.Contain("MCP Test Files"));

        var resourcesList = await PostRpcAsync(client, endpoint, new
        {
            jsonrpc = "2.0",
            id = 30,
            method = "resources/list"
        });

        var resources = resourcesList.RootElement.GetProperty("result").GetProperty("resources").EnumerateArray().ToList();
        Assert.That(resources.Any(resource => resource.GetProperty("uri").GetString() == "mscc://data-sources"), Is.True);

        var readResources = await PostRpcAsync(client, endpoint, new
        {
            jsonrpc = "2.0",
            id = 31,
            method = "resources/read",
            @params = new
            {
                uri = "mscc://data-sources"
            }
        });

        var resourceText = readResources.RootElement
            .GetProperty("result")
            .GetProperty("contents")[0]
            .GetProperty("text")
            .GetString();
        Assert.That(resourceText, Does.Contain("MCP Test Files"));

        var ragSearch = await PostRpcAsync(client, endpoint, new
        {
            jsonrpc = "2.0",
            id = 4,
            method = "tools/call",
            @params = new
            {
                name = "mscc.live_rag_search",
                arguments = new
                {
                    query = "MCP live RAG plugin data",
                    maxContextItemsTotal = 5,
                    maxCharactersPerItem = 1000,
                    useAiPlanning = false
                }
            }
        });

        var ragText = ragSearch.RootElement
            .GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        Assert.That(ragText, Does.Contain("MCP live RAG"));
        Assert.That(ragText, Does.Contain("contextItems"));
        Assert.That(ragText, Does.Contain("executedOperations"));
        Assert.That(ragText, Does.Contain("evidence"));
        Assert.That(ragText, Does.Contain("plan"));
    }

    private static async Task<JsonDocument> PostRpcAsync(HttpClient client, string endpoint, object request)
    {
        using var response = await client.PostAsJsonAsync(endpoint, request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
