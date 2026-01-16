# MSCC - Meta Search Command Center

An extensible meta search engine for Windows that can search multiple data sources simultaneously. The application supports both built-in connectors and custom plugins.

## Table of Contents

- [Feature Overview](#feature-overview)
- [Installation](#installation)
- [User Interface](#user-interface)
- [Connectors and Data Sources](#connectors-and-data-sources)
- [Plugin Development](#plugin-development)
  - [Option 1: Script-based Plugins](#option-1-script-based-plugins)
  - [Option 2: Compiled Plugins (Visual Studio)](#option-2-compiled-plugins-visual-studio)
- [API Reference](#api-reference)
- [Examples](#examples)

---

## Feature Overview

### Core Features

- **Multi-Source Search**: Search multiple data sources simultaneously
- **Data Source Groups**: Organize your data sources into logical groups
- **Label System**: Tag search results with keywords for later reference
- **Saved Queries**: Save and load frequently used searches
- **Extensible**: Add custom connectors via script or as compiled plugins

### Built-in Connectors

| Connector | Description |
|-----------|-------------|
| **File System** | Searches local files and folders by filename |
| **Mock Database** | Demo connector for testing purposes |

---

## Installation

### Prerequisites

- Windows 10/11
- .NET 10 Runtime

### Build from Source

```bash
git clone <repository-url>
cd MSCC
dotnet build
dotnet run
```

---

## User Interface

### Main Window

The main window is divided into three areas:

1. **Left Sidebar**: Manage groups and data sources
2. **Center**: Search results and detail view
3. **Right Sidebar**: Labels and saved queries

### Managing Data Sources

1. Click **+** next to "Data Sources"
2. Select a connector type
3. Enter a name and configuration
4. Click **Save**

### Performing a Search

1. Enable the desired data sources (checkbox)
2. Enter a search term
3. Press **Enter** or click **Search**

---

## Connectors and Data Sources

### Concept

- **Connector**: A plugin that can connect to a specific data source (e.g., file system, REST API, database)
- **Data Source**: A concrete instance of a connector with specific configuration

Example: The file system connector can create multiple data sources - one for "Documents", one for "Downloads", etc.

---

## Plugin Development

There are two ways to create custom connectors:

### Option 1: Script-based Plugins

Script plugins are compiled at runtime and don't require a separate build process. Ideal for quick prototypes and simple connectors.

#### Creating a Script

1. Open **Plugins ? Script Manager**
2. Enter a name and click **+ New Script**
3. Edit the generated template
4. Click **Compile**

#### Script Template

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MSCC.Connectors;
using MSCC.Models;
using MSCC.Scripting;

namespace MSCC.Scripts
{
    public class MyConnector : ScriptedConnectorBase
    {
        // Unique ID for this connector
        public override string Id => "my-connector";
        
        // Display name in the UI
        public override string Name => "My Connector";
        
        // Description
        public override string Description => "Description of my connector";
        
        // Version
        public override string Version => "1.0.0";

        // Define configuration parameters
        public override IEnumerable<ConnectorParameter> ConfigurationParameters => new[]
        {
            new ConnectorParameter
            {
                Name = "ApiUrl",
                DisplayName = "API URL",
                Description = "The URL of the API",
                ParameterType = "string",
                IsRequired = true
            },
            new ConnectorParameter
            {
                Name = "ApiKey",
                DisplayName = "API Key",
                Description = "Optional API key",
                ParameterType = "string",
                IsRequired = false
            }
        };

        private string _apiUrl = string.Empty;
        private string _apiKey = string.Empty;

        // Initialization with configuration
        public override Task<bool> InitializeAsync(Dictionary<string, string> configuration)
        {
            if (!configuration.TryGetValue("ApiUrl", out var url) || string.IsNullOrEmpty(url))
                return Task.FromResult(false);

            _apiUrl = url;
            configuration.TryGetValue("ApiKey", out var key);
            _apiKey = key ?? string.Empty;

            return Task.FromResult(true);
        }

        // Connection test
        public override Task<bool> TestConnectionAsync()
        {
            return Task.FromResult(!string.IsNullOrEmpty(_apiUrl));
        }

        // Implement search logic
        public override async Task<IEnumerable<SearchResult>> SearchAsync(
            string searchTerm,
            int maxResults = 100,
            CancellationToken cancellationToken = default)
        {
            var results = new List<SearchResult>();

            // Implement your search logic here
            // Example: Call API, query database, etc.

            // Add result
            results.Add(new SearchResult
            {
                Title = "Example Result",
                Description = $"Found for: {searchTerm}",
                SourceName = Name,
                ConnectorId = Id,
                OriginalReference = "ref-123",
                RelevanceScore = 100,
                Metadata = new Dictionary<string, object>
                {
                    ["Property1"] = "Value1",
                    ["Property2"] = 42
                }
            });

            return results;
        }
    }
}
```

#### Script Storage Location

Scripts are saved under:
```
%APPDATA%\MSCC\Scripts\
```

Each script consists of two files:
- `ScriptName_<id>.cs` - The source code
- `ScriptName_<id>.cs.meta` - Metadata (name, version, etc.)

---

### Option 2: Compiled Plugins (Visual Studio)

For more complex connectors, development as a compiled plugin is recommended.

#### Creating a Project

1. Create a new **Class Library** project in Visual Studio
2. Target Framework: **.NET 10**
3. Add a project reference to `MSCC.csproj`

#### Implementing IDataSourceConnector

```csharp
using System.Windows;
using MSCC.Connectors;
using MSCC.Models;

namespace MyPlugin
{
    public class MyConnector : IDataSourceConnector
    {
        public string Id => "my-compiled-connector";
        public string Name => "My Compiled Connector";
        public string Description => "A compiled connector";
        public string Version => "1.0.0";

        public IEnumerable<ConnectorParameter> ConfigurationParameters => new[]
        {
            new ConnectorParameter
            {
                Name = "ConnectionString",
                DisplayName = "Connection String",
                ParameterType = "string",
                IsRequired = true
            }
        };

        private string _connectionString = string.Empty;

        public Task<bool> InitializeAsync(Dictionary<string, string> configuration)
        {
            if (!configuration.TryGetValue("ConnectionString", out var cs))
                return Task.FromResult(false);
            
            _connectionString = cs;
            return Task.FromResult(true);
        }

        public Task<bool> TestConnectionAsync()
        {
            // Implement connection test
            return Task.FromResult(!string.IsNullOrEmpty(_connectionString));
        }

        public async Task<IEnumerable<SearchResult>> SearchAsync(
            string searchTerm,
            int maxResults = 100,
            CancellationToken cancellationToken = default)
        {
            var results = new List<SearchResult>();
            
            // Implement search logic here
            
            return results;
        }

        public DetailViewConfiguration GetDetailViewConfiguration(SearchResult result)
        {
            return new DetailViewConfiguration
            {
                ViewType = DetailViewType.Default,
                Actions = new List<ResultAction>
                {
                    new() { Id = "open", Name = "Open", Icon = "??" }
                }
            };
        }

        public FrameworkElement? CreateCustomDetailView(SearchResult result)
        {
            return null; // Or return custom WPF view
        }

        public Task<bool> ExecuteActionAsync(SearchResult result, string actionId)
        {
            // Handle actions
            return Task.FromResult(true);
        }

        public void Dispose()
        {
            // Release resources
        }
    }
}
```

#### Registering the Connector

Add the connector in `DataSourceManager.cs`:

```csharp
public void RegisterDefaultConnectors()
{
    RegisterConnector(new FileSystemConnector());
    RegisterConnector(new MockDatabaseConnector());
    RegisterConnector(new MyConnector()); // Add here
}
```

And in `CreateConnectorInstance`:

```csharp
private static IDataSourceConnector? CreateConnectorInstance(IDataSourceConnector template)
{
    if (template is FileSystemConnector)
        return new FileSystemConnector();
    
    if (template is MockDatabaseConnector)
        return new MockDatabaseConnector();
    
    if (template is MyConnector)
        return new MyConnector();
    
    // ... rest
}
```

---

## API Reference

### IDataSourceConnector

The main interface for all connectors.

| Method/Property | Description |
|-----------------|-------------|
| `Id` | Unique identifier of the connector |
| `Name` | Display name |
| `Description` | Description |
| `Version` | Version number |
| `ConfigurationParameters` | List of configuration parameters |
| `InitializeAsync()` | Initializes the connector with configuration |
| `TestConnectionAsync()` | Tests the connection |
| `SearchAsync()` | Performs a search |
| `GetDetailViewConfiguration()` | Defines the detail view |
| `CreateCustomDetailView()` | Creates a custom WPF view |
| `ExecuteActionAsync()` | Executes an action on a result |

### SearchResult

Represents a search result.

| Property | Type | Description |
|----------|------|-------------|
| `Title` | string | Title of the result |
| `Description` | string | Description |
| `SourceName` | string | Name of the data source |
| `ConnectorId` | string | ID of the connector |
| `OriginalReference` | string | Reference to original (e.g., file path, URL) |
| `RelevanceScore` | int | Relevance (0-100) |
| `Metadata` | Dictionary | Additional properties |

### ConnectorParameter

Defines a configuration parameter.

| Property | Description |
|----------|-------------|
| `Name` | Technical name |
| `DisplayName` | Display name |
| `Description` | Description/help text |
| `ParameterType` | Type: "string", "bool", "int", "path" |
| `IsRequired` | Required field? |
| `DefaultValue` | Default value |

### DetailViewConfiguration

Configures the detail view for search results.

| Property | Description |
|----------|-------------|
| `ViewType` | Type of view: Default, Table, Media, Chart, Custom |
| `TableColumns` | Column definitions for table view |
| `Actions` | Available actions |
| `ChartConfig` | Configuration for charts |

---

## Examples

### Web API Connector

An example of a connector that queries a REST API:

```csharp
public override async Task<IEnumerable<SearchResult>> SearchAsync(
    string searchTerm,
    int maxResults = 100,
    CancellationToken cancellationToken = default)
{
    var results = new List<SearchResult>();
    
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    
    var response = await client.GetAsync(
        $"{_apiUrl}/search?q={Uri.EscapeDataString(searchTerm)}&limit={maxResults}",
        cancellationToken);
    
    if (!response.IsSuccessStatusCode)
        return results;
    
    var json = await response.Content.ReadAsStringAsync(cancellationToken);
    var data = JsonSerializer.Deserialize<ApiResponse>(json);
    
    foreach (var item in data?.Items ?? [])
    {
        results.Add(new SearchResult
        {
            Title = item.Title,
            Description = item.Summary,
            SourceName = Name,
            ConnectorId = Id,
            OriginalReference = item.Url,
            RelevanceScore = (int)(item.Score * 100),
            Metadata = new Dictionary<string, object>
            {
                ["Author"] = item.Author,
                ["Date"] = item.CreatedAt
            }
        });
    }
    
    return results;
}
```

### Database Connector

```csharp
public override async Task<IEnumerable<SearchResult>> SearchAsync(
    string searchTerm,
    int maxResults = 100,
    CancellationToken cancellationToken = default)
{
    var results = new List<SearchResult>();
    
    using var connection = new SqlConnection(_connectionString);
    await connection.OpenAsync(cancellationToken);
    
    var command = new SqlCommand(
        "SELECT TOP (@max) Id, Title, Description FROM Documents WHERE Title LIKE @search",
        connection);
    command.Parameters.AddWithValue("@max", maxResults);
    command.Parameters.AddWithValue("@search", $"%{searchTerm}%");
    
    using var reader = await command.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
    {
        results.Add(new SearchResult
        {
            Title = reader.GetString(1),
            Description = reader.GetString(2),
            SourceName = Name,
            ConnectorId = Id,
            OriginalReference = reader.GetInt32(0).ToString(),
            RelevanceScore = 100
        });
    }
    
    return results;
}
```

---

## Running Tests

```bash
cd MSCC.Tests
dotnet test
```

Current test coverage: **163 tests**

---

## License

Apache 2.0 License

---

## Contributing

Pull requests are welcome! Please create an issue first to discuss larger changes.
