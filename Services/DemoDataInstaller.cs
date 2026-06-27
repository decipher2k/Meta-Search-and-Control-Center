//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.IO;
using MSCC.Models;

namespace MSCC.Services;

public static class DemoDataInstaller
{
    private const string DemoGroupId = "demo-aurora-group";
    private const string DemoIdPrefix = "demo-aurora-";

    public static bool EnsureAuroraDemoDataSources()
    {
        var demoRoot = TryFindDemoRoot();
        if (demoRoot == null)
            return false;

        RemoveExistingDemoEntries();

        GlobalState.Groups.Add(new DataSourceGroup
        {
            Id = DemoGroupId,
            Name = "Project Aurora Demo",
            Description = "Zusammenhaengende Demo-Daten fuer alle MSCC-Konnektoren.",
            Color = "#16A085",
            IconName = "Database"
        });

        var documentsPath = Path.Combine(demoRoot, "documents");
        var apiPath = Path.Combine(demoRoot, "api", "aurora-api-results.json");
        var sqlSeedPath = Path.Combine(demoRoot, "sql", "aurora_mysql_seed.sql");
        var mailPath = Path.Combine(demoRoot, "mail", "aurora-launch-review.eml");
        var graphPath = Path.Combine(demoRoot, "graph", "aurora-graph-calendar-and-todo.json");

        Add("demo-aurora-files", "Demo Aurora - Dokumente",
            "Lokale Project-Aurora-Dokumente fuer Datei- und Live-RAG-Suche.",
            "filesystem-connector", true, new()
            {
                ["BasePath"] = documentsPath,
                ["SearchPattern"] = "*.*",
                ["IncludeSubdirectories"] = "true"
            });

        Add("demo-aurora-findinfiles", "Demo Aurora - Volltext in Dateien",
            "Durchsucht Project-Aurora-Markdown-, Text- und Code-Dateien zeilenweise.",
            "find-in-files-connector", true, new()
            {
                ["BasePath"] = demoRoot,
                ["FilePattern"] = "*.*",
                ["IncludeSubdirectories"] = "true",
                ["UseRegex"] = "false",
                ["CaseSensitive"] = "false"
            });

        Add("demo-aurora-mockdb", "Demo Aurora - Mock Datenbank",
            "Mock-Datenbank-Connector mit Project-Aurora-Labeln und Beispielabfragen.",
            "mock-database-connector", true, new()
            {
                ["ConnectionString"] = "demo://project-aurora",
                ["TableName"] = "aurora_documents"
            });

        Add("demo-aurora-generic-api", "Demo Aurora - Lokale JSON API",
            "Generic-API-Connector liest lokale Project-Aurora-JSON-Ergebnisse.",
            "generic-api-connector", true, new()
            {
                ["ApiEndpoint"] = apiPath,
                ["HttpMethod"] = "GET",
                ["AuthType"] = "None",
                ["QueryParameters"] = "",
                ["PostBody"] = "",
                ["ContentType"] = "application/json",
                ["CustomHeaders"] = "",
                ["ResultJsonPath"] = "results",
                ["ResultTitleProperty"] = "title",
                ["ResultDescriptionProperty"] = "description",
                ["ResultUrlProperty"] = "url",
                ["TimeoutSeconds"] = "5"
            });

        Add("demo-aurora-duckduckgo", "Demo Aurora - Web Kontext",
            "DuckDuckGo-Websuche fuer oeffentliche Begriffe zum Aurora-Thema wie Smart City, Privacy, Traffic Simulation.",
            "duckduckgo-connector", true, new()
            {
                ["MaxResults"] = "8",
                ["Region"] = "de-de",
                ["SafeSearch"] = "true"
            });

        Add("demo-aurora-sql", "Demo Aurora - SQL Seed",
            $"SQL-Connector fuer aurora_demo; Seed-Skript liegt in {sqlSeedPath}.",
            "sql-database-connector", false, new()
            {
                ["ConnectionString"] = "Server=localhost;Database=aurora_demo;UserId=root;Password=;",
                ["DatabaseType"] = "MySQL",
                ["Tables"] = "aurora_cities,aurora_risks",
                ["CustomQuery"] = ""
            });

        Add("demo-aurora-openai", "Demo Aurora - OpenAI Analyse Vorlage",
            "OpenAI-kompatibler Connector mit Project-Aurora-Systemprompt; API-Key eintragen und aktivieren.",
            "openai-connector", false, new()
            {
                ["ApiEndpoint"] = "https://api.openai.com/v1/chat/completions",
                ["ApiKey"] = "DEMO_API_KEY_EINTRAGEN",
                ["Model"] = "gpt-4.1-mini",
                ["SystemPrompt"] = "Du bist der Project-Aurora-Analyseassistent. Antworte nur auf Basis der verbundenen Demo-Evidenz zu Staedten, Risiken, Budget und Launch Review.",
                ["MaxTokens"] = "1200",
                ["Temperature"] = "0.2"
            });

        Add("demo-aurora-imap", "Demo Aurora - IMAP Vorlage",
            $"IMAP-Connector-Vorlage fuer Project-Aurora-Mails; Beispielmail liegt lokal unter {mailPath}.",
            "imap-connector", false, new()
            {
                ["Server"] = "imap.example.com",
                ["Port"] = "993",
                ["EmailAddress"] = "aurora-demo@example.com",
                ["AuthType"] = "Password",
                ["Password"] = "DEMO_PASSWORD_EINTRAGEN",
                ["OAuth2AccessToken"] = "",
                ["Encryption"] = "SslTls",
                ["FolderName"] = "INBOX",
                ["MaxResults"] = "20",
                ["MaxDaysBack"] = "120",
                ["LocalSampleFile"] = mailPath
            });

        Add("demo-aurora-graph", "Demo Aurora - Microsoft Graph Vorlage",
            $"Microsoft-Graph-Vorlage fuer Kalender/ToDo/Mail/OneNote; lokales Sample liegt unter {graphPath}.",
            "microsoft-graph-connector", false, new()
            {
                ["ClientId"] = "DEMO_CLIENT_ID_EINTRAGEN",
                ["TenantId"] = "common",
                ["SearchCalendar"] = "true",
                ["SearchToDo"] = "true",
                ["SearchMail"] = "true",
                ["SearchOneNote"] = "true",
                ["MaxDaysBack"] = "120",
                ["LocalSampleFile"] = graphPath
            });

        EnsureDemoQueries();
        return true;
    }

