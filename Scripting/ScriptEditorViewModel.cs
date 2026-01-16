//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Collections.ObjectModel;
using System.Windows.Input;
using MSCC.Localization;
using MSCC.ViewModels;

namespace MSCC.Scripting;

public class ScriptEditorViewModel : ViewModelBase
{
    private readonly ScriptingService _scriptingService;
    private readonly ScriptRepository _repository;
    
    private ConnectorScript? _currentScript;
    private string _sourceCode = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isCompiling;
    private bool _hasUnsavedChanges;
    private int _cursorLine = 1;
    private int _cursorColumn = 1;

    public ObservableCollection<CompilationError> Errors { get; } = new();
    public ObservableCollection<CompilationError> Warnings { get; } = new();

    public ConnectorScript? CurrentScript
    {
        get => _currentScript;
        set
        {
            if (SetProperty(ref _currentScript, value))
            {
                SourceCode = value?.SourceCode ?? string.Empty;
                HasUnsavedChanges = false;
                OnPropertyChanged(nameof(ScriptName));
                OnPropertyChanged(nameof(HasScript));
            }
        }
    }

    public string SourceCode
    {
        get => _sourceCode;
        set
        {
            if (SetProperty(ref _sourceCode, value))
            {
                HasUnsavedChanges = true;
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsCompiling
    {
        get => _isCompiling;
        set => SetProperty(ref _isCompiling, value);
    }

    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        set => SetProperty(ref _hasUnsavedChanges, value);
    }

    public int CursorLine
    {
        get => _cursorLine;
        set => SetProperty(ref _cursorLine, value);
    }

    public int CursorColumn
    {
        get => _cursorColumn;
        set => SetProperty(ref _cursorColumn, value);
    }

    public string ScriptName => CurrentScript?.Metadata.Name ?? Strings.Instance.NoResults;
    public bool HasScript => CurrentScript != null;

    public ICommand CompileCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ValidateCommand { get; }
    public ICommand InsertTemplateCommand { get; }

    public event Action<string>? OnTemplateInsertRequested;

    public ScriptEditorViewModel(ScriptingService scriptingService, ScriptRepository repository)
    {
        _scriptingService = scriptingService;
        _repository = repository;
        _statusMessage = Strings.Instance.Ready;

        CompileCommand = new AsyncRelayCommand(CompileAsync, _ => HasScript && !IsCompiling);
        SaveCommand = new RelayCommand(_ => Save(), _ => HasScript && HasUnsavedChanges);
        ValidateCommand = new RelayCommand(_ => ValidateCode());
        InsertTemplateCommand = new RelayCommand(InsertTemplate);
    }

    public void LoadScript(ConnectorScript script)
    {
        CurrentScript = script;
        Errors.Clear();
        Warnings.Clear();
        StatusMessage = $"Script '{script.Metadata.Name}' loaded";
    }

    private async Task CompileAsync(object? parameter)
    {
        if (CurrentScript == null) return;

        IsCompiling = true;
        StatusMessage = Strings.Instance.Compiling + "...";
        Errors.Clear();
        Warnings.Clear();

        try
        {
            CurrentScript.SourceCode = SourceCode;
            var result = await Task.Run(() => _repository.CompileAndRegister(CurrentScript));

            foreach (var error in result.Errors)
                Errors.Add(error);
            foreach (var warning in result.Warnings)
                Warnings.Add(warning);

            StatusMessage = result.Success
                ? Strings.Instance.CompileSuccess
                : $"{Strings.Instance.CompileFailed} - {result.Errors.Count} {Strings.Instance.Errors}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"{Strings.Instance.Error}: {ex.Message}";
        }
        finally
        {
            IsCompiling = false;
        }
    }

    private void Save()
    {
        if (CurrentScript == null) return;

        CurrentScript.SourceCode = SourceCode;
        _repository.Update(CurrentScript);

        if (_repository.Save(CurrentScript))
        {
            HasUnsavedChanges = false;
            StatusMessage = Strings.Instance.Success;
        }
        else
        {
            StatusMessage = Strings.Instance.Error;
        }
    }

    private void ValidateCode()
    {
        if (string.IsNullOrWhiteSpace(SourceCode)) return;

        var errors = _scriptingService.Validate(SourceCode);
        
        Errors.Clear();
        Warnings.Clear();
        
        foreach (var error in errors)
        {
            if (error.Severity == "Error")
                Errors.Add(error);
            else
                Warnings.Add(error);
        }
    }

    private void InsertTemplate(object? parameter)
    {
        if (parameter is not string templateName) return;

        var template = templateName switch
        {
            "search" => "\nresults.Add(CreateResult(\"Title\", \"Description\", \"reference\"));\n",
            "action" => "\ncase \"actionId\":\n    return Task.FromResult(true);\n",
            "config" => "\nnew ConnectorParameter { Name = \"Param\", DisplayName = \"Parameter\", IsRequired = true },\n",
            "detailview" => "\nreturn new DetailViewConfiguration { ViewType = DetailViewType.Default };\n",
            _ => string.Empty
        };

        if (!string.IsNullOrEmpty(template))
            OnTemplateInsertRequested?.Invoke(template);
    }

    public async Task<List<ScriptCompletionItem>> GetCompletionsAsync(int position)
    {
        return await _scriptingService.GetCompletionsAsync(SourceCode, position);
    }
}
