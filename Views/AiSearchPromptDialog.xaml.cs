//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Windows;
using MSCC.Localization;

namespace MSCC.Views;

/// <summary>
/// Dialog for entering the AI system prompt.
/// </summary>
public partial class AiSearchPromptDialog : Window
{
    private const string DefaultSystemPrompt = """
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
        """;

    public string SystemPrompt => SystemPromptTextBox.Text;
    
    public AiSearchPromptDialog(int resultCount)
    {
        InitializeComponent();
        ApplyLocalization(resultCount);
    }

    private void ApplyLocalization(int resultCount)
    {
        var loc = Strings.Instance;
        
        Title = loc["AiSearch"];
        TitleText.Text = loc["AiSearch"];
        DescriptionText.Text = loc["AiSearchDescription"];
        SystemPromptLabel.Text = loc["AiSystemPrompt"] + ":";
        SystemPromptTextBox.Text = DefaultSystemPrompt;
        ResultsInfoText.Text = string.Format(loc["AiResultsToAnalyze"], resultCount);
        CancelBtn.Content = loc.Cancel;
        AnalyzeBtn.Content = loc["AiAnalyzeButton"];
    }

    private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SystemPromptTextBox.Text))
        {
            MessageBox.Show(
                Strings.Instance["AiPromptRequired"],
                Strings.Instance.Warning,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }
        
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
