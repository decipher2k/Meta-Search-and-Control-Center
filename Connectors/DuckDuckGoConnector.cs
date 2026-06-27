// Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MSCC.Localization;
using MSCC.Models;

namespace MSCC.Connectors;

/// <summary>
/// DuckDuckGo Web Search Connector.
/// Performs web searches via DuckDuckGo.
/// </summary>
public partial class DuckDuckGoConnector : IDataSourceConnector, IDisposable
{
    private static readonly TimeSpan EndpointTimeout = TimeSpan.FromSeconds(6);

    private HttpClient? _httpClient;
    private int _maxResults = 10;
    private string _region = "wt-wt";
    private bool _safeSearch = true;
    private bool _isInitialized;
    private static Strings L => Strings.Instance;

    public string Id => "duckduckgo-connector";
    public string Name => L.Connector_DuckDuckGo_Name;
    public string Description => L.Connector_DuckDuckGo_Description;
    public string Version => "1.0.0";

    public IEnumerable<ConnectorParameter> ConfigurationParameters =>
    [
        new ConnectorParameter
        {
            Name = "MaxResults",
            DisplayName = L.Connector_DuckDuckGo_MaxResults,
            Description = L.Connector_DuckDuckGo_MaxResults_Desc,
            ParameterType = "int",
            IsRequired = false,
            DefaultValue = "10"
        },
        new ConnectorParameter
        {
            Name = "Region",
            DisplayName = L.Connector_DuckDuckGo_Region,
            Description = L.Connector_DuckDuckGo_Region_Desc,
            ParameterType = "string",
            IsRequired = false,
            DefaultValue = "wt-wt"
        },
        new ConnectorParameter
        {
            Name = "SafeSearch",
            DisplayName = L.Connector_DuckDuckGo_SafeSearch,
            Description = L.Connector_DuckDuckGo_SafeSearch_Desc,
            ParameterType = "bool",
            IsRequired = false,
            DefaultValue = "true"
        }
    ];

    public Task<bool> InitializeAsync(Dictionary<string, string> configuration)
    {
        try
        {
            if (configuration.TryGetValue("MaxResults", out var maxResultsStr))
            {
                if (int.TryParse(maxResultsStr, out var maxResults))
                {
                    _maxResults = Math.Clamp(maxResults, 1, 30);
                }
            }

            if (configuration.TryGetValue("Region", out var region) && !string.IsNullOrEmpty(region))
            {
                _region = region;
            }

            if (configuration.TryGetValue("SafeSearch", out var safeSearch))
            {
                _safeSearch = !bool.TryParse(safeSearch, out var ss) || ss;
            }

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(8)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");

            _isInitialized = true;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DuckDuckGoConnector] Initialization failed: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public async Task<IEnumerable<SearchResult>> SearchAsync(
        string searchTerm,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SearchResult>();

        if (_httpClient == null || !_isInitialized)
        {
            Debug.WriteLine("[DuckDuckGoConnector] Not initialized");
            return results;
        }

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            Debug.WriteLine("[DuckDuckGoConnector] Search term is empty");
            return results;
        }

        var resultLimit = Math.Min(_maxResults, maxResults);

        Debug.WriteLine($"[DuckDuckGoConnector] Searching for '{searchTerm}' (max {resultLimit} results)");

        var outcome = await SearchWithDiagnosticsAsync(searchTerm, resultLimit, cancellationToken);
        foreach (var diagnostic in outcome.Diagnostics)
            Debug.WriteLine($"[DuckDuckGoConnector] {diagnostic}");

        return outcome.Results;
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

        if (_httpClient == null || !_isInitialized)
        {
            result.Success = false;
            result.ErrorMessage = "DuckDuckGo connector is not initialized.";
            return result;
        }

        var seenReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lastError = string.Empty;

        foreach (var searchTerm in LiveRagConnectorHelpers.GetSearchTerms(request))
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.ExecutedQueries.Add(searchTerm);

            var started = DateTime.Now;
            var outcome = await SearchWithDiagnosticsAsync(
                searchTerm,
                Math.Max(1, request.MaxResultsPerSearchTerm),
                cancellationToken);

            lastError = outcome.ErrorMessage ?? lastError;

            var trace = new LiveRagExecutionTrace
            {
                ConnectorId = Id,
                SourceName = Name,
                OperationType = LiveRagOperationType.KeywordSearch,
                Accepted = true,
                StartedAt = started,
                CompletedAt = DateTime.Now,
                ResultCount = outcome.Results.Count,
                Reason = outcome.Results.Count > 0
                    ? "DuckDuckGo live query returned parseable results."
                    : string.Join(" | ", outcome.Diagnostics.Take(3))
            };
            result.Diagnostics.Add(trace);

            foreach (var searchResult in outcome.Results.OrderByDescending(item => item.RelevanceScore))
            {
                var referenceKey = string.IsNullOrWhiteSpace(searchResult.OriginalReference)
                    ? $"{searchResult.ConnectorId}:{searchResult.Title}:{searchResult.Description}"
                    : searchResult.OriginalReference;

                if (!seenReferences.Add(referenceKey))
                    continue;

                var content = $"""
                    {searchResult.Description}
                    URL: {searchResult.OriginalReference}
                    Domain: {searchResult.Metadata.GetValueOrDefault("Domain")}
                    """;

                var contextItem = LiveRagConnectorHelpers.CreateContextItem(
                    searchResult,
                    searchTerm,
                    request,
                    content);
                contextItem.FromNativeLiveRag = true;
                contextItem.RelevanceScore = Math.Max(
                    contextItem.RelevanceScore,
                    LiveRagConnectorHelpers.CalculateLiveRagScore(searchResult, request.Question, result.ExecutedQueries));

                result.ContextItems.Add(contextItem);

                if (result.ContextItems.Count >= Math.Max(1, request.MaxContextItems))
                    return result;
            }
        }

