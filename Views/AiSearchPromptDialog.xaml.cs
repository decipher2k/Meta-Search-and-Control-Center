//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Windows;
using MSCC.Localization;
using MSCC.Services;

namespace MSCC.Views;

/// <summary>
/// Dialog for entering the AI system prompt.
/// </summary>
public partial class AiSearchPromptDialog : Window
{
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
        SystemPromptTextBox.Text = AiSearchService.DefaultSearchResultsAnalysisPrompt;
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
