//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.ComponentModel;
using System.Windows;
using Microsoft.Win32;
using MSCC.Localization;
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
        
        // Auf Sprachwechsel reagieren
        Strings.Instance.PropertyChanged += OnStringsPropertyChanged;
        ApplyLocalization();
        
        Loaded += async (s, e) => await _viewModel.LoadScriptsAsync();
    }

    private void OnStringsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        var loc = Strings.Instance;
        
        Title = loc.ScriptManager;
        NewScriptButton.Content = "+ " + loc.NewScript;
        CompileAllButton.Content = loc.Compile;
        LoadingText.Text = loc.Loading + "...";
        EditButton.Content = loc.Edit;
        CompileButton.Content = loc.Compile;
        SaveButton.Content = loc.Save;
        DeleteButton.Content = loc.Delete;
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
        var loc = Strings.Instance;
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = "Import Script"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private string? ShowSaveFileDialog(string defaultFileName)
    {
        var loc = Strings.Instance;
        var dialog = new SaveFileDialog
        {
            Filter = "C# Script (*.cs)|*.cs|All Files (*.*)|*.*",
            FileName = defaultFileName,
            Title = "Export Script"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    protected override void OnClosed(EventArgs e)
    {
        Strings.Instance.PropertyChanged -= OnStringsPropertyChanged;
        _viewModel.OnEditRequested -= OpenEditor;
        _viewModel.OnImportFileRequested -= ShowOpenFileDialog;
        _viewModel.OnExportFileRequested -= ShowSaveFileDialog;
        base.OnClosed(e);
    }
}
