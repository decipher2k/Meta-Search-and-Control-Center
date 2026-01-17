//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using MSCC.Models;
using SearchResult = MSCC.Models.SearchResult;
using Process = System.Diagnostics.Process;
using Application = System.Windows.Application;

namespace MSCC.Connectors;

/// <summary>
/// Microsoft 365 Connector zum Durchsuchen von Kalender, ToDo, E-Mails und OneNote.
/// Nutzt die Microsoft Graph API mit OAuth 2.0 Authentifizierung.
/// </summary>
public class MicrosoftGraphConnector : IDataSourceConnector, IDisposable
{
    private GraphServiceClient? _graphClient;
    private string _clientId = string.Empty;
    private string _tenantId = string.Empty;
    private bool _searchCalendar = true;
    private bool _searchToDo = true;
    private bool _searchMail = true;
    private bool _searchOneNote = true;
    private int _maxDaysBack = 30;
    private bool _isInitialized;

    public string Id => "microsoft-graph-connector";
    public string Name => "Microsoft 365";
    public string Description => "Durchsucht Microsoft 365: Kalender, ToDo, E-Mails und OneNote.";
    public string Version => "1.0.0";

    public IEnumerable<ConnectorParameter> ConfigurationParameters => new[]
    {
        new ConnectorParameter
        {
            Name = "ClientId",
            DisplayName = "Client ID (App-ID)",
            Description = "Die Application (Client) ID aus der Azure App-Registrierung.",
            ParameterType = "string",
            IsRequired = true
        },
        new ConnectorParameter
        {
            Name = "TenantId",
            DisplayName = "Tenant ID",
            Description = "Die Tenant ID (oder 'common' für Multi-Tenant, 'consumers' für persönliche Konten).",
            ParameterType = "string",
            IsRequired = true,
            DefaultValue = "common"
        },
        new ConnectorParameter
        {
            Name = "SearchCalendar",
            DisplayName = "Kalender durchsuchen",
            Description = "Kalendereinträge in die Suche einbeziehen.",
            ParameterType = "bool",
            IsRequired = false,
            DefaultValue = "true"
        },
        new ConnectorParameter
        {
            Name = "SearchToDo",
            DisplayName = "ToDo durchsuchen",
            Description = "Microsoft ToDo Aufgaben in die Suche einbeziehen.",
            ParameterType = "bool",
            IsRequired = false,
            DefaultValue = "true"
        },
        new ConnectorParameter
        {
            Name = "SearchMail",
            DisplayName = "E-Mails durchsuchen",
            Description = "E-Mails in die Suche einbeziehen.",
            ParameterType = "bool",
            IsRequired = false,
            DefaultValue = "true"
        },
        new ConnectorParameter
        {
            Name = "SearchOneNote",
            DisplayName = "OneNote durchsuchen",
            Description = "OneNote-Notizen in die Suche einbeziehen.",
            ParameterType = "bool",
            IsRequired = false,
            DefaultValue = "true"
        },
        new ConnectorParameter
        {
            Name = "MaxDaysBack",
            DisplayName = "Maximale Tage zurück",
            Description = "Wie viele Tage zurück nach Kalendereinträgen und E-Mails gesucht werden soll.",
            ParameterType = "int",
            IsRequired = false,
            DefaultValue = "30"
        }
    };

