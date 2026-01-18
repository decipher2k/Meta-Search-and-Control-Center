# MSCC - Meta Search Command Center

An extensible meta search engine for Windows that can search multiple data sources simultaneously. The application supports both built-in connectors and custom plugins.

## Table of Contents

- [Feature Overview](#feature-overview)
- [Installation](#installation)
- [User Interface](#user-interface)
- [AI Search](#ai-search)
- [Connectors and Data Sources](#connectors-and-data-sources)
  - [File System Connector](#file-system-connector)
  - [Find In Files Connector](#find-in-files-connector)
  - [Microsoft 365 Connector](#microsoft-365-connector)
  - [SQL Database Connector](#sql-database-connector)
  - [DuckDuckGo Web Search Connector](#duckduckgo-web-search-connector)
  - [OpenAI Connector](#openai-connector)
  - [Generic API Connector](#generic-api-connector)
  - [IMAP Email Connector](#imap-email-connector)
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
| **Find In Files** | Searches for text content inside files with regex support |
| **Microsoft 365** | Searches Calendar, ToDo, Emails, and OneNote via Microsoft Graph API |
| **DuckDuckGo** | Performs web searches using DuckDuckGo |
| **SQL Database** | Searches SQL databases (MySQL, MSSQL, PostgreSQL) |
| **OpenAI API** | Queries OpenAI-compatible AI APIs (ChatGPT, local LLMs) |
| **Generic API** | Connects to any REST API with flexible authentication |
| **IMAP Email** | Searches emails via IMAP (Gmail, Outlook, Yahoo, private servers) |
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

## AI Search

The AI Search feature allows you to analyze your search results using an AI language model. Instead of manually reviewing each result, you can have an AI summarize, categorize, or extract insights from your search results.

### Features

- **Automatic Search Execution**: If you have a search term but haven't searched yet, clicking "AI Search" will automatically perform the search first
- **Customizable System Prompt**: Define how the AI should analyze your results
- **HTML-Formatted Output**: Results are displayed in a rich HTML view with proper formatting
- **WebView2 Rendering**: Modern web-based display with support for tables, lists, and styled content
- **Token Usage Tracking**: See how many tokens were used for the request and response

### Prerequisites

Before using AI Search, you need to configure an AI API in the application settings:

1. Go to **File ? Settings**
2. Navigate to the **AI Search** section
3. Configure the following:

| Setting | Description | Example |
|---------|-------------|---------|
| **API Endpoint** | The URL of your AI API | `https://api.openai.com/v1/chat/completions` |
| **API Key** | Your authentication token | `sk-...` |
| **Model** | The AI model to use | `gpt-4`, `gpt-3.5-turbo`, `llama2` |

#### Supported AI Providers

| Provider | Endpoint | Notes |
|----------|----------|-------|
| **OpenAI** | `https://api.openai.com/v1/chat/completions` | Requires API key from [platform.openai.com](https://platform.openai.com) |
| **Azure OpenAI** | `https://{resource}.openai.azure.com/openai/deployments/{deployment}/chat/completions?api-version=2024-02-01` | Use your Azure deployment |
| **Ollama** | `http://localhost:11434/v1/chat/completions` | Free, local, no API key needed |
| **LM Studio** | `http://localhost:1234/v1/chat/completions` | Free, local, no API key needed |
| **Any OpenAI-compatible API** | Varies | Must support the OpenAI chat completions format |

### How to Use

1. **Enter a search term** in the search box
2. **Click "AI Search"** (purple button next to the regular Search button)
   - If no search has been performed yet, it will automatically search first
3. **Review the System Prompt** in the dialog that appears
   - The default prompt instructs the AI to output HTML-formatted results
   - You can customize this prompt to change how the AI analyzes the results
4. **Click "Analyze with AI"**
5. **View the results** in the AI Analysis Result window
   - Results are displayed with rich HTML formatting
   - Use "Copy Response" to copy the raw response to clipboard

### Default System Prompt

The default system prompt is designed to produce well-formatted HTML output:

```
You are a helpful assistant. Analyze the following search results and provide a well-structured summary.

IMPORTANT: Your response MUST be valid HTML. Use these HTML elements for formatting:
- <h2> for main section headers
- <h3> for sub-section headers
- <p> for paragraphs
- <ul> and <li> for bullet lists
- <ol> and <li> for numbered lists
- <strong> for bold/important text
- <em> for italic/emphasized text
- <code> for code or technical terms
- <blockquote> for quotes or important notes
- <table>, <tr>, <th>, <td> for tabular data

Structure your response with clear sections. Highlight the most relevant information.
Do NOT include <html>, <head>, or <body> tags - only the inner content.
```

### Customizing the Prompt

You can modify the system prompt to change how the AI analyzes your results. Here are some examples:

#### Summarization Prompt
```
Analyze the search results and create a brief executive summary (max 3 paragraphs).
Output as HTML with <h2> for the title and <p> for paragraphs.
Focus only on the most important findings.
```

#### Comparison Prompt
```
Compare and contrast the search results. Create an HTML table showing:
- Key similarities
- Key differences
- Unique aspects of each result
Use <table>, <tr>, <th>, <td> tags for the comparison table.
```

#### Action Items Prompt
```
Review the search results and extract actionable items.
Format as an HTML ordered list (<ol>, <li>) with priority levels.
Use <strong> to highlight high-priority items.
```

#### Technical Analysis Prompt
```
Analyze these search results from a technical perspective.
Create an HTML report with:
- <h2> for main sections
- <code> for technical terms
- <ul> for feature lists
- <table> for specifications comparison
```

### Result Window

The AI Analysis Result window displays:

| Element | Description |
|---------|-------------|
| **Title** | "AI Analysis Result" |
| **Model** | The AI model that generated the response |
| **Response** | The formatted HTML response from the AI |
| **Token Info** | Prompt tokens, completion tokens, and total tokens used |
| **Copy Response** | Button to copy the raw response to clipboard |

### Tips for Best Results

1. **Be specific in your search**: The more relevant your search results, the better the AI analysis
2. **Customize the prompt**: Tailor the system prompt to get the type of analysis you need
3. **Use appropriate models**: GPT-4 produces better analysis than GPT-3.5-turbo but costs more
4. **Keep result count reasonable**: Too many results may exceed token limits or produce less focused analysis
5. **Review token usage**: Monitor token consumption to manage API costs

### Troubleshooting

| Issue | Solution |
|-------|----------|
| "No search results to analyze" | Perform a search first, or enter a search term before clicking AI Search |
| "API Error" | Check your API endpoint, API key, and model settings |
| Empty response | Verify the AI service is running and accessible |
| Timeout | The AI may be taking too long; try with fewer search results |
| Malformed output | The AI may not have followed HTML instructions; try adjusting the prompt |
| Rate limit exceeded | Wait and try again, or upgrade your API plan |

### Cost Considerations

- **OpenAI**: Charges per token (input + output). GPT-4 is ~30x more expensive than GPT-3.5-turbo
- **Azure OpenAI**: Similar pricing to OpenAI
- **Ollama/LM Studio**: Free (runs locally on your hardware)

Monitor your usage at:
- OpenAI: [platform.openai.com/usage](https://platform.openai.com/usage)
- Azure: Azure Portal cost management

---

## Connectors and Data Sources

### Concept

- **Connector**: A plugin that can connect to a specific data source (e.g., file system, REST API, database)
- **Data Source**: A concrete instance of a connector with specific configuration

Example: The file system connector can create multiple data sources - one for "Documents", one for "Downloads", etc.

---

### File System Connector

The File System Connector allows you to search for files and folders by their names in your local file system.

#### Features

- **Fast filename search**: Quickly find files by name pattern
- **Wildcard support**: Use patterns like `*.txt`, `*.pdf`, `report*`
- **Recursive search**: Optionally include subdirectories
- **File metadata**: View size, dates, and file type information

#### Configuration

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| **Base Path** | Yes | - | The folder path to search in |
| **Search Pattern** | No | `*.*` | File pattern filter (e.g., `*.txt`, `*.pdf`) |
| **Include Subdirectories** | No | true | Whether to search in subdirectories |

#### Search Results

Each search result includes:

| Field | Description |
|-------|-------------|
| **Title** | The file or folder name |
| **Description** | Full path to the file |
| **Path** | Complete file path |
| **Size** | File size in bytes |
| **Created** | Creation date |
| **Modified** | Last modification date |
| **Type** | File extension or "Folder" |

#### Available Actions

| Action | Description |
|--------|-------------|
| **Open** | Opens the file with the default application |
| **Open Folder** | Opens the containing folder in Explorer |
| **Copy Path** | Copies the full file path to clipboard |

#### Example Usage

1. Click **+** next to "Data Sources"
2. Select **File System** as the connector
3. Enter a name (e.g., "Documents")
4. Configure:
   - **Base Path**: `C:\Users\YourName\Documents`
   - **Search Pattern**: `*.*` (all files) or `*.pdf` (only PDFs)
   - **Include Subdirectories**: true
5. Click **Save**
6. Enable the data source and start searching!

---

### Find In Files Connector

The Find In Files Connector allows you to search for text content inside files. Unlike the File System Connector which searches by filename, this connector reads file contents and finds matching text, similar to "grep" on Linux or "Find in Files" in text editors.

#### Features

- **Full-text search**: Search inside file contents, not just filenames
- **Regular expression support**: Use regex patterns for advanced searches
- **Case sensitivity option**: Choose between case-sensitive or case-insensitive search
- **Multiple file types**: Search in text files, source code, logs, and more
- **Match context**: See the line and column where matches were found
- **Match highlighting**: View all matches with their surrounding context

#### Configuration

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| **Base Path** | Yes | - | The folder path to search in |
| **File Pattern** | No | `*.*` | File pattern filter (e.g., `*.txt`, `*.cs`, `*.log`) |
| **Include Subdirectories** | No | true | Whether to search in subdirectories |
| **Use Regular Expressions** | No | false | Enable regex pattern matching |
| **Case Sensitive** | No | false | Enable case-sensitive matching |

#### Search Modes

**1. Plain Text Search (Default)**

When "Use Regular Expressions" is disabled, the connector performs a simple text search:
- Searches for the exact text you enter
- Case sensitivity depends on the "Case Sensitive" setting
- Fast and straightforward

**Example**: Searching for `TODO` will find all occurrences of "TODO" in files.

**2. Regular Expression Search**

When "Use Regular Expressions" is enabled, you can use regex patterns:

| Pattern | Description | Example Match |
|---------|-------------|---------------|
| `\bword\b` | Whole word only | "word" but not "keyword" |
| `error\|warning` | Either pattern | "error" or "warning" |
| `\d{4}-\d{2}-\d{2}` | Date pattern | "2024-01-15" |
| `TODO:.*` | TODO with text | "TODO: fix this bug" |
| `^import\s+` | Line starting with import | "import System;" |
| `\.(jpg\|png\|gif)$` | Image extensions | ".jpg", ".png" |

#### Search Results

Each search result represents a file with matches:

| Field | Description |
|-------|-------------|
| **Title** | Filename with match count |
| **Description** | First few matches with line numbers |
| **Path** | Full file path |
| **Directory** | Parent directory |
| **File Name** | Just the filename |
| **Size** | File size |
| **Modified** | Last modification date |
| **Match Count** | Total number of matches in the file |
| **Matches** | Detailed list of all matches with line/column |

#### Match Details

For each match found, the connector provides:

| Field | Description |
|-------|-------------|
| **Line** | Line number (1-based) |
| **Column** | Column position (1-based) |
| **Text** | The matched text |
| **Context** | The full line containing the match |

#### Available Actions

| Action | Description |
|--------|-------------|
| **Open** | Opens the file with the default application |
| **Open Folder** | Opens the containing folder in Explorer |
| **Copy Path** | Copies the full file path to clipboard |
| **Copy Matches** | Copies all matches with line numbers to clipboard |

#### Example Usage

##### Example 1: Find TODO Comments in Source Code

1. Click **+** next to "Data Sources"
2. Select **Find In Files** as the connector
3. Enter a name (e.g., "TODO Finder")
4. Configure:
   - **Base Path**: `C:\Projects\MyProject`
   - **File Pattern**: `*.cs` (C# files only)
   - **Include Subdirectories**: true
   - **Use Regular Expressions**: false
   - **Case Sensitive**: false
5. Click **Save**
6. Search for: `TODO`

##### Example 2: Find Error Messages in Log Files

1. Create a new data source named "Log Search"
2. Configure:
   - **Base Path**: `C:\Logs`
   - **File Pattern**: `*.log`
   - **Include Subdirectories**: true
   - **Use Regular Expressions**: true
   - **Case Sensitive**: false
3. Search for: `error|exception|failed`

##### Example 3: Find IP Addresses

1. Enable regex mode
2. Search for: `\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b`

##### Example 4: Find Email Addresses

1. Enable regex mode
2. Search for: `[\w.+-]+@[\w-]+\.[\w.-]+`

#### Supported File Types

The connector can search in any text-based file format:

| Category | Extensions |
|----------|------------|
| **Source Code** | `.cs`, `.js`, `.ts`, `.py`, `.java`, `.cpp`, `.c`, `.h` |
| **Web Files** | `.html`, `.css`, `.xml`, `.json`, `.yaml`, `.yml` |
| **Documents** | `.txt`, `.md`, `.rst`, `.csv` |
| **Config Files** | `.config`, `.ini`, `.env`, `.properties` |
| **Log Files** | `.log`, `.trace` |
| **Scripts** | `.ps1`, `.sh`, `.bat`, `.cmd` |

> **Note**: Binary files (images, executables, etc.) are automatically skipped.

#### Performance Tips

1. **Narrow your file pattern**: Use specific patterns like `*.cs` instead of `*.*`
2. **Limit the base path**: Search in specific folders rather than entire drives
3. **Disable subdirectories**: If you only need to search the top-level folder
4. **Use simpler regex**: Complex regex patterns are slower than plain text
5. **Consider file size**: Very large files take longer to search

#### Troubleshooting

| Issue | Solution |
|-------|----------|
| No results found | Check that files exist and match the file pattern |
| Search is slow | Narrow the base path or file pattern |
| Regex not working | Verify your regex syntax; test with simple patterns first |
| Encoding issues | The connector uses automatic encoding detection |
| Access denied | Check folder permissions |
| Out of memory | Reduce the number of files by using a more specific file pattern |

#### Comparison: File System vs Find In Files

| Feature | File System | Find In Files |
|---------|-------------|---------------|
| Search by filename | ? | ? |
| Search file contents | ? | ? |
| Regex support | ? | ? |
| Case sensitivity option | ? | ? |
| Match highlighting | ? | ? |
| Speed | Faster | Slower (reads file contents) |
| Use case | Find files by name | Find text in files |

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

### OpenAI Connector

The OpenAI Connector allows you to query OpenAI-compatible AI APIs directly from MSCC. This includes OpenAI's ChatGPT, Azure OpenAI, and local LLM servers that implement the OpenAI API format (like Ollama, LM Studio, or llama.cpp server).

#### Features

- **ChatGPT Integration**: Query OpenAI's GPT models directly
- **Local LLM Support**: Works with any OpenAI-compatible API endpoint
- **Customizable System Prompt**: Define the AI's behavior and context
- **Token Control**: Set maximum response length
- **Temperature Settings**: Control response creativity

#### Prerequisites

For OpenAI API:
1. Create an account at [https://platform.openai.com](https://platform.openai.com)
2. Generate an API key in your account settings
3. Ensure you have API credits available

For Local LLMs:
1. Install and run a local LLM server (e.g., Ollama, LM Studio)
2. Note the API endpoint URL (typically `http://localhost:11434/v1/chat/completions` for Ollama)

#### Configuration

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| **API Endpoint** | Yes | `https://api.openai.com/v1/chat/completions` | The API endpoint URL |
| **API Key** | Yes | - | Your API authentication token |
| **Model** | Yes | `gpt-3.5-turbo` | The AI model to use (e.g., `gpt-4`, `gpt-3.5-turbo`, `llama2`) |
| **System Prompt** | No | `You are a helpful assistant...` | Instructions that define the AI's behavior |
| **Max Tokens** | No | 1000 | Maximum tokens in the response (1-128000) |
| **Temperature** | No | 0.7 | Response creativity (0.0 = deterministic, 2.0 = very creative) |

#### Endpoint Examples

| Service | Endpoint URL |
|---------|-------------|
| **OpenAI** | `https://api.openai.com/v1/chat/completions` |
| **Azure OpenAI** | `https://{resource}.openai.azure.com/openai/deployments/{deployment}/chat/completions?api-version=2024-02-01` |
| **Ollama** | `http://localhost:11434/v1/chat/completions` |
| **LM Studio** | `http://localhost:1234/v1/chat/completions` |
| **llama.cpp** | `http://localhost:8080/v1/chat/completions` |

#### Model Examples

| Provider | Model Names |
|----------|-------------|
| **OpenAI** | `gpt-4`, `gpt-4-turbo`, `gpt-3.5-turbo`, `gpt-4o`, `gpt-4o-mini` |
| **Ollama** | `llama2`, `llama3`, `mistral`, `codellama`, `mixtral` |
| **Azure OpenAI** | Your deployment name |

#### Search Results

Each query returns a single result containing:

| Field | Description |
|-------|-------------|
| **Title** | First 100 characters of the AI response |
| **Description** | Full AI response text |
| **Model** | The model that generated the response |
| **Prompt Tokens** | Number of tokens in your query |
| **Completion Tokens** | Number of tokens in the response |
| **Total Tokens** | Total tokens used |
| **Finish Reason** | Why the response ended (e.g., `stop`, `length`) |

#### Available Actions

| Action | Description |
|--------|-------------|
| **Copy Response** | Copies the full AI response to clipboard |
| **Copy Query** | Copies your original query to clipboard |

#### Example Usage

1. Click **+** next to "Data Sources"
2. Select **OpenAI API** as the connector
3. Enter a name (e.g., "ChatGPT" or "Local Llama")
4. Configure:
   - **API Endpoint**: Your API URL
   - **API Key**: Your API key
   - **Model**: `gpt-3.5-turbo` or your preferred model
   - **System Prompt**: (optional) Customize AI behavior
5. Click **Save**
6. Enable the data source and start querying!

#### Example System Prompts

**Code Assistant:**
```
You are a helpful programming assistant. Provide concise code examples and explanations. Use markdown formatting for code blocks.
```

**Research Assistant:**
```
You are a research assistant. Provide factual, well-structured answers with sources when possible. Be concise but thorough.
```

**Translation Assistant:**
```
You are a translation assistant. Translate any text the user provides into English. Maintain the original meaning and tone.
```

#### Troubleshooting

| Issue | Solution |
|-------|----------|
| "401 Unauthorized" | Check your API key is correct and has not expired |
| "429 Too Many Requests" | You've exceeded rate limits; wait and try again |
| "Model not found" | Verify the model name is correct for your endpoint |
| Timeout errors | Increase timeout in advanced settings or reduce max tokens |
| Empty responses | Check that max tokens > 0 and the model supports your query |

#### Cost Considerations

- OpenAI charges per token (both input and output)
- GPT-4 is significantly more expensive than GPT-3.5-turbo
- Monitor usage at [https://platform.openai.com/usage](https://platform.openai.com/usage)
- Local LLMs (Ollama, LM Studio) are free but require local resources

---

### Generic API Connector

The Generic API Connector allows you to connect to any REST API with flexible authentication and search parameter configuration. This is ideal for integrating custom APIs, third-party services, or internal company APIs that don't have a dedicated connector.

#### Features

- **Multiple HTTP Methods**: GET, POST, PUT, PATCH, DELETE
- **Flexible Authentication**: None, Header-based, Bearer Token, OAuth2, JWT, Query Parameter, Post Parameter
- **Custom Headers**: Add any custom HTTP headers
- **JSON Path Navigation**: Extract results from nested JSON structures
- **Configurable Result Mapping**: Map JSON properties to title, description, and URL

#### Supported Authentication Types

| Type | Description | Required Parameters |
|------|-------------|---------------------|
| **None** | No authentication | - |
| **Header** | Custom header authentication | `AuthHeaderName`, `AuthHeaderValue` |
| **Bearer** | Bearer token authentication | `AuthToken` |
| **OAuth2** | OAuth 2.0 client credentials flow | `OAuth2TokenEndpoint`, `OAuth2ClientId`, `OAuth2ClientSecret` |
| **JWT** | JWT token authentication | `AuthToken` (your JWT token) |
| **Query** | API key in query string | `AuthToken` (format: `key=value`) |
| **Post** | API key in POST body | `AuthToken` (format: `key=value`) |

#### Configuration

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| **API Endpoint** | Yes | - | Base URL. Use `[SEARCH]` as placeholder for search term |
| **HTTP Method** | Yes | GET | HTTP method: GET, POST, PUT, PATCH, DELETE |
| **Auth Type** | Yes | None | Authentication method (see above) |
| **Auth Header Name** | No | Authorization | Header name for Header-based auth |
| **Auth Header Value** | No | - | Header value for Header-based auth |
| **Auth Token** | No | - | Token for Bearer/JWT auth, or `key=value` for Query/Post auth |
| **OAuth2 Token Endpoint** | No | - | URL to obtain OAuth2 access token |
| **OAuth2 Client ID** | No | - | OAuth2 client ID |
| **OAuth2 Client Secret** | No | - | OAuth2 client secret |
| **OAuth2 Scope** | No | - | OAuth2 scope (optional) |
| **Query Parameters** | No | `q=[SEARCH]` | URL query parameters. Use `[SEARCH]` for search term |
| **POST Body** | No | - | Request body for POST/PUT/PATCH. Use `[SEARCH]` for search term |
| **Content Type** | No | application/json | Content-Type header for request body |
| **Custom Headers** | No | - | Additional headers, one per line (format: `Header-Name: value`) |
| **Result JSON Path** | No | results | Path to results array in JSON (e.g., `data.results`) |
| **Result Title Property** | No | title | JSON property name for result title |
| **Result Description Property** | No | description | JSON property name for result description |
| **Result URL Property** | No | url | JSON property name for result URL |
| **Timeout (seconds)** | No | 30 | Request timeout (1-300 seconds) |

#### The [SEARCH] Placeholder

Use `[SEARCH]` anywhere in your configuration to insert the search term:

- In the **API Endpoint**: `https://api.example.com/search/[SEARCH]`
- In **Query Parameters**: `q=[SEARCH]&limit=10`
- In **POST Body**: `{"query": "[SEARCH]", "maxResults": 10}`

The placeholder is automatically URL-encoded when used in URLs and query parameters.

#### JSON Path Navigation

The `Result JSON Path` parameter supports:

- Simple paths: `results`, `data`, `items`
- Nested paths: `data.results`, `response.data.items`
- Array indexing: `results[0].items`

**Example API Response:**
```json
{
  "status": "ok",
  "data": {
    "results": [
      {"title": "Result 1", "desc": "Description 1"},
      {"title": "Result 2", "desc": "Description 2"}
    ]
  }
}
```

**Configuration:**
- Result JSON Path: `data.results`
- Result Title Property: `title`
- Result Description Property: `desc`

#### Configuration Examples

##### Example 1: Simple GET API with API Key

**API**: A weather API that uses query parameters

| Setting | Value |
|---------|-------|
| API Endpoint | `https://api.weather.com/v1/search` |
| HTTP Method | GET |
| Auth Type | Query |
| Auth Token | `apikey=your-api-key-here` |
| Query Parameters | `q=[SEARCH]&format=json` |
| Result JSON Path | `locations` |
| Result Title Property | `name` |
| Result Description Property | `country` |

##### Example 2: POST API with Bearer Token

**API**: A search API that requires POST requests

| Setting | Value |
|---------|-------|
| API Endpoint | `https://api.example.com/search` |
| HTTP Method | POST |
| Auth Type | Bearer |
| Auth Token | `your-bearer-token` |
| Content Type | application/json |
| POST Body | `{"query": "[SEARCH]", "limit": 20}` |
| Result JSON Path | `results` |
| Result Title Property | `name` |
| Result Description Property | `summary` |
| Result URL Property | `link` |

##### Example 3: OAuth2 Client Credentials

**API**: An enterprise API using OAuth2

| Setting | Value |
|---------|-------|
| API Endpoint | `https://api.enterprise.com/v2/search` |
| HTTP Method | GET |
| Auth Type | OAuth2 |
| OAuth2 Token Endpoint | `https://auth.enterprise.com/oauth/token` |
| OAuth2 Client ID | `your-client-id` |
| OAuth2 Client Secret | `your-client-secret` |
| OAuth2 Scope | `read:search` |
| Query Parameters | `q=[SEARCH]` |

##### Example 4: Custom Header Authentication

**API**: An API using a custom authentication header

| Setting | Value |
|---------|-------|
| API Endpoint | `https://api.service.com/search` |
| HTTP Method | GET |
| Auth Type | Header |
| Auth Header Name | `X-API-Key` |
| Auth Header Value | `your-api-key` |
| Custom Headers | `X-Request-Source: MSCC` |
| Query Parameters | `term=[SEARCH]&max=25` |

#### Available Actions

| Action | Description |
|--------|-------------|
| **Open URL** | Opens the result URL in your default browser |
| **Copy as JSON** | Copies the result data as formatted JSON |
| **Copy URL** | Copies the result URL to clipboard |

#### Example Usage

1. Click **+** next to "Data Sources"
2. Select **Generic API** as the connector
3. Enter a name (e.g., "Company API")
4. Configure all required parameters based on your API's documentation
5. Click **Save**
6. Test with a simple search to verify configuration
7. Enable the data source and start searching!

#### Troubleshooting

| Issue | Solution |
|-------|----------|
| "401 Unauthorized" | Check authentication configuration matches API requirements |
| "400 Bad Request" | Verify POST body format and Content-Type header |
| Empty results | Check Result JSON Path matches actual API response structure |
| Timeout | Increase timeout value or check API endpoint availability |
| Wrong data displayed | Verify Result Title/Description/URL properties match JSON field names |
| OAuth2 fails | Ensure token endpoint, client ID, and secret are all correct |

#### Tips

1. **Test with API tools first**: Use Postman or curl to understand the API before configuring
2. **Check API documentation**: Verify required headers, authentication, and response format
3. **Start simple**: Begin with minimal configuration and add complexity as needed
4. **Use browser dev tools**: Inspect network requests if the API is used by a web app
5. **Handle pagination**: If the API returns paginated results, set appropriate limit parameters

---

### IMAP Email Connector

The IMAP Connector allows you to search emails from any IMAP-compatible email server. It supports both password authentication and OAuth2, making it compatible with Gmail, Outlook.com, Yahoo Mail, and private email servers.

#### Features

- **Universal IMAP Support**: Works with any IMAP server (Gmail, Outlook, Yahoo, private servers)
- **Multiple Authentication Methods**: Password or OAuth2 authentication
- **Flexible Encryption**: SSL/TLS, STARTTLS, or no encryption
- **Folder Selection**: Search in any mail folder (INBOX, Sent, Archive, custom folders)
- **Date Filtering**: Limit search to recent emails
- **Full-Text Search**: Searches subject, body, and sender

#### Prerequisites

**For Password Authentication:**
- Your email address and password
- For Gmail: Enable "Less secure app access" or create an App Password
- For Outlook.com: Create an App Password if 2FA is enabled

**For OAuth2 Authentication:**
- A valid OAuth2 access token for your email provider
- The token must have IMAP access scope

#### Common IMAP Server Settings

| Provider | Server | Port | Encryption |
|----------|--------|------|------------|
| **Gmail** | `imap.gmail.com` | 993 | SslTls |
| **Outlook.com / Hotmail** | `outlook.office365.com` | 993 | SslTls |
| **Yahoo Mail** | `imap.mail.yahoo.com` | 993 | SslTls |
| **iCloud** | `imap.mail.me.com` | 993 | SslTls |
| **AOL** | `imap.aol.com` | 993 | SslTls |
| **Zoho Mail** | `imap.zoho.com` | 993 | SslTls |
| **GMX** | `imap.gmx.net` | 993 | SslTls |
| **Private Server** | Your server hostname | 993 or 143 | Varies |

#### Configuration

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| **IMAP Server** | Yes | `imap.gmail.com` | The IMAP server hostname |
| **Port** | Yes | 993 | IMAP port (993 for SSL/TLS, 143 for STARTTLS) |
| **Email Address** | Yes | - | Your email address for authentication |
| **Auth Type** | Yes | Password | Authentication method: `Password` or `OAuth2` |
| **Password** | No | - | Your password or app password (for Password auth) |
| **OAuth2 Access Token** | No | - | OAuth2 access token (for OAuth2 auth) |
| **Encryption** | Yes | SslTls | Encryption method: `SslTls`, `StartTls`, or `None` |
| **Folder Name** | No | INBOX | Mail folder to search (e.g., `INBOX`, `Sent`, `[Gmail]/All Mail`) |
| **Max Results** | No | 50 | Maximum number of emails to return (1-500) |
| **Max Days Back** | No | 30 | How many days back to search (1-365) |

#### Gmail Setup

To use this connector with Gmail, you need to enable IMAP and create an App Password:

1. **Enable IMAP in Gmail**:
   - Go to Gmail Settings ? See all settings ? Forwarding and POP/IMAP
   - Under "IMAP access", select "Enable IMAP"
   - Click "Save Changes"

2. **Create an App Password** (required if 2FA is enabled):
   - Go to [https://myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords)
   - Sign in to your Google account
   - Select "Mail" and "Windows Computer" (or other)
   - Click "Generate"
   - Copy the 16-character password

3. **Configure the connector**:
   - Server: `imap.gmail.com`
   - Port: `993`
   - Email Address: Your Gmail address
   - Auth Type: `Password`
   - Password: Your App Password (not your regular password)
   - Encryption: `SslTls`

#### Outlook.com / Hotmail Setup

1. **Enable IMAP** (usually enabled by default):
   - Go to Outlook.com ? Settings ? View all Outlook settings
   - Mail ? Sync email ? POP and IMAP
   - Ensure "Let devices and apps use IMAP" is set to Yes

2. **Create an App Password** (if 2FA is enabled):
   - Go to [https://account.microsoft.com/security](https://account.microsoft.com/security)
   - Select "Advanced security options"
   - Under "App passwords", click "Create a new app password"
   - Copy the generated password

3. **Configure the connector**:
   - Server: `outlook.office365.com`
   - Port: `993`
   - Email Address: Your Outlook.com address
   - Auth Type: `Password`
   - Password: Your App Password
   - Encryption: `SslTls`

#### Search Capabilities

The connector searches across:

| Field | Description |
|-------|-------------|
| **Subject** | Email subject line |
| **Body** | Email body content (text and HTML) |
| **Sender** | Sender's name and email address |

#### Search Results

Each search result includes:

| Field | Description |
|-------|-------------|
| **Title** | Email subject |
| **Description** | Sender name and body preview |
| **From** | Sender's display name |
| **From (Email)** | Sender's email address |
| **Date** | When the email was received |
| **Attachments** | Whether the email has attachments |
| **Folder** | The mail folder where the email is located |

#### Available Actions

| Action | Description |
|--------|-------------|
| **Copy Body** | Copies the full email body to clipboard |
| **Copy Sender** | Copies the sender's email address to clipboard |

#### Example Usage

1. Click **+** next to "Data Sources"
2. Select **IMAP Email** as the connector
3. Enter a name (e.g., "Gmail" or "Work Email")
4. Configure:
   - **IMAP Server**: `imap.gmail.com`
   - **Port**: `993`
   - **Email Address**: `your.email@gmail.com`
   - **Auth Type**: `Password`
   - **Password**: Your App Password
   - **Encryption**: `SslTls`
   - **Folder Name**: `INBOX`
   - **Max Results**: `50`
   - **Max Days Back**: `30`
5. Click **Save**
6. Enable the data source and start searching!

#### Special Folder Names

Different email providers use different folder naming conventions:

| Provider | Sent | Trash | Drafts | All Mail |
|----------|------|-------|--------|----------|
| **Gmail** | `[Gmail]/Sent Mail` | `[Gmail]/Trash` | `[Gmail]/Drafts` | `[Gmail]/All Mail` |
| **Outlook.com** | `Sent` | `Deleted` | `Drafts` | - |
| **Yahoo** | `Sent` | `Trash` | `Draft` | - |
| **Generic** | `Sent` | `Trash` | `Drafts` | - |

#### Troubleshooting

| Issue | Solution |
|-------|----------|
| "Authentication failed" | Check email/password, or create an App Password if 2FA is enabled |
| "Connection refused" | Verify server hostname and port; check firewall settings |
| "Certificate error" | Try changing encryption method; some servers use StartTls on port 143 |
| "Login failed for Gmail" | Create an App Password at [https://myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords) |
| "Folder not found" | Check folder name spelling; use full path for Gmail folders (e.g., `[Gmail]/Sent Mail`) |
| No results | Increase "Max Days Back" or check if the folder contains emails |
| Slow searches | Reduce "Max Days Back" and "Max Results" for faster searches |

#### Security Considerations

- **Passwords are stored locally** in the application settings file
- Use **App Passwords** instead of your main account password when available
- Consider using **OAuth2** for enhanced security (requires external token management)
- Connection uses **encrypted TLS** by default
- The connector only **reads** emails - it cannot modify or delete them

#### OAuth2 Authentication

For OAuth2 authentication, you need to obtain an access token from your email provider. This typically involves:

1. Registering an application with your email provider
2. Implementing the OAuth2 flow to obtain tokens
3. Providing the access token to the connector

**Note**: OAuth2 tokens typically expire after 1 hour. You'll need to refresh them periodically.

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

#### Customizing the Detail View

When a user clicks on a search result, the detail view shows additional information. You can customize this view by overriding `GetDetailViewConfiguration()` and optionally `ExecuteActionAsync()`.

##### Default Implementation

The base class automatically displays all metadata properties:

```csharp
public virtual DetailViewConfiguration GetDetailViewConfiguration(SearchResult result)
{
    return new DetailViewConfiguration
    {
        ViewType = DetailViewType.Default,
        DisplayProperties = result.Metadata.Keys.ToList()
    };
}
```

##### Available ViewTypes

| ViewType | Description | Required Properties |
|----------|-------------|---------------------|
| `Default` | Standard text view with key-value pairs | `DisplayProperties` |
| `Table` | Tabular data display | `TableColumns` |
| `Chart` | Bar, Line, or Pie charts | `ChartConfig` |
| `Media` | Image/Video preview | `MediaPathProperty` |
| `Custom` | Custom WPF control | Override `CreateCustomDetailView()` |

##### Example: Custom Detail View with Actions

```csharp
public override DetailViewConfiguration GetDetailViewConfiguration(SearchResult result)
{
    return new DetailViewConfiguration
    {
        ViewType = DetailViewType.Default,
        
        // Which metadata properties to display
        DisplayProperties = new List<string> 
        { 
            "Author", 
            "CreatedDate", 
            "FileSize",
            "Category"
        },
        
        // Define available actions
        Actions = new List<ResultAction>
        {
            new ResultAction 
            { 
                Id = "open", 
                Name = "Open", 
                Icon = "??",
                Description = "Open the file in default application"
            },
            new ResultAction 
            { 
                Id = "copy-path", 
                Name = "Copy Path", 
                Icon = "??" 
            },
            new ResultAction 
            { 
                Id = "open-folder", 
                Name = "Open Folder", 
                Icon = "??" 
            }
        }
    };
}

// Handle action execution
public override Task<bool> ExecuteActionAsync(SearchResult result, string actionId)
{
    switch (actionId)
    {
        case "open":
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = result.OriginalReference,
                UseShellExecute = true
            });
            return Task.FromResult(true);
            
        case "copy-path":
            System.Windows.Clipboard.SetText(result.OriginalReference);
            return Task.FromResult(true);
            
        case "open-folder":
            var folder = System.IO.Path.GetDirectoryName(result.OriginalReference);
            if (!string.IsNullOrEmpty(folder))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            return Task.FromResult(true);
            
        default:
            return Task.FromResult(false);
    }
}
```

##### Example: Table View

For displaying structured data in a table format:

```csharp
public override DetailViewConfiguration GetDetailViewConfiguration(SearchResult result)
{
    return new DetailViewConfiguration
    {
        ViewType = DetailViewType.Table,
        TableColumns = new List<TableColumnDefinition>
        {
            new TableColumnDefinition 
            { 
                PropertyName = "Name", 
                Header = "Name", 
                Width = "*" 
            },
            new TableColumnDefinition 
            { 
                PropertyName = "Size", 
                Header = "Size", 
                Width = "100",
                Format = "{0:N0} KB"
            },
            new TableColumnDefinition 
            { 
                PropertyName = "Modified", 
                Header = "Modified", 
                Width = "150",
                Format = "{0:yyyy-MM-dd HH:mm}"
            }
        },
        Actions = new List<ResultAction>
        {
            new ResultAction { Id = "open", Name = "Open", Icon = "??" }
        }
    };
}
```

##### Example: Fully Custom WPF View

For complete control over the detail view, create a custom WPF control:

```csharp
public override DetailViewConfiguration GetDetailViewConfiguration(SearchResult result)
{
    return new DetailViewConfiguration
    {
        ViewType = DetailViewType.Custom
    };
}

public override FrameworkElement? CreateCustomDetailView(SearchResult result)
{
    // Create a custom panel
    var panel = new StackPanel { Margin = new Thickness(10) };
    
    // Title
    panel.Children.Add(new TextBlock 
    { 
        Text = result.Title, 
        FontWeight = FontWeights.Bold,
        FontSize = 18,
        Margin = new Thickness(0, 0, 0, 10)
    });
    
    // Description
    panel.Children.Add(new TextBlock 
    { 
        Text = result.Description,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 10)
    });
    
    // Metadata as a grid
    var grid = new Grid();
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    
    int row = 0;
    foreach (var kvp in result.Metadata)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        
        var keyText = new TextBlock 
        { 
            Text = kvp.Key + ":", 
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 2, 10, 2)
        };
        Grid.SetRow(keyText, row);
        Grid.SetColumn(keyText, 0);
        grid.Children.Add(keyText);
        
        var valueText = new TextBlock 
        { 
            Text = kvp.Value?.ToString() ?? "",
            Margin = new Thickness(0, 2, 0, 2)
        };
        Grid.SetRow(valueText, row);
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(valueText);
        
        row++;
    }
    panel.Children.Add(grid);
    
    // Action button
    var button = new Button 
    { 
        Content = "Open Item",
        Padding = new Thickness(20, 8, 20, 8),
        Margin = new Thickness(0, 15, 0, 0),
        HorizontalAlignment = HorizontalAlignment.Left
    };
    button.Click += async (s, e) => await ExecuteActionAsync(result, "open");
    panel.Children.Add(button);
    
    return panel;
}
```

##### Tips for Detail Views

1. **Use Metadata**: Store all displayable data in `SearchResult.Metadata` during search
2. **Keep Actions Simple**: Each action should have a clear, single purpose
3. **Handle Errors**: Wrap action execution in try-catch blocks
4. **Use Icons**: Emojis work well as icons (??, ??, ??, ??, ???)
5. **Consider Localization**: Use constants or resources for action names

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

### AI Search Example

For the AI Search feature, no additional code is needed in connectors. Just ensure your AI API is configured in settings.

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
