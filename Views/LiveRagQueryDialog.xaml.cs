//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Windows;
using MSCC.Localization;

namespace MSCC.Views;

/// <summary>
/// Dialog for entering a live RAG question and retrieval options.
/// </summary>
public partial class LiveRagQueryDialog : Window
{
    private const string DefaultSystemPrompt = """
        You are a live RAG assistant for a desktop meta-search application.
        Use the live RAG tool to search the selected data sources before answering.
        Answer the user's question using ONLY context returned by the live tool calls.

        IMPORTANT: Your response MUST be valid HTML. Use these HTML elements for formatting:
        - <h2> for main section headers
        - <h3> for sub-section headers
        - <p> for paragraphs
        - <ul> and <li> for bullet lists
        - <strong> for important text
        - <em> for emphasis
        - <table>, <tr>, <th>, <td> for tabular comparisons when useful

        Cite sources with markers like [Source 1] and mention uncertainty when the live tool context is insufficient.
        Do not include <html>, <head>, or <body> tags - only the inner content.
        Answer in the same language as the user's question.
        """;

    public string Question => QuestionTextBox.Text.Trim();
    public string SystemPrompt => SystemPromptTextBox.Text;
    public int MaxResultsPerSearchTerm { get; private set; } = 20;
    public int MaxContextItemsPerSource { get; private set; } = 10;
    public int MaxContextItemsTotal { get; private set; } = 40;
    public int MaxCharactersPerItem { get; private set; } = 2500;
    public bool IncludeMetadata => IncludeMetadataCheckBox.IsChecked ?? true;

    public LiveRagQueryDialog(int selectedSourceCount, string initialQuestion)
    {
        InitializeComponent();
        QuestionTextBox.Text = initialQuestion;
        SystemPromptTextBox.Text = DefaultSystemPrompt;
        ApplyLocalization(selectedSourceCount);
    }

    private void ApplyLocalization(int selectedSourceCount)
    {
        var loc = Strings.Instance;

        Title = loc["LiveRagSearch"];
        TitleText.Text = loc["LiveRagSearch"];
        DescriptionText.Text = loc["LiveRagDescription"];
        SourceInfoText.Text = string.Format(loc["LiveRagSourcesSelected"], selectedSourceCount);
        QuestionLabel.Text = loc["LiveRagQuestion"] + ":";
        SystemPromptLabel.Text = loc["AiSystemPrompt"] + ":";
        MaxResultsLabel.Text = loc["LiveRagResultsPerTerm"];
        MaxContextPerSourceLabel.Text = loc["LiveRagContextPerSource"];
        MaxContextTotalLabel.Text = loc["LiveRagContextTotal"];
        MaxCharsLabel.Text = loc["LiveRagCharsPerChunk"];
        IncludeMetadataCheckBox.Content = loc["LiveRagIncludeMetadata"];
        CancelBtn.Content = loc.Cancel;
        RunBtn.Content = loc["LiveRagAskButton"];
    }

    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(QuestionTextBox.Text))
        {
            MessageBox.Show(
                Strings.Instance["LiveRagQuestionRequired"],
                Strings.Instance.Warning,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(SystemPromptTextBox.Text))
        {
            MessageBox.Show(
                Strings.Instance["AiPromptRequired"],
                Strings.Instance.Warning,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!TryReadNumber(MaxResultsTextBox.Text, 1, 100, out var maxResults) ||
            !TryReadNumber(MaxContextPerSourceTextBox.Text, 1, 100, out var maxPerSource) ||
            !TryReadNumber(MaxContextTotalTextBox.Text, 1, 200, out var maxTotal) ||
            !TryReadNumber(MaxCharsTextBox.Text, 250, 20000, out var maxChars))
        {
            MessageBox.Show(
                Strings.Instance["LiveRagInvalidLimits"],
                Strings.Instance.ValidationError,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        MaxResultsPerSearchTerm = maxResults;
        MaxContextItemsPerSource = maxPerSource;
        MaxContextItemsTotal = maxTotal;
        MaxCharactersPerItem = maxChars;

        DialogResult = true;
        Close();
    }

    private static bool TryReadNumber(string text, int min, int max, out int value)
    {
        if (!int.TryParse(text, out value))
            return false;

        return value >= min && value <= max;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