    public async Task<bool> InitializeAsync(Dictionary<string, string> configuration)
    {
        try
        {
            if (!configuration.TryGetValue("ClientId", out var clientId) || string.IsNullOrEmpty(clientId))
            {
                Debug.WriteLine("[MicrosoftGraphConnector] ClientId is required");
                return false;
            }

            _clientId = clientId;

            if (configuration.TryGetValue("TenantId", out var tenantId) && !string.IsNullOrEmpty(tenantId))
            {
                _tenantId = tenantId;
            }
            else
            {
                _tenantId = "common";
            }

            // Optionale Parameter auslesen
            if (configuration.TryGetValue("SearchCalendar", out var searchCal))
                _searchCalendar = bool.TryParse(searchCal, out var sc) && sc;

            if (configuration.TryGetValue("SearchToDo", out var searchToDo))
                _searchToDo = bool.TryParse(searchToDo, out var st) && st;

            if (configuration.TryGetValue("SearchMail", out var searchMail))
                _searchMail = bool.TryParse(searchMail, out var sm) && sm;

            if (configuration.TryGetValue("SearchOneNote", out var searchOneNote))
                _searchOneNote = bool.TryParse(searchOneNote, out var so) && so;

            if (configuration.TryGetValue("MaxDaysBack", out var maxDays))
                _maxDaysBack = int.TryParse(maxDays, out var md) ? md : 30;

            // Graph Client mit Interactive Browser Authentication erstellen
            var scopes = GetRequiredScopes();
            
            var options = new InteractiveBrowserCredentialOptions
            {
                TenantId = _tenantId,
                ClientId = _clientId,
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                RedirectUri = new Uri("http://localhost")
            };

            var credential = new InteractiveBrowserCredential(options);
            _graphClient = new GraphServiceClient(credential, scopes);

            _isInitialized = true;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MicrosoftGraphConnector] Initialization failed: {ex.Message}");
            return false;
        }
    }

    private string[] GetRequiredScopes()
    {
        var scopes = new List<string> { "User.Read" };

        if (_searchCalendar)
            scopes.Add("Calendars.Read");

        if (_searchToDo)
            scopes.Add("Tasks.Read");

        if (_searchMail)
            scopes.Add("Mail.Read");

        if (_searchOneNote)
        {
            scopes.Add("Notes.Read");
            scopes.Add("Notes.Read.All");
        }

        return scopes.ToArray();
    }

    public async Task<IEnumerable<SearchResult>> SearchAsync(
        string searchTerm,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SearchResult>();

        if (_graphClient == null || !_isInitialized)
        {
            Debug.WriteLine("[MicrosoftGraphConnector] Not initialized");
            return results;
        }

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            Debug.WriteLine("[MicrosoftGraphConnector] Search term is empty");
            return results;
        }

        Debug.WriteLine($"[MicrosoftGraphConnector] Searching for '{searchTerm}'");

        var tasks = new List<Task<List<SearchResult>>>();
        var resultsPerType = maxResults / 4; // Gleichmäßig auf alle Typen verteilen

        if (_searchCalendar)
            tasks.Add(SearchCalendarAsync(searchTerm, resultsPerType, cancellationToken));

        if (_searchToDo)
            tasks.Add(SearchToDoAsync(searchTerm, resultsPerType, cancellationToken));

        if (_searchMail)
            tasks.Add(SearchMailAsync(searchTerm, resultsPerType, cancellationToken));

        if (_searchOneNote)
            tasks.Add(SearchOneNoteAsync(searchTerm, resultsPerType, cancellationToken));

        try
        {
            var allResults = await Task.WhenAll(tasks);
            foreach (var resultList in allResults)
            {
                results.AddRange(resultList);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MicrosoftGraphConnector] Search error: {ex.Message}");
        }

        return results.Take(maxResults);
    }

    #region Calendar Search

    private async Task<List<SearchResult>> SearchCalendarAsync(
        string searchTerm,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var results = new List<SearchResult>();

        try
        {
            var startDate = DateTime.UtcNow.AddDays(-_maxDaysBack);
            var endDate = DateTime.UtcNow.AddDays(365); // Ein Jahr in die Zukunft

            var events = await _graphClient!.Me.CalendarView
                .GetAsync(config =>
                {
                    config.QueryParameters.StartDateTime = startDate.ToString("o");
                    config.QueryParameters.EndDateTime = endDate.ToString("o");
                    config.QueryParameters.Top = 100;
                    config.QueryParameters.Select = new[] { "id", "subject", "bodyPreview", "start", "end", "location", "organizer", "webLink" };
                    config.QueryParameters.Orderby = new[] { "start/dateTime" };
                }, cancellationToken);

            if (events?.Value == null)
                return results;

            foreach (var evt in events.Value)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (results.Count >= maxResults)
                    break;

                // Suche in Subject und Body
                var matchesSubject = evt.Subject?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false;
                var matchesBody = evt.BodyPreview?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false;
                var matchesLocation = evt.Location?.DisplayName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false;

                if (matchesSubject || matchesBody || matchesLocation)
                {
                    var startTime = evt.Start?.DateTime != null
                        ? DateTime.Parse(evt.Start.DateTime)
                        : DateTime.MinValue;

                    var endTime = evt.End?.DateTime != null
                        ? DateTime.Parse(evt.End.DateTime)
                        : DateTime.MinValue;

                    results.Add(new SearchResult
                    {
                        Title = evt.Subject ?? "(Kein Betreff)",
                        Description = evt.BodyPreview ?? "",
                        SourceName = "Microsoft 365 - Kalender",
                        ConnectorId = Id,
                        OriginalReference = evt.WebLink ?? "",
                        RelevanceScore = matchesSubject ? 100 : 70,
                        Metadata = new Dictionary<string, object>
                        {
                            ["Type"] = "Calendar",
                            ["EventId"] = evt.Id ?? "",
                            ["Start"] = startTime,
                            ["End"] = endTime,
                            ["Location"] = evt.Location?.DisplayName ?? "",
                            ["Organizer"] = evt.Organizer?.EmailAddress?.Name ?? "",
                            ["OrganizerEmail"] = evt.Organizer?.EmailAddress?.Address ?? "",
                            ["WebLink"] = evt.WebLink ?? ""
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MicrosoftGraphConnector] Calendar search error: {ex.Message}");
        }

        return results;
    }

    #endregion

    #region ToDo Search

    private async Task<List<SearchResult>> SearchToDoAsync(
        string searchTerm,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var results = new List<SearchResult>();

        try
        {
            // Alle ToDo-Listen abrufen
            var taskLists = await _graphClient!.Me.Todo.Lists
                .GetAsync(cancellationToken: cancellationToken);

            if (taskLists?.Value == null)
                return results;

            foreach (var taskList in taskLists.Value)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (results.Count >= maxResults)
                    break;

                // Aufgaben in jeder Liste abrufen
                var tasks = await _graphClient.Me.Todo.Lists[taskList.Id].Tasks
                    .GetAsync(config =>
                    {
                        config.QueryParameters.Top = 50;
                    }, cancellationToken);

                if (tasks?.Value == null)
                    continue;

                foreach (var task in tasks.Value)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (results.Count >= maxResults)
                        break;

                    var matchesTitle = task.Title?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false;
                    var matchesBody = task.Body?.Content?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false;

                    if (matchesTitle || matchesBody)
                    {
                        var dueDate = task.DueDateTime?.DateTime != null
                            ? DateTime.Parse(task.DueDateTime.DateTime)
                            : (DateTime?)null;

                        results.Add(new SearchResult
                        {
                            Title = task.Title ?? "(Keine Bezeichnung)",
                            Description = StripHtml(task.Body?.Content ?? ""),
                            SourceName = $"Microsoft 365 - ToDo ({taskList.DisplayName})",
                            ConnectorId = Id,
                            OriginalReference = $"https://to-do.live.com/tasks/id/{task.Id}",
                            RelevanceScore = matchesTitle ? 100 : 70,
                            Metadata = new Dictionary<string, object>
                            {
                                ["Type"] = "ToDo",
                                ["TaskId"] = task.Id ?? "",
                                ["ListId"] = taskList.Id ?? "",
                                ["ListName"] = taskList.DisplayName ?? "",
                                ["Status"] = task.Status?.ToString() ?? "",
                                ["Importance"] = task.Importance?.ToString() ?? "",
                                ["DueDate"] = dueDate ?? (object)"",
                                ["IsCompleted"] = task.Status == Microsoft.Graph.Models.TaskStatus.Completed,
                                ["CreatedDateTime"] = task.CreatedDateTime ?? DateTimeOffset.MinValue
                            }
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MicrosoftGraphConnector] ToDo search error: {ex.Message}");
        }

        return results;
    }

    #endregion

    #region Mail Search

    private async Task<List<SearchResult>> SearchMailAsync(
        string searchTerm,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var results = new List<SearchResult>();

        try
        {
            // Graph API Suche für E-Mails
            var messages = await _graphClient!.Me.Messages
                .GetAsync(config =>
                {
                    config.QueryParameters.Search = $"\"{searchTerm}\"";
                    config.QueryParameters.Top = maxResults;
                    config.QueryParameters.Select = new[] { "id", "subject", "bodyPreview", "from", "receivedDateTime", "hasAttachments", "importance", "webLink", "isRead" };
                    config.QueryParameters.Orderby = new[] { "receivedDateTime desc" };
                }, cancellationToken);

            if (messages?.Value == null)
                return results;

            foreach (var message in messages.Value)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (results.Count >= maxResults)
                    break;

                results.Add(new SearchResult
                {
                    Title = message.Subject ?? "(Kein Betreff)",
                    Description = message.BodyPreview ?? "",
                    SourceName = "Microsoft 365 - E-Mail",
                    ConnectorId = Id,
                    OriginalReference = message.WebLink ?? "",
                    RelevanceScore = 80,
                    Metadata = new Dictionary<string, object>
                    {
                        ["Type"] = "Mail",
                        ["MessageId"] = message.Id ?? "",
                        ["From"] = message.From?.EmailAddress?.Name ?? "",
                        ["FromEmail"] = message.From?.EmailAddress?.Address ?? "",
                        ["ReceivedDateTime"] = message.ReceivedDateTime ?? DateTimeOffset.MinValue,
                        ["HasAttachments"] = message.HasAttachments ?? false,
                        ["Importance"] = message.Importance?.ToString() ?? "",
                        ["IsRead"] = message.IsRead ?? false,
                        ["WebLink"] = message.WebLink ?? ""
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MicrosoftGraphConnector] Mail search error: {ex.Message}");
        }

        return results;
    }

    #endregion

    #region OneNote Search

    private async Task<List<SearchResult>> SearchOneNoteAsync(
        string searchTerm,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var results = new List<SearchResult>();

        try
        {
            // OneNote Seiten durchsuchen
            var pages = await _graphClient!.Me.Onenote.Pages
                .GetAsync(config =>
                {
                    config.QueryParameters.Search = searchTerm;
                    config.QueryParameters.Top = maxResults;
                    config.QueryParameters.Select = new[] { "id", "title", "createdDateTime", "lastModifiedDateTime", "links" };
                    config.QueryParameters.Expand = new[] { "parentSection" };
                }, cancellationToken);

            if (pages?.Value == null)
                return results;

            foreach (var page in pages.Value)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (results.Count >= maxResults)
                    break;

                var webLink = page.Links?.OneNoteWebUrl?.Href ?? "";
                var sectionName = page.ParentSection?.DisplayName ?? "";

                results.Add(new SearchResult
                {
                    Title = page.Title ?? "(Ohne Titel)",
                    Description = $"Abschnitt: {sectionName}",
                    SourceName = "Microsoft 365 - OneNote",
                    ConnectorId = Id,
                    OriginalReference = webLink,
                    RelevanceScore = 85,
                    Metadata = new Dictionary<string, object>
                    {
                        ["Type"] = "OneNote",
                        ["PageId"] = page.Id ?? "",
                        ["SectionName"] = sectionName,
                        ["CreatedDateTime"] = page.CreatedDateTime ?? DateTimeOffset.MinValue,
                        ["LastModifiedDateTime"] = page.LastModifiedDateTime ?? DateTimeOffset.MinValue,
                        ["WebLink"] = webLink
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MicrosoftGraphConnector] OneNote search error: {ex.Message}");
        }

        return results;
    }

    #endregion

    public async Task<bool> TestConnectionAsync()
    {
        if (_graphClient == null)
            return false;

        try
        {
            var user = await _graphClient.Me.GetAsync();
            return user != null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MicrosoftGraphConnector] Connection test failed: {ex.Message}");
            return false;
        }
    }

    public DetailViewConfiguration GetDetailViewConfiguration(SearchResult result)
    {
        var type = result.Metadata.GetValueOrDefault("Type")?.ToString() ?? "";

        return type switch
        {
            "Calendar" => new DetailViewConfiguration
            {
                ViewType = DetailViewType.Table,
                TableColumns = new List<TableColumnDefinition>
                {
                    new() { PropertyName = "Start", Header = "Beginn", Width = "150", Format = "{0:g}" },
                    new() { PropertyName = "End", Header = "Ende", Width = "150", Format = "{0:g}" },
                    new() { PropertyName = "Location", Header = "Ort", Width = "*" },
                    new() { PropertyName = "Organizer", Header = "Organisator", Width = "150" }
                },
                Actions = GetCalendarActions()
            },
            "ToDo" => new DetailViewConfiguration
            {
                ViewType = DetailViewType.Table,
                TableColumns = new List<TableColumnDefinition>
                {
                    new() { PropertyName = "ListName", Header = "Liste", Width = "150" },
                    new() { PropertyName = "Status", Header = "Status", Width = "100" },
                    new() { PropertyName = "Importance", Header = "Priorität", Width = "80" },
                    new() { PropertyName = "DueDate", Header = "Fällig", Width = "150", Format = "{0:d}" }
                },
                Actions = GetToDoActions()
            },
            "Mail" => new DetailViewConfiguration
            {
                ViewType = DetailViewType.Table,
                TableColumns = new List<TableColumnDefinition>
                {
                    new() { PropertyName = "From", Header = "Von", Width = "150" },
                    new() { PropertyName = "ReceivedDateTime", Header = "Empfangen", Width = "150", Format = "{0:g}" },
                    new() { PropertyName = "HasAttachments", Header = "Anhänge", Width = "70" },
                    new() { PropertyName = "Importance", Header = "Priorität", Width = "80" }
                },
                Actions = GetMailActions()
            },
            "OneNote" => new DetailViewConfiguration
            {
                ViewType = DetailViewType.Table,
                TableColumns = new List<TableColumnDefinition>
                {
                    new() { PropertyName = "SectionName", Header = "Abschnitt", Width = "150" },
                    new() { PropertyName = "CreatedDateTime", Header = "Erstellt", Width = "150", Format = "{0:g}" },
                    new() { PropertyName = "LastModifiedDateTime", Header = "Geändert", Width = "150", Format = "{0:g}" }
                },
                Actions = GetOneNoteActions()
            },
            _ => new DetailViewConfiguration
            {
                ViewType = DetailViewType.Default,
                Actions = GetDefaultActions()
            }
        };
    }

    private static List<ResultAction> GetCalendarActions() => new()
    {
        new() { Id = "open-web", Name = "In Outlook öffnen", Icon = "??", Description = "Termin in Outlook Web öffnen" },
        new() { Id = "copy-link", Name = "Link kopieren", Icon = "??", Description = "Link in Zwischenablage kopieren" }
    };

    private static List<ResultAction> GetToDoActions() => new()
    {
        new() { Id = "open-web", Name = "In ToDo öffnen", Icon = "?", Description = "Aufgabe in Microsoft ToDo öffnen" },
        new() { Id = "copy-link", Name = "Link kopieren", Icon = "??", Description = "Link in Zwischenablage kopieren" }
    };

    private static List<ResultAction> GetMailActions() => new()
    {
        new() { Id = "open-web", Name = "In Outlook öffnen", Icon = "??", Description = "E-Mail in Outlook Web öffnen" },
        new() { Id = "copy-link", Name = "Link kopieren", Icon = "??", Description = "Link in Zwischenablage kopieren" }
    };

    private static List<ResultAction> GetOneNoteActions() => new()
    {
        new() { Id = "open-web", Name = "In OneNote öffnen", Icon = "??", Description = "Seite in OneNote öffnen" },
        new() { Id = "copy-link", Name = "Link kopieren", Icon = "??", Description = "Link in Zwischenablage kopieren" }
    };

    private static List<ResultAction> GetDefaultActions() => new()
    {
        new() { Id = "open-web", Name = "Im Browser öffnen", Icon = "??", Description = "Im Standard-Browser öffnen" }
    };

    public FrameworkElement? CreateCustomDetailView(SearchResult result)
    {
        var type = result.Metadata.GetValueOrDefault("Type")?.ToString() ?? "";

        // Erstelle eine formatierte Detailansicht basierend auf dem Typ
        var stackPanel = new StackPanel { Margin = new Thickness(8) };

        // Typ-spezifisches Icon und Header
        var header = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        };

        switch (type)
        {
            case "Calendar":
                header.Text = "?? Kalendereintrag";
                AddCalendarDetails(stackPanel, result);
                break;
            case "ToDo":
                header.Text = "? Aufgabe";
                AddToDoDetails(stackPanel, result);
                break;
            case "Mail":
                header.Text = "?? E-Mail";
                AddMailDetails(stackPanel, result);
                break;
            case "OneNote":
                header.Text = "?? OneNote-Seite";
                AddOneNoteDetails(stackPanel, result);
                break;
            default:
                return null;
        }

        stackPanel.Children.Insert(0, header);
        return stackPanel;
    }

    private void AddCalendarDetails(StackPanel panel, SearchResult result)
    {
        AddDetailRow(panel, "Betreff", result.Title);
        AddDetailRow(panel, "Beginn", FormatDateTime(result.Metadata.GetValueOrDefault("Start")));
        AddDetailRow(panel, "Ende", FormatDateTime(result.Metadata.GetValueOrDefault("End")));
        AddDetailRow(panel, "Ort", result.Metadata.GetValueOrDefault("Location")?.ToString() ?? "");
        AddDetailRow(panel, "Organisator", result.Metadata.GetValueOrDefault("Organizer")?.ToString() ?? "");
        
        if (!string.IsNullOrEmpty(result.Description))
        {
            AddDetailRow(panel, "Beschreibung", result.Description);
        }
    }

    private void AddToDoDetails(StackPanel panel, SearchResult result)
    {
        AddDetailRow(panel, "Aufgabe", result.Title);
        AddDetailRow(panel, "Liste", result.Metadata.GetValueOrDefault("ListName")?.ToString() ?? "");
        AddDetailRow(panel, "Status", result.Metadata.GetValueOrDefault("Status")?.ToString() ?? "");
        AddDetailRow(panel, "Priorität", result.Metadata.GetValueOrDefault("Importance")?.ToString() ?? "");
        
        var dueDate = result.Metadata.GetValueOrDefault("DueDate");
        if (dueDate != null && dueDate is DateTime dt && dt != DateTime.MinValue)
        {
            AddDetailRow(panel, "Fällig am", dt.ToString("d"));
        }

        if (!string.IsNullOrEmpty(result.Description))
        {
            AddDetailRow(panel, "Notizen", result.Description);
        }
    }

    private void AddMailDetails(StackPanel panel, SearchResult result)
    {
        AddDetailRow(panel, "Betreff", result.Title);
        AddDetailRow(panel, "Von", $"{result.Metadata.GetValueOrDefault("From")} <{result.Metadata.GetValueOrDefault("FromEmail")}>");
        AddDetailRow(panel, "Empfangen", FormatDateTime(result.Metadata.GetValueOrDefault("ReceivedDateTime")));
        AddDetailRow(panel, "Priorität", result.Metadata.GetValueOrDefault("Importance")?.ToString() ?? "");
        
        var hasAttachments = result.Metadata.GetValueOrDefault("HasAttachments");
        if (hasAttachments is true)
        {
            AddDetailRow(panel, "Anhänge", "Ja");
        }

        if (!string.IsNullOrEmpty(result.Description))
        {
            AddDetailRow(panel, "Vorschau", result.Description);
        }
    }

    private void AddOneNoteDetails(StackPanel panel, SearchResult result)
    {
        AddDetailRow(panel, "Titel", result.Title);
        AddDetailRow(panel, "Abschnitt", result.Metadata.GetValueOrDefault("SectionName")?.ToString() ?? "");
        AddDetailRow(panel, "Erstellt", FormatDateTime(result.Metadata.GetValueOrDefault("CreatedDateTime")));
        AddDetailRow(panel, "Geändert", FormatDateTime(result.Metadata.GetValueOrDefault("LastModifiedDateTime")));
    }

    private static void AddDetailRow(StackPanel panel, string label, string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        
        row.Children.Add(new TextBlock
        {
            Text = $"{label}: ",
            FontWeight = FontWeights.SemiBold,
            MinWidth = 100
        });
        
        row.Children.Add(new TextBlock
        {
            Text = value,
            TextWrapping = TextWrapping.Wrap
        });
        
        panel.Children.Add(row);
    }

    private static string FormatDateTime(object? value)
    {
        return value switch
        {
            DateTime dt when dt != DateTime.MinValue => dt.ToString("g"),
            DateTimeOffset dto when dto != DateTimeOffset.MinValue => dto.LocalDateTime.ToString("g"),
            _ => ""
        };
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return "";

        // Einfaches HTML-Stripping
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", "");
        text = System.Net.WebUtility.HtmlDecode(text);
        return text.Trim();
    }

    public async Task<bool> ExecuteActionAsync(SearchResult result, string actionId)
    {
        return await Task.Run(() =>
        {
            try
            {
                switch (actionId)
                {
                    case "open-web":
                        var webLink = result.Metadata.GetValueOrDefault("WebLink")?.ToString() 
                            ?? result.OriginalReference;
                        
                        if (!string.IsNullOrEmpty(webLink))
                        {
                            Process.Start(new ProcessStartInfo(webLink) { UseShellExecute = true });
                            return true;
                        }
                        break;

                    case "copy-link":
                        var link = result.Metadata.GetValueOrDefault("WebLink")?.ToString() 
                            ?? result.OriginalReference;
                        
                        if (!string.IsNullOrEmpty(link))
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Clipboard.SetText(link);
                            });
                            return true;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MicrosoftGraphConnector] Action error: {ex.Message}");
                return false;
            }

            return false;
        });
    }

    public void Dispose()
    {
        _graphClient = null;
        _isInitialized = false;
        GC.SuppressFinalize(this);
    }
}