        result.ContextItems = result.ContextItems
            .OrderByDescending(item => item.RelevanceScore)
            .ToList();

        if (result.ContextItems.Count == 0)
        {
            result.Success = string.IsNullOrWhiteSpace(lastError);
            result.ErrorMessage = string.IsNullOrWhiteSpace(lastError)
                ? "DuckDuckGo returned no parseable live search results."
                : lastError;
        }

        return result;
    }

    private async Task<DuckDuckGoSearchOutcome> SearchWithDiagnosticsAsync(
        string searchTerm,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var outcome = new DuckDuckGoSearchOutcome();
        if (_httpClient == null || !_isInitialized)
        {
            outcome.ErrorMessage = "DuckDuckGo connector is not initialized.";
            outcome.Diagnostics.Add(outcome.ErrorMessage);
            return outcome;
        }

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            outcome.ErrorMessage = "DuckDuckGo search term is empty.";
            outcome.Diagnostics.Add(outcome.ErrorMessage);
            return outcome;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            outcome.ErrorMessage = "DuckDuckGo search was cancelled before execution.";
            outcome.Diagnostics.Add(outcome.ErrorMessage);
            return outcome;
        }

        var safeSearchParam = _safeSearch ? "1" : "-1";
        var encodedQuery = Uri.EscapeDataString(searchTerm);
        var endpoints = new[]
        {
            $"https://html.duckduckgo.com/html/?q={encodedQuery}&kl={_region}&kp={safeSearchParam}",
            $"https://duckduckgo.com/html/?q={encodedQuery}&kl={_region}&kp={safeSearchParam}",
            $"https://lite.duckduckgo.com/lite/?q={encodedQuery}&kl={_region}&kp={safeSearchParam}"
        };

        foreach (var endpoint in endpoints)
        {
            try
            {
                using var endpointTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                endpointTimeout.CancelAfter(EndpointTimeout);

                using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
                using var response = await _httpClient.SendAsync(httpRequest, endpointTimeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    outcome.Diagnostics.Add($"{endpoint} returned HTTP {(int)response.StatusCode}.");
                    continue;
                }

                var html = await response.Content.ReadAsStringAsync(endpointTimeout.Token);
                if (string.IsNullOrWhiteSpace(html))
                {
                    outcome.Diagnostics.Add($"{endpoint} returned an empty response.");
                    continue;
                }

                var parsed = ParseSearchResults(html, maxResults);
                if (parsed.Count > 0)
                {
                    outcome.Results = parsed;
                    outcome.Diagnostics.Add($"{endpoint} returned {parsed.Count} result(s).");
                    return outcome;
                }

                outcome.Diagnostics.Add($"{endpoint} returned no parseable result anchors.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                outcome.Diagnostics.Add($"{endpoint} timed out.");
            }
            catch (Exception ex)
            {
                outcome.Diagnostics.Add($"{endpoint} failed: {ex.Message}");
            }
        }

        try
        {
            using var instantTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            instantTimeout.CancelAfter(EndpointTimeout);

            var instantResults = await SearchInstantAnswerAsync(searchTerm, maxResults, instantTimeout.Token);
            if (instantResults.Count > 0)
            {
                outcome.Results = instantResults;
                outcome.Diagnostics.Add("DuckDuckGo instant-answer API returned fallback result(s).");
                return outcome;
            }

            outcome.Diagnostics.Add("DuckDuckGo instant-answer API returned no fallback results.");
        }
        catch (Exception ex)
        {
            outcome.Diagnostics.Add($"DuckDuckGo instant-answer API failed: {ex.Message}");
        }

        outcome.ErrorMessage = string.Join(" ", outcome.Diagnostics.TakeLast(4));
        return outcome;
    }

    private List<SearchResult> ParseSearchResults(string html, int maxResults)
    {
        var results = new List<SearchResult>();

        try
        {
            var anchorMatches = ResultAnchorRegex().Matches(html);
            if (anchorMatches.Count == 0)
                anchorMatches = LiteResultAnchorRegex().Matches(html);

            foreach (Match anchorMatch in anchorMatches)
            {
                if (results.Count >= maxResults)
                    break;

                var url = HttpUtility.HtmlDecode(anchorMatch.Groups["href"].Value);
                var title = StripHtml(HttpUtility.HtmlDecode(anchorMatch.Groups["title"].Value));
                var actualUrl = ExtractActualUrl(url);

                var tail = anchorMatch.Groups["tail"].Value;
                var snippetMatch = SnippetRegex().Match(tail);
                var description = snippetMatch.Success
                    ? StripHtml(HttpUtility.HtmlDecode(snippetMatch.Groups["snippet"].Value))
                    : "";

                if (!IsUsableResultUrl(actualUrl) || string.IsNullOrWhiteSpace(title))
                    continue;

                var domain = "";
                try
                {
                    var uri = new Uri(actualUrl);
                    domain = uri.Host;
                }
                catch
                {
                    domain = actualUrl;
                }

                results.Add(new SearchResult
                {
                    Title = title.Trim(),
                    Description = description.Trim(),
                    SourceName = "DuckDuckGo",
                    ConnectorId = Id,
                    OriginalReference = actualUrl,
                    RelevanceScore = 100 - (results.Count * 3),
                    Metadata = new Dictionary<string, object>
                    {
                        ["Type"] = "WebSearch",
                        ["Url"] = actualUrl,
                        ["Domain"] = domain,
                        ["Position"] = results.Count + 1,
                        ["SearchEngine"] = "DuckDuckGo"
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DuckDuckGoConnector] Parse error: {ex.Message}");
        }

        return results;
    }

    private async Task<List<SearchResult>> SearchInstantAnswerAsync(
        string searchTerm,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var results = new List<SearchResult>();
        if (_httpClient == null)
            return results;

        var url = $"https://api.duckduckgo.com/?q={Uri.EscapeDataString(searchTerm)}&format=json&no_html=1&skip_disambig=1";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return results;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var abstractText = TryGetJsonString(root, "AbstractText");
        var abstractUrl = TryGetJsonString(root, "AbstractURL");
        var heading = TryGetJsonString(root, "Heading");
        if (!string.IsNullOrWhiteSpace(abstractText))
        {
            results.Add(CreateInstantAnswerResult(
                string.IsNullOrWhiteSpace(heading) ? searchTerm : heading,
                abstractText,
                abstractUrl,
                100,
                1));
        }

        if (root.TryGetProperty("RelatedTopics", out var relatedTopics) &&
            relatedTopics.ValueKind == JsonValueKind.Array)
        {
            foreach (var topic in FlattenRelatedTopics(relatedTopics))
            {
                if (results.Count >= maxResults)
                    break;

                var text = TryGetJsonString(topic, "Text");
                var firstUrl = TryGetJsonString(topic, "FirstURL");
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                results.Add(CreateInstantAnswerResult(
                    text.Length > 100 ? text[..100] + "..." : text,
                    text,
                    firstUrl,
                    Math.Max(40, 95 - results.Count * 3),
                    results.Count + 1));
            }
        }

        return results
            .Where(result => !string.IsNullOrWhiteSpace(result.Description))
            .Take(Math.Max(1, maxResults))
            .ToList();
    }

    private static IEnumerable<JsonElement> FlattenRelatedTopics(JsonElement topics)
    {
        foreach (var item in topics.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            if (item.TryGetProperty("Topics", out var nestedTopics) &&
                nestedTopics.ValueKind == JsonValueKind.Array)
            {
                foreach (var nested in FlattenRelatedTopics(nestedTopics))
                    yield return nested;
            }
            else
            {
                yield return item;
            }
        }
    }

    private SearchResult CreateInstantAnswerResult(
        string title,
        string description,
        string url,
        int relevanceScore,
        int position)
    {
        var reference = string.IsNullOrWhiteSpace(url)
            ? $"duckduckgo:instant:{position}"
            : url;
        var domain = "";
        if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
            domain = uri.Host;

        return new SearchResult
        {
            Title = title.Trim(),
            Description = description.Trim(),
            SourceName = "DuckDuckGo",
            ConnectorId = Id,
            OriginalReference = reference,
            RelevanceScore = relevanceScore,
            Metadata = new Dictionary<string, object>
            {
                ["Type"] = "WebInstantAnswer",
                ["Url"] = reference,
                ["Domain"] = domain,
                ["Position"] = position,
                ["SearchEngine"] = "DuckDuckGo"
            }
        };
    }

    private static string TryGetJsonString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string ExtractActualUrl(string ddgUrl)
    {
        if (ddgUrl.Contains("uddg="))
        {
            var uddgMatch = Regex.Match(ddgUrl, @"uddg=([^&]+)");
            if (uddgMatch.Success)
            {
                return Uri.UnescapeDataString(uddgMatch.Groups[1].Value);
            }
        }

        if (ddgUrl.StartsWith("//"))
            return "https:" + ddgUrl;

        return ddgUrl;
    }

    private static bool IsUsableResultUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        if (uri.Host.Contains("duckduckgo.com", StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.Contains("/y.js", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return uri.Scheme is "http" or "https";
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return "";

        var text = Regex.Replace(html, "<[^>]*>", " ");
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    public Task<bool> TestConnectionAsync()
    {
        return Task.FromResult(_httpClient != null && _isInitialized);
    }

    public DetailViewConfiguration GetDetailViewConfiguration(SearchResult result)
    {
        return new DetailViewConfiguration
        {
            ViewType = DetailViewType.Custom,
            DisplayProperties = ["Url", "Domain", "Position"],
            Actions = GetWebSearchActions()
        };
    }

    private List<ResultAction> GetWebSearchActions() =>
    [
        new() { Id = "open-browser", Name = L.Connector_DuckDuckGo_OpenBrowser, Icon = "\uD83C\uDF10", Description = L.Connector_DuckDuckGo_OpenBrowser_Desc },
        new() { Id = "copy-url", Name = L.Connector_DuckDuckGo_CopyUrl, Icon = "\uD83D\uDCCB", Description = L.Connector_DuckDuckGo_CopyUrl_Desc },
        new() { Id = "search-more", Name = L.Connector_DuckDuckGo_SearchMore, Icon = "\uD83D\uDD0D", Description = L.Connector_DuckDuckGo_SearchMore_Desc }
    ];

    public FrameworkElement? CreateCustomDetailView(SearchResult result)
    {
        var stackPanel = new StackPanel { Margin = new Thickness(8) };

        var header = new TextBlock
        {
            Text = L.Connector_DuckDuckGo_WebResult,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 12)
        };
        stackPanel.Children.Add(header);

        var titleBlock = new TextBlock
        {
            Text = result.Title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(26, 13, 171)),
            Margin = new Thickness(0, 0, 0, 4)
        };
        stackPanel.Children.Add(titleBlock);

        var url = result.Metadata.GetValueOrDefault("Url")?.ToString() ?? result.OriginalReference;
        var domain = result.Metadata.GetValueOrDefault("Domain")?.ToString() ?? "";
        
        var urlBlock = new TextBlock
        {
            Text = domain,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 128, 64)),
            Margin = new Thickness(0, 0, 0, 8)
        };
        stackPanel.Children.Add(urlBlock);

        if (!string.IsNullOrEmpty(result.Description))
        {
            var descBlock = new TextBlock
            {
                Text = result.Description,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.DarkGray),
                Margin = new Thickness(0, 0, 0, 12)
            };
            stackPanel.Children.Add(descBlock);
        }

        AddDetailRow(stackPanel, "URL", url);
        
        var position = result.Metadata.GetValueOrDefault("Position")?.ToString() ?? "";
        if (!string.IsNullOrEmpty(position))
        {
            AddDetailRow(stackPanel, L.Connector_DuckDuckGo_Position, $"#{position}");
        }

        return stackPanel;
    }

    private static void AddDetailRow(StackPanel panel, string label, string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        var row = new StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            Margin = new Thickness(0, 2, 0, 2) 
        };

        row.Children.Add(new TextBlock
        {
            Text = $"{label}: ",
            FontWeight = FontWeights.SemiBold,
            MinWidth = 80,
            FontSize = 12
        });

        row.Children.Add(new TextBlock
        {
            Text = value,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        });

        panel.Children.Add(row);
    }

    public async Task<bool> ExecuteActionAsync(SearchResult result, string actionId)
    {
        return await Task.Run(() =>
        {
            try
            {
                var url = result.Metadata.GetValueOrDefault("Url")?.ToString() 
                    ?? result.OriginalReference;

                switch (actionId)
                {
                    case "open-browser":
                        if (!string.IsNullOrEmpty(url))
                        {
                            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                            return true;
                        }
                        break;

                    case "copy-url":
                        if (!string.IsNullOrEmpty(url))
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Clipboard.SetText(url);
                            });
                            return true;
                        }
                        break;

                    case "search-more":
                        var searchUrl = $"https://duckduckgo.com/?q={Uri.EscapeDataString(result.Title)}";
                        Process.Start(new ProcessStartInfo(searchUrl) { UseShellExecute = true });
                        return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DuckDuckGoConnector] Action error: {ex.Message}");
                return false;
            }

            return false;
        });
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _httpClient = null;
        _isInitialized = false;
        GC.SuppressFinalize(this);
    }

    [GeneratedRegex(@"<a(?=[^>]*class=""[^""]*result__a[^""]*"")(?=[^>]*href=""(?<href>[^""]*)"")[^>]*>(?<title>.*?)</a>(?<tail>[\s\S]{0,2500})", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ResultAnchorRegex();

    [GeneratedRegex(@"<a(?=[^>]*class=""[^""]*result-link[^""]*"")(?=[^>]*href=""(?<href>[^""]*)"")[^>]*>(?<title>.*?)</a>(?<tail>[\s\S]{0,1800})", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LiteResultAnchorRegex();

    [GeneratedRegex(@"class=""[^""]*result__snippet[^""]*""[^>]*>(?<snippet>.*?)</(?:a|div)>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SnippetRegex();

    private sealed class DuckDuckGoSearchOutcome
    {
        public List<SearchResult> Results { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public List<string> Diagnostics { get; } = new();
    }
}
