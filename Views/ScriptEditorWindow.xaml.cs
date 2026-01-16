//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Windows;
using System.Windows.Controls;
using MSCC.Scripting;

namespace MSCC.Views;

public partial class ScriptEditorWindow : Window
{
    private readonly ScriptEditorViewModel _viewModel;

    public ScriptEditorWindow(ScriptEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        SetupEditor();
        SetupBindings();
    }

    private void SetupEditor()
    {
        CodeEditor.TextChanged += CodeEditor_TextChanged;
        CodeEditor.SelectionChanged += CodeEditor_SelectionChanged;
        _viewModel.OnTemplateInsertRequested += InsertTemplate;
    }

    private void SetupBindings()
    {
        if (_viewModel.CurrentScript != null)
        {
            CodeEditor.Text = _viewModel.CurrentScript.SourceCode;
            UpdateLineNumbers();
        }
    }

    private void CodeEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_viewModel.SourceCode != CodeEditor.Text)
        {
            _viewModel.SourceCode = CodeEditor.Text;
        }
        UpdateLineNumbers();
    }

    private void CodeEditor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        var lineIndex = CodeEditor.GetLineIndexFromCharacterIndex(CodeEditor.CaretIndex);
        if (lineIndex < 0) lineIndex = 0;
        
        var charIndexStart = CodeEditor.GetCharacterIndexFromLineIndex(lineIndex);
        var charIndex = charIndexStart >= 0 ? CodeEditor.CaretIndex - charIndexStart : 0;
        
        _viewModel.CursorLine = lineIndex + 1;
        _viewModel.CursorColumn = charIndex + 1;
    }

    private void UpdateLineNumbers()
    {
        var lineCount = CodeEditor.LineCount;
        if (lineCount < 1) lineCount = 1;
        
        var numbers = string.Join("\n", Enumerable.Range(1, lineCount));
        LineNumbers.Text = numbers;
    }

    private void ErrorList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListView listView && listView.SelectedItem is CompilationError error)
        {
            if (error.Line > 0 && error.Line <= CodeEditor.LineCount)
            {
                var charIndex = CodeEditor.GetCharacterIndexFromLineIndex(error.Line - 1);
                if (charIndex >= 0)
                {
                    CodeEditor.CaretIndex = charIndex + Math.Max(0, error.Column - 1);
                    CodeEditor.Focus();
                    CodeEditor.ScrollToLine(error.Line - 1);
                }
            }
        }
    }

    private void InsertTemplate(string template)
    {
        var caretIndex = CodeEditor.CaretIndex;
        CodeEditor.Text = CodeEditor.Text.Insert(caretIndex, template);
        CodeEditor.CaretIndex = caretIndex + template.Length;
        CodeEditor.Focus();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.OnTemplateInsertRequested -= InsertTemplate;
        base.OnClosed(e);
    }

    public void LoadScript(ConnectorScript script)
    {
        _viewModel.LoadScript(script);
        CodeEditor.Text = script.SourceCode;
        UpdateLineNumbers();
    }
}
