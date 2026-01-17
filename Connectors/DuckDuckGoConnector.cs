// Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Diagnostics;
using System.Net.Http;
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

            _httpClient = new HttpClient();
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

        try
        {
            var safeSearchParam = _safeSearch ? "1" : "-1";
            var url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(searchTerm)}&kl={_region}&kp={safeSearchParam}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[DuckDuckGoConnector] HTTP error: {response.StatusCode}");
                return results;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            results = ParseSearchResults(html, resultLimit);

            Debug.WriteLine($"[DuckDuckGoConnector] Found {results.Count} results");
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[DuckDuckGoConnector] HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            Debug.WriteLine("[DuckDuckGoConnector] Search cancelled");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DuckDuckGoConnector] Search error: {ex.Message}");
        }

        return results;
    }

    private List<SearchResult> ParseSearchResults(string html, int maxResults)
    {
        var results = new List<SearchResult>();

        try
        {
            var resultPattern = ResultBlockRegex();
            var resultMatches = resultPattern.Matches(html);

            foreach (Match resultMatch in resultMatches)
            {
                if (results.Count >= maxResults)
                    break;

                var resultHtml = resultMatch.Value;
                var linkPattern = ResultLinkRegex();
                var linkMatch = linkPattern.Match(resultHtml);

                if (!linkMatch.Success)
                    continue;

                var url = HttpUtility.HtmlDecode(linkMatch.Groups[1].Value);
                var title = StripHtml(HttpUtility.HtmlDecode(linkMatch.Groups[2].Value));
                var actualUrl = ExtractActualUrl(url);

                var snippetPattern = SnippetRegex();
                var snippetMatch = snippetPattern.Match(resultHtml);
                var description = snippetMatch.Success 
                    ? StripHtml(HttpUtility.HtmlDecode(snippetMatch.Groups[1].Value))
                    : "";

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

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(actualUrl))
                    continue;

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
        new() { Id = "open-browser", Name = L.Connector_DuckDuckGo_OpenBrowser, Icon = "[Web]", Description = L.Connector_DuckDuckGo_OpenBrowser_Desc },
        new() { Id = "copy-url", Name = L.Connector_DuckDuckGo_CopyUrl, Icon = "[Copy]", Description = L.Connector_DuckDuckGo_CopyUrl_Desc },
        new() { Id = "search-more", Name = L.Connector_DuckDuckGo_SearchMore, Icon = "[Search]", Description = L.Connector_DuckDuckGo_SearchMore_Desc }
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

    [GeneratedRegex(@"<div[^>]*class=""[^""]*result[^""]*""[^>]*>.*?</div>\s*</div>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ResultBlockRegex();

    [GeneratedRegex(@"<a[^>]*class=""[^""]*result__a[^""]*""[^>]*href=""([^""]*)""[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ResultLinkRegex();

    [GeneratedRegex(@"<a[^>]*class=""[^""]*result__snippet[^""]*""[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex SnippetRegex();
}
