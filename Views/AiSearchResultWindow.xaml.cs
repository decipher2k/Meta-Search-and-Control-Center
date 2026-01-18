//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Net;
using System.Windows;
using MSCC.Localization;
using MSCC.Services;

namespace MSCC.Views;

/// <summary>
/// Window for displaying AI analysis results with WebView2 for HTML rendering.
/// </summary>
public partial class AiSearchResultWindow : Window
{
    private readonly AiSearchResponse _response;
    private string _rawResponse = "";

    public AiSearchResultWindow(AiSearchResponse response)
    {
        InitializeComponent();
        _response = response;
        _rawResponse = response.Response ?? response.ErrorMessage ?? "";
        
        Loaded += async (s, e) => await InitializeWebViewAsync();
        ApplyLocalization();
        DisplayTokenInfo();
    }

    private void ApplyLocalization()
    {
        var loc = Strings.Instance;
        
        Title = loc["AiAnalysisResult"];
        TitleText.Text = loc["AiAnalysisResult"];
        CopyBtn.Content = loc["AiCopyResponse"];
        CloseBtn.Content = loc.Close;
    }

    private void DisplayTokenInfo()
    {
        var loc = Strings.Instance;
        
        if (_response.Success)
        {
            ModelText.Text = $"{loc["AiModel"]}: {_response.Model ?? "unknown"}";
            PromptTokensText.Text = $"{loc["AiPromptTokens"]}: {_response.PromptTokens}";
            CompletionTokensText.Text = $"{loc["AiCompletionTokens"]}: {_response.CompletionTokens}";
            TotalTokensText.Text = $"{loc["AiTotalTokens"]}: {_response.TotalTokens}";
        }
        else
        {
            ModelText.Text = loc.Error;
            ModelText.Foreground = System.Windows.Media.Brushes.Red;
            PromptTokensText.Text = "";
            CompletionTokensText.Text = "";
            TotalTokensText.Text = "";
        }
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            await ResponseWebView.EnsureCoreWebView2Async();
            
            var html = GenerateHtmlContent(_rawResponse, !_response.Success);
            ResponseWebView.NavigateToString(html);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView2 initialization failed: {ex.Message}");
        }
    }

    private static string GenerateHtmlContent(string content, bool isError)
    {
        var textColor = isError ? "#e74c3c" : "#2c3e50";
        
        // If the response is already HTML (contains HTML tags), use it directly
        // Otherwise, escape it for safety
        var htmlContent = content.Contains("<") && content.Contains(">") 
            ? content 
            : WebUtility.HtmlEncode(content).Replace("\n", "<br>");
        
        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <style>
                    * { margin: 0; padding: 0; box-sizing: border-box; }
                    body {
                        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                        font-size: 14px;
                        line-height: 1.6;
                        color: {{textColor}};
                        padding: 16px;
                        background: transparent;
                    }
                    h1, h2, h3, h4, h5, h6 {
                        margin-top: 1em;
                        margin-bottom: 0.5em;
                        font-weight: 600;
                        color: #2c3e50;
                    }
                    h1 { font-size: 1.5em; }
                    h2 { font-size: 1.3em; border-bottom: 1px solid #e0e0e0; padding-bottom: 0.3em; }
                    h3 { font-size: 1.1em; }
                    p { margin-bottom: 0.8em; }
                    ul, ol {
                        margin-left: 1.5em;
                        margin-bottom: 0.8em;
                    }
                    li { margin-bottom: 0.3em; }
                    code {
                        background: #f4f4f4;
                        padding: 2px 6px;
                        border-radius: 3px;
                        font-family: 'Consolas', 'Courier New', monospace;
                        font-size: 0.9em;
                    }
                    pre {
                        background: #f4f4f4;
                        padding: 12px;
                        border-radius: 4px;
                        overflow-x: auto;
                        margin-bottom: 0.8em;
                    }
                    pre code {
                        background: none;
                        padding: 0;
                    }
                    strong, b { font-weight: 600; }
                    em, i { font-style: italic; }
                    blockquote {
                        border-left: 3px solid #3498db;
                        padding-left: 12px;
                        margin: 0.8em 0;
                        color: #666;
                        background: #f8f9fa;
                        padding: 12px;
                        border-radius: 0 4px 4px 0;
                    }
                    hr {
                        border: none;
                        border-top: 1px solid #e0e0e0;
                        margin: 1em 0;
                    }
                    a { color: #3498db; text-decoration: none; }
                    a:hover { text-decoration: underline; }
                    table {
                        border-collapse: collapse;
                        width: 100%;
                        margin-bottom: 1em;
                    }
                    th, td {
                        border: 1px solid #e0e0e0;
                        padding: 8px 12px;
                        text-align: left;
                    }
                    th {
                        background: #f4f4f4;
                        font-weight: 600;
                    }
                    tr:nth-child(even) {
                        background: #f8f9fa;
                    }
                </style>
            </head>
            <body>
                {{htmlContent}}
            </body>
            </html>
            """;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_rawResponse);
            MessageBox.Show(
                Strings.Instance["AiResponseCopied"],
                Strings.Instance.Success,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch
        {
            // Ignore clipboard errors
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
