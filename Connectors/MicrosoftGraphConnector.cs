// Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using MSCC.Localization;
using MSCC.Models;
using SearchResult = MSCC.Models.SearchResult;
using Process = System.Diagnostics.Process;
using Application = System.Windows.Application;

namespace MSCC.Connectors;

/// <summary>
/// Microsoft 365 Connector for searching Calendar, ToDo, Emails, and OneNote.
/// Uses Microsoft Graph API with OAuth 2.0 authentication.
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
    private static Strings L => Strings.Instance;

    public string Id => "microsoft-graph-connector";
    public string Name => L.Connector_M365_Name;
    public string Description => L.Connector_M365_Description;
    public string Version => "1.0.0";

    public IEnumerable<ConnectorParameter> ConfigurationParameters =>
    [
        new ConnectorParameter
        {
            Name = "ClientId",
            DisplayName = L.Connector_M365_ClientId,
            Description = L.Connector_M365_ClientId_Desc,
            ParameterType = "string",
            IsRequired = true
        },
        new ConnectorParameter
        {
            Name = "TenantId",
            DisplayName = L.Connector_M365_TenantId,
            Description = L.Connector_M365_TenantId_Desc,
            ParameterType = "string",
            IsRequired = true,
            DefaultValue = "common"
        },
        new ConnectorParameter
        {
            Name = "SearchCalendar",
            DisplayName = L.Connector_M365_SearchCalendar,
            Description = L.Connector_M365_SearchCalendar_Desc,
            ParameterType = "bool",
            IsRequired = false,
            DefaultValue = "true"
        },
        new ConnectorParameter
        {
            Name = "SearchToDo",
            DisplayName = L.Connector_M365_SearchToDo,
            Description = L.Connector_M365_SearchToDo_Desc,
            ParameterType = "bool",
            IsRequired = false,
            DefaultValue = "true"
        },
        new ConnectorParameter
        {
            Name = "SearchMail",
            DisplayName = L.Connector_M365_SearchMail,
            Description = L.Connector_M365_SearchMail_Desc,
            ParameterType = "bool",
            IsRequired = false,
            DefaultValue = "true"
        },
        new ConnectorParameter
        {
            Name = "SearchOneNote",
            DisplayName = L.Connector_M365_SearchOneNote,
            Description = L.Connector_M365_SearchOneNote_Desc,
            ParameterType = "bool",
            IsRequired = false,
            DefaultValue = "true"
        },
        new ConnectorParameter
        {
            Name = "MaxDaysBack",
            DisplayName = L.Connector_M365_MaxDaysBack,
            Description = L.Connector_M365_MaxDaysBack_Desc,
            ParameterType = "int",
            IsRequired = false,
            DefaultValue = "30"
        }
    ];

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
            _tenantId = configuration.TryGetValue("TenantId", out var tenantId) && !string.IsNullOrEmpty(tenantId) 
                ? tenantId : "common";

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
        if (_searchCalendar) scopes.Add("Calendars.Read");
        if (_searchToDo) scopes.Add("Tasks.Read");
        if (_searchMail) scopes.Add("Mail.Read");
        if (_searchOneNote) { scopes.Add("Notes.Read"); scopes.Add("Notes.Read.All"); }
        return scopes.ToArray();
    }

    public async Task<IEnumerable<SearchResult>> SearchAsync(
        string searchTerm, int maxResults = 100, CancellationToken cancellationToken = default)
    {
        var results = new List<SearchResult>();

        if (_graphClient == null || !_isInitialized || string.IsNullOrWhiteSpace(searchTerm))
            return results;

        Debug.WriteLine($"[MicrosoftGraphConnector] Searching for '{searchTerm}'");

        var tasks = new List<Task<List<SearchResult>>>();
        var resultsPerType = maxResults / 4;

        if (_searchCalendar) tasks.Add(SearchCalendarAsync(searchTerm, resultsPerType, cancellationToken));
        if (_searchToDo) tasks.Add(SearchToDoAsync(searchTerm, resultsPerType, cancellationToken));
        if (_searchMail) tasks.Add(SearchMailAsync(searchTerm, resultsPerType, cancellationToken));
        if (_searchOneNote) tasks.Add(SearchOneNoteAsync(searchTerm, resultsPerType, cancellationToken));

        try
        {
            var allResults = await Task.WhenAll(tasks);
            foreach (var resultList in allResults) results.AddRange(resultList);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MicrosoftGraphConnector] Search error: {ex.Message}");
        }

        return results.Take(maxResults);
    }

    private async Task<List<SearchResult>> SearchCalendarAsync(string searchTerm, int maxResults, CancellationToken ct)
    {
        var results = new List<SearchResult>();
        try
        {
            var startDate = DateTime.UtcNow.AddDays(-_maxDaysBack);
            var endDate = DateTime.UtcNow.AddDays(365);

            var events = await _graphClient!.Me.CalendarView
                .GetAsync(config =>
                {
                    config.QueryParameters.StartDateTime = startDate.ToString("o");
                    config.QueryParameters.EndDateTime = endDate.ToString("o");
                    config.QueryParameters.Top = 100;
                    config.QueryParameters.Select = ["id", "subject", "bodyPreview", "start", "end", "location", "organizer", "webLink"];
                    config.QueryParameters.Orderby = ["start/dateTime"];
                }, ct);

            if (events?.Value == null) return results;

            foreach (var evt in events.Value)
            {
                if (ct.IsCancellationRequested || results.Count >= maxResults) break;

                var matchesSubject = evt.Subject?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false;
                var matchesBody = evt.BodyPreview?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false;
                var matchesLocation = evt.Location?.DisplayName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false;

                if (matchesSubject || matchesBody || matchesLocation)
                {
                    var startTime = evt.Start?.DateTime != null ? DateTime.Parse(evt.Start.DateTime) : DateTime.MinValue;
                    var endTime = evt.End?.DateTime != null ? DateTime.Parse(evt.End.DateTime) : DateTime.MinValue;

                    results.Add(new SearchResult
                    {
                        Title = evt.Subject ?? "(No Subject)",
                        Description = evt.BodyPreview ?? "",
                        SourceName = $"Microsoft 365 - {L.Connector_M365_CalendarEntry}",
                        ConnectorId = Id,
                        OriginalReference = evt.WebLink ?? "",
                        RelevanceScore = matchesSubject ? 100 : 70,
                        Metadata = new Dictionary<string, object>
                        {
                            ["Type"] = "Calendar", ["EventId"] = evt.Id ?? "", ["Start"] = startTime, ["End"] = endTime,
                            ["Location"] = evt.Location?.DisplayName ?? "", ["Organizer"] = evt.Organizer?.EmailAddress?.Name ?? "",
                            ["OrganizerEmail"] = evt.Organizer?.EmailAddress?.Address ?? "", ["WebLink"] = evt.WebLink ?? ""
                        }
                    });
                }
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[MicrosoftGraphConnector] Calendar search error: {ex.Message}"); }
        return results;
    }

    private async Task<List<SearchResult>> SearchToDoAsync(string searchTerm, int maxResults, CancellationToken ct)
    {
        var results = new List<SearchResult>();
        try
        {
            var taskLists = await _graphClient!.Me.Todo.Lists.GetAsync(cancellationToken: ct);
            if (taskLists?.Value == null) return results;

            foreach (var taskList in taskLists.Value)
            {
                if (ct.IsCancellationRequested || results.Count >= maxResults) break;

                var tasks = await _graphClient.Me.Todo.Lists[taskList.Id].Tasks
                    .GetAsync(config => { config.QueryParameters.Top = 50; }, ct);

                if (tasks?.Value == null) continue;

                foreach (var task in tasks.Value)
                {
                    if (ct.IsCancellationRequested || results.Count >= maxResults) break;

                    var matchesTitle = task.Title?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false;
                    var matchesBody = task.Body?.Content?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false;

                    if (matchesTitle || matchesBody)
                    {
                        var dueDate = task.DueDateTime?.DateTime != null ? DateTime.Parse(task.DueDateTime.DateTime) : (DateTime?)null;

                        results.Add(new SearchResult
                        {
                            Title = task.Title ?? "(No Title)",
                            Description = StripHtml(task.Body?.Content ?? ""),
                            SourceName = $"Microsoft 365 - ToDo ({taskList.DisplayName})",
                            ConnectorId = Id,
                            OriginalReference = $"https://to-do.live.com/tasks/id/{task.Id}",
                            RelevanceScore = matchesTitle ? 100 : 70,
                            Metadata = new Dictionary<string, object>
                            {
                                ["Type"] = "ToDo", ["TaskId"] = task.Id ?? "", ["ListId"] = taskList.Id ?? "",
                                ["ListName"] = taskList.DisplayName ?? "", ["Status"] = task.Status?.ToString() ?? "",
                                ["Importance"] = task.Importance?.ToString() ?? "", ["DueDate"] = dueDate ?? (object)"",
                                ["IsCompleted"] = task.Status == Microsoft.Graph.Models.TaskStatus.Completed,
                                ["CreatedDateTime"] = task.CreatedDateTime ?? DateTimeOffset.MinValue
                            }
                        });
                    }
                }
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[MicrosoftGraphConnector] ToDo search error: {ex.Message}"); }
        return results;
    }

    private async Task<List<SearchResult>> SearchMailAsync(string searchTerm, int maxResults, CancellationToken ct)
    {
        var results = new List<SearchResult>();
        try
        {
            var messages = await _graphClient!.Me.Messages
                .GetAsync(config =>
                {
                    config.QueryParameters.Search = $"\"{searchTerm}\"";
                    config.QueryParameters.Top = maxResults;
                    config.QueryParameters.Select = ["id", "subject", "bodyPreview", "from", "receivedDateTime", "hasAttachments", "importance", "webLink", "isRead"];
                    config.QueryParameters.Orderby = ["receivedDateTime desc"];
                }, ct);

            if (messages?.Value == null) return results;

            foreach (var message in messages.Value)
            {
                if (ct.IsCancellationRequested || results.Count >= maxResults) break;

                results.Add(new SearchResult
                {
                    Title = message.Subject ?? "(No Subject)",
                    Description = message.BodyPreview ?? "",
                    SourceName = $"Microsoft 365 - {L.Connector_M365_Email}",
                    ConnectorId = Id,
                    OriginalReference = message.WebLink ?? "",
                    RelevanceScore = 80,
                    Metadata = new Dictionary<string, object>
                    {
                        ["Type"] = "Mail", ["MessageId"] = message.Id ?? "",
                        ["From"] = message.From?.EmailAddress?.Name ?? "", ["FromEmail"] = message.From?.EmailAddress?.Address ?? "",
                        ["ReceivedDateTime"] = message.ReceivedDateTime ?? DateTimeOffset.MinValue,
                        ["HasAttachments"] = message.HasAttachments ?? false, ["Importance"] = message.Importance?.ToString() ?? "",
                        ["IsRead"] = message.IsRead ?? false, ["WebLink"] = message.WebLink ?? ""
                    }
                });
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[MicrosoftGraphConnector] Mail search error: {ex.Message}"); }
        return results;
    }

    private async Task<List<SearchResult>> SearchOneNoteAsync(string searchTerm, int maxResults, CancellationToken ct)
    {
        var results = new List<SearchResult>();
        try
        {
            var pages = await _graphClient!.Me.Onenote.Pages
                .GetAsync(config =>
                {
                    config.QueryParameters.Search = searchTerm;
                    config.QueryParameters.Top = maxResults;
                    config.QueryParameters.Select = ["id", "title", "createdDateTime", "lastModifiedDateTime", "links"];
                    config.QueryParameters.Expand = ["parentSection"];
                }, ct);

            if (pages?.Value == null) return results;

            foreach (var page in pages.Value)
            {
                if (ct.IsCancellationRequested || results.Count >= maxResults) break;

                var webLink = page.Links?.OneNoteWebUrl?.Href ?? "";
                var sectionName = page.ParentSection?.DisplayName ?? "";

                results.Add(new SearchResult
                {
                    Title = page.Title ?? "(Untitled)",
                    Description = $"{L.Connector_M365_Section}: {sectionName}",
                    SourceName = $"Microsoft 365 - OneNote",
                    ConnectorId = Id,
                    OriginalReference = webLink,
                    RelevanceScore = 85,
                    Metadata = new Dictionary<string, object>
                    {
                        ["Type"] = "OneNote", ["PageId"] = page.Id ?? "", ["SectionName"] = sectionName,
                        ["CreatedDateTime"] = page.CreatedDateTime ?? DateTimeOffset.MinValue,
                        ["LastModifiedDateTime"] = page.LastModifiedDateTime ?? DateTimeOffset.MinValue, ["WebLink"] = webLink
                    }
                });
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[MicrosoftGraphConnector] OneNote search error: {ex.Message}"); }
        return results;
    }

    public async Task<bool> TestConnectionAsync()
    {
        if (_graphClient == null) return false;
        try { var user = await _graphClient.Me.GetAsync(); return user != null; }
        catch (Exception ex) { Debug.WriteLine($"[MicrosoftGraphConnector] Connection test failed: {ex.Message}"); return false; }
    }

    public DetailViewConfiguration GetDetailViewConfiguration(SearchResult result)
    {
        var type = result.Metadata.GetValueOrDefault("Type")?.ToString() ?? "";
        return type switch
        {
            "Calendar" => new DetailViewConfiguration
            {
                ViewType = DetailViewType.Table,
                TableColumns = [
                    new() { PropertyName = "Start", Header = L.Connector_M365_Start, Width = "150", Format = "{0:g}" },
                    new() { PropertyName = "End", Header = L.Connector_M365_End, Width = "150", Format = "{0:g}" },
                    new() { PropertyName = "Location", Header = L.Connector_M365_Location, Width = "*" },
                    new() { PropertyName = "Organizer", Header = L.Connector_M365_Organizer, Width = "150" }
                ],
                Actions = GetCalendarActions()
            },
            "ToDo" => new DetailViewConfiguration
            {
                ViewType = DetailViewType.Table,
                TableColumns = [
                    new() { PropertyName = "ListName", Header = L.Connector_M365_List, Width = "150" },
                    new() { PropertyName = "Status", Header = L.Connector_M365_Status, Width = "100" },
                    new() { PropertyName = "Importance", Header = L.Connector_M365_Priority, Width = "80" },
                    new() { PropertyName = "DueDate", Header = L.Connector_M365_DueDate, Width = "150", Format = "{0:d}" }
                ],
                Actions = GetToDoActions()
            },
            "Mail" => new DetailViewConfiguration
            {
                ViewType = DetailViewType.Table,
                TableColumns = [
                    new() { PropertyName = "From", Header = L.Connector_M365_From, Width = "150" },
                    new() { PropertyName = "ReceivedDateTime", Header = L.Connector_M365_Received, Width = "150", Format = "{0:g}" },
                    new() { PropertyName = "HasAttachments", Header = L.Connector_M365_Attachments, Width = "70" },
                    new() { PropertyName = "Importance", Header = L.Connector_M365_Priority, Width = "80" }
                ],
                Actions = GetMailActions()
            },
            "OneNote" => new DetailViewConfiguration
            {
                ViewType = DetailViewType.Table,
                TableColumns = [
                    new() { PropertyName = "SectionName", Header = L.Connector_M365_Section, Width = "150" },
                    new() { PropertyName = "CreatedDateTime", Header = L.Connector_FileSystem_Created, Width = "150", Format = "{0:g}" },
                    new() { PropertyName = "LastModifiedDateTime", Header = L.Connector_FileSystem_Modified, Width = "150", Format = "{0:g}" }
                ],
                Actions = GetOneNoteActions()
            },
            _ => new DetailViewConfiguration { ViewType = DetailViewType.Default, Actions = GetDefaultActions() }
        };
    }

    private List<ResultAction> GetCalendarActions() => [
        new() { Id = "open-web", Name = L.Connector_M365_OpenOutlook, Icon = "[Cal]", Description = L.Connector_M365_OpenOutlook_Desc },
        new() { Id = "copy-link", Name = L.Connector_M365_CopyLink, Icon = "[Copy]", Description = L.Connector_M365_CopyLink_Desc }
    ];
    private List<ResultAction> GetToDoActions() => [
        new() { Id = "open-web", Name = L.Connector_M365_OpenToDo, Icon = "[Task]", Description = L.Connector_M365_OpenToDo_Desc },
        new() { Id = "copy-link", Name = L.Connector_M365_CopyLink, Icon = "[Copy]", Description = L.Connector_M365_CopyLink_Desc }
    ];
    private List<ResultAction> GetMailActions() => [
        new() { Id = "open-web", Name = L.Connector_M365_OpenOutlook, Icon = "[Mail]", Description = L.Connector_M365_OpenOutlook_Desc },
        new() { Id = "copy-link", Name = L.Connector_M365_CopyLink, Icon = "[Copy]", Description = L.Connector_M365_CopyLink_Desc }
    ];
    private List<ResultAction> GetOneNoteActions() => [
        new() { Id = "open-web", Name = L.Connector_M365_OpenOneNote, Icon = "[Note]", Description = L.Connector_M365_OpenOneNote_Desc },
        new() { Id = "copy-link", Name = L.Connector_M365_CopyLink, Icon = "[Copy]", Description = L.Connector_M365_CopyLink_Desc }
    ];
    private List<ResultAction> GetDefaultActions() => [
        new() { Id = "open-web", Name = L.Connector_M365_OpenBrowser, Icon = "[Web]", Description = L.Connector_M365_OpenBrowser_Desc }
    ];

    public FrameworkElement? CreateCustomDetailView(SearchResult result)
    {
        var type = result.Metadata.GetValueOrDefault("Type")?.ToString() ?? "";
        var stackPanel = new StackPanel { Margin = new Thickness(8) };

        var header = new TextBlock { FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) };

        switch (type)
        {
            case "Calendar": header.Text = L.Connector_M365_CalendarEntry; AddCalendarDetails(stackPanel, result); break;
            case "ToDo": header.Text = L.Connector_M365_Task; AddToDoDetails(stackPanel, result); break;
            case "Mail": header.Text = L.Connector_M365_Email; AddMailDetails(stackPanel, result); break;
            case "OneNote": header.Text = L.Connector_M365_OneNotePage; AddOneNoteDetails(stackPanel, result); break;
            default: return null;
        }

        stackPanel.Children.Insert(0, header);
        return stackPanel;
    }

    private void AddCalendarDetails(StackPanel panel, SearchResult result)
    {
        AddDetailRow(panel, L.Connector_M365_Subject, result.Title);
        AddDetailRow(panel, L.Connector_M365_Start, FormatDateTime(result.Metadata.GetValueOrDefault("Start")));
        AddDetailRow(panel, L.Connector_M365_End, FormatDateTime(result.Metadata.GetValueOrDefault("End")));
        AddDetailRow(panel, L.Connector_M365_Location, result.Metadata.GetValueOrDefault("Location")?.ToString() ?? "");
        AddDetailRow(panel, L.Connector_M365_Organizer, result.Metadata.GetValueOrDefault("Organizer")?.ToString() ?? "");
    }

    private void AddToDoDetails(StackPanel panel, SearchResult result)
    {
        AddDetailRow(panel, L.Connector_M365_Task, result.Title);
        AddDetailRow(panel, L.Connector_M365_List, result.Metadata.GetValueOrDefault("ListName")?.ToString() ?? "");
        AddDetailRow(panel, L.Connector_M365_Status, result.Metadata.GetValueOrDefault("Status")?.ToString() ?? "");
        AddDetailRow(panel, L.Connector_M365_Priority, result.Metadata.GetValueOrDefault("Importance")?.ToString() ?? "");
        var dueDate = result.Metadata.GetValueOrDefault("DueDate");
        if (dueDate is DateTime dt && dt != DateTime.MinValue) AddDetailRow(panel, L.Connector_M365_DueDate, dt.ToString("d"));
        if (!string.IsNullOrEmpty(result.Description)) AddDetailRow(panel, L.Connector_M365_Notes, result.Description);
    }

    private void AddMailDetails(StackPanel panel, SearchResult result)
    {
        AddDetailRow(panel, L.Connector_M365_Subject, result.Title);
        AddDetailRow(panel, L.Connector_M365_From, $"{result.Metadata.GetValueOrDefault("From")} <{result.Metadata.GetValueOrDefault("FromEmail")}>");
        AddDetailRow(panel, L.Connector_M365_Received, FormatDateTime(result.Metadata.GetValueOrDefault("ReceivedDateTime")));
        AddDetailRow(panel, L.Connector_M365_Priority, result.Metadata.GetValueOrDefault("Importance")?.ToString() ?? "");
        if (result.Metadata.GetValueOrDefault("HasAttachments") is true) AddDetailRow(panel, L.Connector_M365_Attachments, L.Yes);
        if (!string.IsNullOrEmpty(result.Description)) AddDetailRow(panel, L.Connector_M365_Preview, result.Description);
    }

    private void AddOneNoteDetails(StackPanel panel, SearchResult result)
    {
        AddDetailRow(panel, L.Connector_M365_Title, result.Title);
        AddDetailRow(panel, L.Connector_M365_Section, result.Metadata.GetValueOrDefault("SectionName")?.ToString() ?? "");
        AddDetailRow(panel, L.Connector_FileSystem_Created, FormatDateTime(result.Metadata.GetValueOrDefault("CreatedDateTime")));
        AddDetailRow(panel, L.Connector_FileSystem_Modified, FormatDateTime(result.Metadata.GetValueOrDefault("LastModifiedDateTime")));
    }

    private static void AddDetailRow(StackPanel panel, string label, string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        row.Children.Add(new TextBlock { Text = $"{label}: ", FontWeight = FontWeights.SemiBold, MinWidth = 100 });
        row.Children.Add(new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(row);
    }

    private static string FormatDateTime(object? value) => value switch
    {
        DateTime dt when dt != DateTime.MinValue => dt.ToString("g"),
        DateTimeOffset dto when dto != DateTimeOffset.MinValue => dto.LocalDateTime.ToString("g"),
        _ => ""
    };

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", "");
        return System.Net.WebUtility.HtmlDecode(text).Trim();
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
                        var webLink = result.Metadata.GetValueOrDefault("WebLink")?.ToString() ?? result.OriginalReference;
                        if (!string.IsNullOrEmpty(webLink))
                        {
                            Process.Start(new ProcessStartInfo(webLink) { UseShellExecute = true });
                            return true;
                        }
                        break;
                    case "copy-link":
                        var link = result.Metadata.GetValueOrDefault("WebLink")?.ToString() ?? result.OriginalReference;
                        if (!string.IsNullOrEmpty(link))
                        {
                            Application.Current.Dispatcher.Invoke(() => { Clipboard.SetText(link); });
                            return true;
                        }
                        break;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[MicrosoftGraphConnector] Action error: {ex.Message}"); return false; }
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
