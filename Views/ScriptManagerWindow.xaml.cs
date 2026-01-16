//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Windows;
using Microsoft.Win32;
using MSCC.Scripting;

namespace MSCC.Views;

public partial class ScriptManagerWindow : Window
{
    private readonly ScriptManagerViewModel _viewModel;
    private readonly ScriptingService _scriptingService;
    private readonly ScriptRepository _repository;

    public ScriptManagerWindow(ScriptingService scriptingService, ScriptRepository repository)
    {
        InitializeComponent();
        
        _scriptingService = scriptingService;
        _repository = repository;
        _viewModel = new ScriptManagerViewModel(scriptingService, repository);
        
        DataContext = _viewModel;
        
        SetupEventHandlers();
        
        Loaded += async (s, e) => await _viewModel.LoadScriptsAsync();
    }

    private void SetupEventHandlers()
    {
        _viewModel.OnEditRequested += OpenEditor;
        _viewModel.OnImportFileRequested += ShowOpenFileDialog;
        _viewModel.OnExportFileRequested += ShowSaveFileDialog;
    }

    private void OpenEditor(ConnectorScript script)
    {
        var editorViewModel = new ScriptEditorViewModel(_scriptingService, _repository);
        editorViewModel.LoadScript(script);
        
        var editorWindow = new ScriptEditorWindow(editorViewModel)
        {
            Owner = this
        };
        
        editorWindow.ShowDialog();
        
        // Aktualisiere Liste nach Bearbeitung
        _viewModel.RefreshCommand.Execute(null);
    }

    private string? ShowOpenFileDialog(string filter)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = "Script importieren"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private string? ShowSaveFileDialog(string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "C# Script (*.cs)|*.cs|Alle Dateien (*.*)|*.*",
            FileName = defaultFileName,
            Title = "Script exportieren"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.OnEditRequested -= OpenEditor;
        _viewModel.OnImportFileRequested -= ShowOpenFileDialog;
        _viewModel.OnExportFileRequested -= ShowSaveFileDialog;
        base.OnClosed(e);
    }
}
