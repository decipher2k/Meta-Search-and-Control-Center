//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Collections.ObjectModel;
using System.Windows.Input;
using MSCC.ViewModels;

namespace MSCC.Scripting;

public class ScriptManagerViewModel : ViewModelBase
{
    private readonly ScriptingService _scriptingService;
    private readonly ScriptRepository _repository;
    
    private ConnectorScript? _selectedScript;
    private string _statusMessage = "Bereit";
    private string _newScriptName = "NeuerKonnektor";
    private bool _isLoading;

    public ObservableCollection<ConnectorScript> Scripts { get; } = new();

    public ConnectorScript? SelectedScript
    {
        get => _selectedScript;
        set
        {
            if (SetProperty(ref _selectedScript, value))
                OnPropertyChanged(nameof(HasSelection));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string NewScriptName
    {
        get => _newScriptName;
        set => SetProperty(ref _newScriptName, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool HasSelection => SelectedScript != null;
    public string ScriptsDirectory => _repository.ScriptsDirectory;

    public ICommand CreateCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CompileCommand { get; }
    public ICommand CompileAllCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ToggleEnabledCommand { get; }
    public ICommand OpenFolderCommand { get; }

    public event Action<ConnectorScript>? OnEditRequested;
    public event Func<string, string?>? OnImportFileRequested;
    public event Func<string, string?>? OnExportFileRequested;

    public ScriptManagerViewModel(ScriptingService scriptingService, ScriptRepository repository)
    {
        _scriptingService = scriptingService;
        _repository = repository;

        CreateCommand = new RelayCommand(_ => CreateScript(), _ => !string.IsNullOrWhiteSpace(NewScriptName));
        EditCommand = new RelayCommand(_ => EditScript(), _ => HasSelection);
        DeleteCommand = new RelayCommand(_ => DeleteScript(), _ => HasSelection);
        CompileCommand = new AsyncRelayCommand(CompileScriptAsync, _ => HasSelection);
        CompileAllCommand = new AsyncRelayCommand(CompileAllAsync);
        SaveCommand = new RelayCommand(_ => SaveScript(), _ => HasSelection);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ImportCommand = new AsyncRelayCommand(ImportScriptAsync);
        ExportCommand = new RelayCommand(_ => ExportScript(), _ => HasSelection);
        ToggleEnabledCommand = new RelayCommand(_ => ToggleEnabled(), _ => HasSelection);
        OpenFolderCommand = new RelayCommand(_ => OpenScriptsFolder());
    }

    public async Task LoadScriptsAsync()
    {
        IsLoading = true;
        StatusMessage = "Lade Scripts...";

        try
        {
            var count = await _repository.LoadAllAsync();
            RefreshScriptsList();
            StatusMessage = $"{count} Scripts geladen";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshScriptsList()
    {
        Scripts.Clear();
        foreach (var script in _repository.GetAll().OrderBy(s => s.Metadata.Name))
            Scripts.Add(script);
    }

    private void CreateScript()
    {
        if (string.IsNullOrWhiteSpace(NewScriptName)) return;

        var script = _repository.Create(NewScriptName);
        Scripts.Add(script);
        SelectedScript = script;
        StatusMessage = $"Script '{NewScriptName}' erstellt";
        OnEditRequested?.Invoke(script);
    }

    private void EditScript()
    {
        if (SelectedScript != null)
            OnEditRequested?.Invoke(SelectedScript);
    }

    private void DeleteScript()
    {
        if (SelectedScript == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Script '{SelectedScript.Metadata.Name}' loeschen?",
            "Loeschen",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            var name = SelectedScript.Metadata.Name;
            if (_repository.Delete(SelectedScript.Metadata.Id))
            {
                Scripts.Remove(SelectedScript);
                SelectedScript = null;
                StatusMessage = $"Script '{name}' geloescht";
            }
        }
    }

    private async Task CompileScriptAsync(object? parameter)
    {
        if (SelectedScript == null) return;

        StatusMessage = $"Kompiliere '{SelectedScript.Metadata.Name}'...";
        var result = await Task.Run(() => _repository.CompileAndRegister(SelectedScript));

        StatusMessage = result.Success
            ? $"Script erfolgreich kompiliert"
            : $"Kompilierung fehlgeschlagen: {result.Errors.Count} Fehler";

        OnPropertyChanged(nameof(SelectedScript));
    }

    private async Task CompileAllAsync(object? parameter)
    {
        StatusMessage = "Kompiliere alle Scripts...";
        var (success, failed) = await _repository.CompileAllAsync();
        StatusMessage = $"Kompiliert: {success} OK, {failed} Fehler";
        RefreshScriptsList();
    }

    private void SaveScript()
    {
        if (SelectedScript == null) return;

        if (_repository.Save(SelectedScript))
            StatusMessage = $"Script gespeichert";
        else
            StatusMessage = "Fehler beim Speichern";
    }

    private async Task RefreshAsync(object? parameter)
    {
        await LoadScriptsAsync();
    }

    private async Task ImportScriptAsync(object? parameter)
    {
        var filePath = OnImportFileRequested?.Invoke("C# Script (*.cs)|*.cs");
        if (string.IsNullOrEmpty(filePath)) return;

        var script = await _repository.ImportAsync(filePath);
        if (script != null)
        {
            Scripts.Add(script);
            SelectedScript = script;
            StatusMessage = $"Script importiert";
        }
    }

    private void ExportScript()
    {
        if (SelectedScript == null) return;

        var filePath = OnExportFileRequested?.Invoke($"{SelectedScript.Metadata.Name}.cs");
        if (string.IsNullOrEmpty(filePath)) return;

        if (_repository.Export(SelectedScript.Metadata.Id, filePath))
            StatusMessage = "Script exportiert";
    }

    private void ToggleEnabled()
    {
        if (SelectedScript == null) return;

        SelectedScript.Metadata.IsEnabled = !SelectedScript.Metadata.IsEnabled;
        _repository.Update(SelectedScript);
        StatusMessage = SelectedScript.Metadata.IsEnabled ? "Script aktiviert" : "Script deaktiviert";
        OnPropertyChanged(nameof(SelectedScript));
    }

    private void OpenScriptsFolder()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _repository.ScriptsDirectory,
                UseShellExecute = true
            });
        }
        catch { }
    }
}