    private static void Add(
        string id,
        string name,
        string description,
        string connectorId,
        bool isEnabled,
        Dictionary<string, string> configuration)
    {
        GlobalState.DataSources.Add(new DataSource
        {
            Id = id,
            Name = name,
            Description = description,
            ConnectorId = connectorId,
            Configuration = configuration,
            IsEnabled = isEnabled,
            GroupId = DemoGroupId
        });
    }

    private static void RemoveExistingDemoEntries()
    {
        foreach (var source in GlobalState.DataSources
            .Where(source => source.GroupId == DemoGroupId || source.Id.StartsWith(DemoIdPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList())
        {
            GlobalState.DataSources.Remove(source);
        }

        foreach (var group in GlobalState.Groups
            .Where(group => group.Id == DemoGroupId)
            .ToList())
        {
            GlobalState.Groups.Remove(group);
        }

        foreach (var query in GlobalState.Queries
            .Where(query => query.Id.StartsWith(DemoIdPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList())
        {
            GlobalState.Queries.Remove(query);
        }
    }

    private static void EnsureDemoQueries()
    {
        GlobalState.Queries.Add(new SearchQuery
        {
            Id = "demo-aurora-query-risks",
            Name = "Demo: Aurora Risiken",
            SearchTerm = "Welche Risiken hat Project Aurora und wer ist verantwortlich?",
            CreatedAt = DateTime.Now,
            SelectedGroupIds = { DemoGroupId }
        });

        GlobalState.Queries.Add(new SearchQuery
        {
            Id = "demo-aurora-query-city",
            Name = "Demo: Aurora Stadtvergleich",
            SearchTerm = "Welche Stadt hat die hoechste erwartete Einsparung?",
            CreatedAt = DateTime.Now,
            SelectedGroupIds = { DemoGroupId }
        });
    }

    private static string? TryFindDemoRoot()
    {
        var candidates = new List<string>
        {
            Path.Combine(Environment.CurrentDirectory, "DemoData", "Aurora"),
            Path.Combine(AppContext.BaseDirectory, "DemoData", "Aurora")
        };

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var index = 0; directory != null && index < 8; index++, directory = directory.Parent)
            candidates.Add(Path.Combine(directory.FullName, "DemoData", "Aurora"));

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(path => File.Exists(Path.Combine(path, "README.md")));
    }
}
