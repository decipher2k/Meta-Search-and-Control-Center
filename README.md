# MSCC - Meta Search Command Center

An extensible meta search engine for Windows that can search multiple data sources simultaneously. The application supports both built-in connectors and custom plugins.

## Table of Contents

- [Feature Overview](#feature-overview)
- [Installation](#installation)
- [User Interface](#user-interface)
- [Connectors and Data Sources](#connectors-and-data-sources)
  - [Microsoft 365 Connector](#microsoft-365-connector)
  - [SQL Database Connector](#sql-database-connector)
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


<img width="1919" height="1004" alt="Unbenannt" src="https://github.com/user-attachments/assets/c585fcac-0758-42d8-a606-a16c5c7a57c7" />



### Built-in Connectors

| Connector | Description |
|-----------|-------------|
| **File System** | Searches local files and folders by filename |
| **Microsoft 365** | Searches Calendar, ToDo, Emails, and OneNote via Microsoft Graph API |
| **DuckDuckGo** | Performs web searches using DuckDuckGo |
| **SQL Database** | Searches SQL databases (MySQL, MSSQL, PostgreSQL) |
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

### Microsoft 365 Connector

The Microsoft 365 Connector allows you to search across your Microsoft 365 data including Calendar events, ToDo tasks, Emails, and OneNote pages using the Microsoft Graph API.

#### Prerequisites

Before using this connector, you need to register an application in Azure Active Directory:

1. **Go to Azure Portal**: Navigate to [https://portal.azure.com](https://portal.azure.com)

2. **Register a new application**:
   - Go to **Azure Active Directory** ? **App registrations** ? **New registration**
   - Name: e.g., "MSCC Meta Search"
   - Supported account types: Choose based on your needs:
     - **Single tenant**: Only your organization
     - **Multitenant**: Any Azure AD directory
     - **Personal Microsoft accounts**: For personal Outlook.com, OneDrive, etc.
   - Redirect URI: Select **Public client/native** and enter `http://localhost`
   - Click **Register**

3. **Note the Application (Client) ID**: Copy this value - you'll need it for configuration

4. **Note the Tenant ID** (optional): 
   - Found on the Overview page
   - Or use `common` for multi-tenant, `consumers` for personal accounts only

5. **Configure API Permissions**:
   - Go to **API permissions** ? **Add a permission** ? **Microsoft Graph** ? **Delegated permissions**
   - Add the following permissions:
     - `User.Read` (required)
     - `Calendars.Read` (for calendar search)
     - `Tasks.Read` (for ToDo search)
     - `Mail.Read` (for email search)
     - `Notes.Read` (for OneNote search)
   - Click **Grant admin consent** if you have admin privileges (optional but recommended)

6. **Enable public client flows** (required for interactive authentication):
   - Go to **Authentication**
   - Under **Advanced settings**, set **Allow public client flows** to **Yes**
   - Click **Save**

#### Configuration

When creating a Microsoft 365 data source in MSCC, configure the following parameters:

| Parameter | Required | Description |
|-----------|----------|-------------|
| **Client ID (App-ID)** | Yes | The Application (Client) ID from your Azure app registration |
| **Tenant ID** | Yes | Your Azure AD Tenant ID, or use special values: `common` (any Azure AD + personal), `consumers` (personal accounts only), `organizations` (any Azure AD only) |
| **Search Calendar** | No | Include calendar events in search (default: true) |
| **Search ToDo** | No | Include ToDo tasks in search (default: true) |
| **Search Mail** | No | Include emails in search (default: true) |
| **Search OneNote** | No | Include OneNote pages in search (default: true) |
| **Max Days Back** | No | How many days back to search for calendar/mail (default: 30) |

#### First-Time Authentication

When you first use the connector (either when testing the connection or performing a search):

1. A browser window will open automatically
2. Sign in with your Microsoft account
3. Review and accept the requested permissions
4. The browser will redirect to localhost (you can close it)
5. MSCC will now have access to search your Microsoft 365 data

**Note**: The authentication token is cached, so you won't need to sign in again unless the token expires or you revoke access.

#### Search Capabilities

| Data Type | What's Searched | Result Details |
|-----------|-----------------|----------------|
| **Calendar** | Event subject, body preview, location | Start/end time, location, organizer |
| **ToDo** | Task title, body content | List name, status, importance, due date |
| **Email** | Full-text search via Graph API | Sender, received date, attachments, importance |
| **OneNote** | Page title and content | Section name, created/modified dates |

#### Available Actions

Each search result supports the following actions:

| Action | Description |
|--------|-------------|
| **Open in Browser** | Opens the item in the corresponding web app (Outlook, ToDo, OneNote) |
| **Copy Link** | Copies the web link to the clipboard |

#### Example Usage

1. Click **+** next to "Data Sources"
2. Select **Microsoft 365** as the connector
3. Enter a name (e.g., "My Microsoft 365")
4. Enter your **Client ID** from Azure
5. Enter your **Tenant ID** (or use `common`)
6. Enable/disable the services you want to search
7. Click **Save**
8. Sign in when prompted
9. Enable the data source and start searching!

#### Troubleshooting

| Issue | Solution |
|-------|----------|
| "AADSTS50011: Reply URL mismatch" | Ensure `http://localhost` is added as a redirect URI in Azure |
| "AADSTS65001: User needs to consent" | The user hasn't consented to permissions. Re-authenticate or grant admin consent |
| "AADSTS7000218: Request body must contain client_assertion" | Enable "Allow public client flows" in Azure app settings |
| No results returned | Check that the specific service (Calendar/ToDo/Mail/OneNote) is enabled in configuration |
| OneNote search fails | Ensure `Notes.Read` permission is granted; some organizational policies may restrict access |

#### Security Considerations

- The connector uses **delegated permissions**, meaning it can only access data the signed-in user has access to
- No credentials are stored - authentication is handled via OAuth 2.0 with PKCE
- Tokens are cached locally and can be revoked from [https://myaccount.microsoft.com](https://myaccount.microsoft.com)
- For organizational use, consider having an admin grant consent to avoid per-user consent prompts

---

### SQL Database Connector

The SQL Database Connector allows you to search across relational databases including Microsoft SQL Server, MySQL, and PostgreSQL. It can search all text fields in specified tables or execute custom SQL queries.

#### Supported Databases

| Database | Required Package |
|----------|------------------|
| **Microsoft SQL Server** | `Microsoft.Data.SqlClient` or `System.Data.SqlClient` |
| **MySQL** | `MySql.Data` or `MySqlConnector` |
| **PostgreSQL** | `Npgsql` |

> **Note**: Install the appropriate NuGet package for your database before using this connector.

#### Configuration

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| **Connection String** | Yes | - | Database connection string |
| **Database Type** | Yes | MSSQL | Type: `MSSQL`, `MySQL`, or `PostgreSQL` |
| **Tables** | No | * | Comma-separated table names, or `*` for all tables |
| **Custom SQL Query** | No | - | Custom SELECT statement with `@SearchTerm` placeholder |

#### Connection String Examples

**SQL Server:**
```
Server=localhost;Database=MyDB;User Id=sa;Password=secret;TrustServerCertificate=True;
```

**MySQL:**
```
Server=localhost;Database=MyDB;User=root;Password=secret;
```

**PostgreSQL:**
```
Host=localhost;Database=MyDB;Username=postgres;Password=secret;
```

#### Search Modes

**1. Table Search Mode (Default)**

When using table search mode, the connector will:
- Get all specified tables (or all tables if `*` is specified)
- For each table, search all text-like columns (VARCHAR, NVARCHAR, TEXT, etc.)
- Return matching rows with relevance scoring

Example configuration:
- Tables: `Customers, Orders, Products`
- Or Tables: `*` (search all tables)

**2. Custom Query Mode**

Use a custom SQL query for more control. Use `@SearchTerm` as a placeholder for the search term:

```sql
SELECT * FROM Products 
WHERE Name LIKE '%' + @SearchTerm + '%' 
   OR Description LIKE '%' + @SearchTerm + '%'
```

For LIKE queries with wildcards, use `@SearchTermWildcard` which includes `%` wildcards:

```sql
SELECT * FROM Products WHERE Name LIKE @SearchTermWildcard
```

#### Search Results

Each search result includes:

| Field | Description |
|-------|-------------|
| **Title** | First non-null text value from the record |
| **Description** | Preview of column values |
| **Table Name** | Source table of the record |
| **Matching Columns** | Columns where the search term was found |
| **All Fields** | Complete record data in metadata |

#### Available Actions

| Action | Description |
|--------|-------------|
| **Copy as JSON** | Copies the record as formatted JSON |
| **Copy as INSERT** | Generates and copies a SQL INSERT statement |

#### Example Usage

1. Install required NuGet package for your database:
   ```bash
   dotnet add package Microsoft.Data.SqlClient
   # or
   dotnet add package MySql.Data
   # or
   dotnet add package Npgsql
   ```

2. Click **+** next to "Data Sources"
3. Select **SQL Database** as the connector
4. Enter a name (e.g., "Production Database")
5. Configure:
   - **Connection String**: Your database connection string
   - **Database Type**: MSSQL, MySQL, or PostgreSQL
   - **Tables**: `*` or specific tables like `Users, Products`
6. Click **Save**
7. Enable the data source and start searching!

#### Security Considerations

- Connection strings may contain sensitive credentials
- Consider using Windows Authentication for SQL Server when possible
- Use read-only database accounts for search operations
- Connection strings are stored locally in the application settings

#### Troubleshooting

| Issue | Solution |
|-------|----------|
| "Provider not found" | Install the appropriate NuGet package for your database |
| Connection timeout | Check firewall settings and ensure database is accessible |
| No results | Verify table names are correct and contain text columns |
| Access denied | Check database user permissions |

---

### DuckDuckGo Web Search Connector

The DuckDuckGo Connector allows you to perform web searches directly from MSCC using the privacy-focused DuckDuckGo search engine.

#### Features

- **Privacy-focused**: Uses DuckDuckGo which doesn't track your searches
- **No API key required**: Works out of the box without any registration
- **Configurable result count**: Set how many results you want (1-30)
- **Region settings**: Customize search results for your region
- **SafeSearch support**: Filter out adult content

#### Configuration

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| **Max Results** | No | 10 | Number of search results to return (1-30) |
| **Region** | No | wt-wt | Region code for localized results |
| **SafeSearch** | No | true | Enable family-friendly content filter |

#### Region Codes

| Code | Region |
|------|--------|
| `wt-wt` | Worldwide (no region bias) |
| `de-de` | Germany |
| `at-de` | Austria |
| `ch-de` | Switzerland (German) |
| `us-en` | United States |
| `uk-en` | United Kingdom |
| `fr-fr` | France |
| `es-es` | Spain |
| `it-it` | Italy |
| `nl-nl` | Netherlands |

For a complete list of region codes, see the [DuckDuckGo region documentation](https://duckduckgo.com/params).

#### Search Results

Each search result includes:

| Field | Description |
|-------|-------------|
| **Title** | The webpage title |
| **Description** | Snippet/summary from the page |
| **URL** | Direct link to the webpage |
| **Domain** | The website domain |
| **Position** | Ranking position in search results |

#### Available Actions

| Action | Description |
|--------|-------------|
| **Open in Browser** | Opens the webpage in your default browser |
| **Copy URL** | Copies the URL to clipboard |
| **Search on DuckDuckGo** | Opens full DuckDuckGo results page for this query |

#### Example Usage

1. Click **+** next to "Data Sources"
2. Select **DuckDuckGo Web Search** as the connector
3. Enter a name (e.g., "Web Search")
4. Configure:
   - **Max Results**: 15 (or your preferred number)
   - **Region**: de-de (for German results)
   - **SafeSearch**: true
5. Click **Save**
6. Enable the data source and start searching!

#### Technical Notes

- The connector uses DuckDuckGo's HTML Lite interface for reliable parsing
- No JavaScript execution is required
- Results are parsed from the HTML response
- Rate limiting may apply for very frequent searches

#### Limitations

- Maximum 30 results per search (DuckDuckGo limitation)
- No image or video search (text results only)
- Some advanced DuckDuckGo features (bangs, instant answers) are not available

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
