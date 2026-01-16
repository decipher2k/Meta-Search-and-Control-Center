//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MSCC.Models;
using MSCC.Services;

namespace MSCC.ViewModels;

/// <summary>
/// Basis-Klasse für ViewModels mit INotifyPropertyChanged-Implementierung.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

/// <summary>
/// Einfache ICommand-Implementierung.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}

/// <summary>
/// Async Command Implementierung.
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Predicate<object?>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        _isExecuting = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute(parameter);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public event EventHandler? CanExecuteChanged;
    private void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Wrapper für Datenquellen mit Auswahlstatus.
/// </summary>
public class SelectableDataSource : ViewModelBase
{
    private bool _isSelected;

    public DataSource DataSource { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public SelectableDataSource(DataSource dataSource)
    {
        DataSource = dataSource;
    }
}

/// <summary>
/// Wrapper für Gruppen mit Auswahlstatus.
/// </summary>
public class SelectableGroup : ViewModelBase
{
    private bool _isSelected;

    public DataSourceGroup Group { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public SelectableGroup(DataSourceGroup group)
    {
        Group = group;
    }
}

/// <summary>
/// Wrapper für Suchergebnisse mit Label-Funktionalität.
/// </summary>
public class LabelableSearchResult : ViewModelBase
{
    private string _newLabel = string.Empty;

    public SearchResult Result { get; }
    public ObservableCollection<QueryLabel> Labels { get; } = new();

    public string NewLabel
    {
        get => _newLabel;
        set => SetProperty(ref _newLabel, value);
    }

    public LabelableSearchResult(SearchResult result)
    {
        Result = result;
    }
}

/// <summary>
/// Haupt-ViewModel für das MainWindow.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly DataSourceManager _dataSourceManager;
    private readonly SearchService _searchService;
    private CancellationTokenSource? _searchCancellation;

    private string _searchTerm = string.Empty;
    private bool _isSearching;
    private string _statusMessage = "Bereit";
    private LabelableSearchResult? _selectedResult;
    private SearchQuery? _currentQuery;
    private SelectableDataSource? _selectedDataSource;
    private SelectableGroup? _selectedGroup;

    public ObservableCollection<SelectableDataSource> DataSources { get; } = new();
    public ObservableCollection<SelectableGroup> Groups { get; } = new();
    public ObservableCollection<LabelableSearchResult> SearchResults { get; } = new();
    public ObservableCollection<SearchQuery> SavedQueries => GlobalState.Queries;

    public string SearchTerm
    {
        get => _searchTerm;
        set => SetProperty(ref _searchTerm, value);
    }

    public bool IsSearching
    {
        get => _isSearching;
        set => SetProperty(ref _isSearching, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public LabelableSearchResult? SelectedResult
    {
        get => _selectedResult;
        set => SetProperty(ref _selectedResult, value);
    }

    public SearchQuery? CurrentQuery
    {
        get => _currentQuery;
        set => SetProperty(ref _currentQuery, value);
    }

    public SelectableDataSource? SelectedDataSource
    {
        get => _selectedDataSource;
        set => SetProperty(ref _selectedDataSource, value);
    }

    public SelectableGroup? SelectedGroup
    {
        get => _selectedGroup;
        set => SetProperty(ref _selectedGroup, value);
    }

    // Expose DataSourceManager for dialogs
    public DataSourceManager DataSourceManager => _dataSourceManager;

    public ICommand SearchCommand { get; }
    public ICommand CancelSearchCommand { get; }
    public ICommand AddLabelCommand { get; }
    public ICommand RemoveLabelCommand { get; }
    public ICommand SaveQueryCommand { get; }
    public ICommand LoadQueryCommand { get; }
    public ICommand SearchByKeywordCommand { get; }
    
    // CRUD Commands
    public ICommand AddDataSourceCommand { get; }
    public ICommand EditDataSourceCommand { get; }
    public ICommand DeleteDataSourceCommand { get; }
    public ICommand AddGroupCommand { get; }
    public ICommand EditGroupCommand { get; }
    public ICommand DeleteGroupCommand { get; }

    public MainViewModel()
    {
        _dataSourceManager = new DataSourceManager();
        _searchService = new SearchService(_dataSourceManager);

        SearchCommand = new AsyncRelayCommand(ExecuteSearchAsync, _ => !IsSearching && !string.IsNullOrWhiteSpace(SearchTerm));
        CancelSearchCommand = new RelayCommand(_ => CancelSearch(), _ => IsSearching);
        AddLabelCommand = new RelayCommand(AddLabel, _ => SelectedResult != null);
        RemoveLabelCommand = new RelayCommand(RemoveLabel);
        SaveQueryCommand = new RelayCommand(SaveCurrentQuery, _ => CurrentQuery != null);
        LoadQueryCommand = new RelayCommand(LoadSavedQuery);
        SearchByKeywordCommand = new AsyncRelayCommand(SearchByKeywordAsync);

        // CRUD Commands
        AddDataSourceCommand = new RelayCommand(_ => OnAddDataSource());
        EditDataSourceCommand = new RelayCommand(_ => OnEditDataSource(), _ => SelectedDataSource != null);
        DeleteDataSourceCommand = new RelayCommand(_ => OnDeleteDataSource(), _ => SelectedDataSource != null);
        AddGroupCommand = new RelayCommand(_ => OnAddGroup());
        EditGroupCommand = new RelayCommand(_ => OnEditGroup(), _ => SelectedGroup != null);
        DeleteGroupCommand = new RelayCommand(_ => OnDeleteGroup(), _ => SelectedGroup != null);

        InitializeDefaultDataSources();
    }

    private void InitializeDefaultDataSources()
    {
        _dataSourceManager.RegisterDefaultConnectors();

        // Erstelle Beispiel-Gruppen
        var documentsGroup = _dataSourceManager.CreateGroup("Dokumente", "Alle Dokumenten-Datenquellen", "#3498db");
        var databaseGroup = _dataSourceManager.CreateGroup("Datenbanken", "Alle Datenbank-Datenquellen", "#e74c3c");

        // Erstelle Datenquellen synchron im Hintergrund und aktualisiere dann UI
        Task.Run(async () =>
        {
            try
            {
                var fileDs = await _dataSourceManager.CreateDataSourceAsync(
                    "Eigene Dateien",
                    "filesystem-connector",
                    new Dictionary<string, string>
                    {
                        ["BasePath"] = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        ["SearchPattern"] = "*.*",
                        ["IncludeSubdirectories"] = "true"
                    },
                    documentsGroup.Id);

                var dbDs = await _dataSourceManager.CreateDataSourceAsync(
                    "Mock Datenbank",
                    "mock-database-connector",
                    new Dictionary<string, string>
                    {
                        ["ConnectionString"] = "Server=localhost;Database=Test",
                        ["TableName"] = "Documents"
                    },
                    databaseGroup.Id);

                // Aktualisiere UI auf dem UI-Thread
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    RefreshDataSources();
                    var count = DataSources.Count;
                    StatusMessage = $"Bereit - {count} Datenquellen geladen";
                });
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Fehler beim Laden der Datenquellen: {ex.Message}";
                });
            }
        });

        RefreshDataSources();
    }

    // Events for dialog handling (to be subscribed by View)
    public event Action? AddDataSourceRequested;
    public event Action<DataSource>? EditDataSourceRequested;
    public event Action? AddGroupRequested;
    public event Action<DataSourceGroup>? EditGroupRequested;

    private void OnAddDataSource()
    {
        AddDataSourceRequested?.Invoke();
    }

    private void OnEditDataSource()
    {
        if (SelectedDataSource != null)
        {
            EditDataSourceRequested?.Invoke(SelectedDataSource.DataSource);
        }
    }

    private void OnDeleteDataSource()
    {
        if (SelectedDataSource == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Möchten Sie die Datenquelle '{SelectedDataSource.DataSource.Name}' wirklich löschen?",
            "Datenquelle löschen",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            _dataSourceManager.RemoveDataSource(SelectedDataSource.DataSource.Id);
            RefreshDataSources();
            StatusMessage = $"Datenquelle '{SelectedDataSource.DataSource.Name}' gelöscht";
        }
    }

    private void OnAddGroup()
    {
        AddGroupRequested?.Invoke();
    }

    private void OnEditGroup()
    {
        if (SelectedGroup != null)
        {
            EditGroupRequested?.Invoke(SelectedGroup.Group);
        }
    }

    private void OnDeleteGroup()
    {
        if (SelectedGroup == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Möchten Sie die Gruppe '{SelectedGroup.Group.Name}' wirklich löschen?\n\nDatenquellen in dieser Gruppe werden nicht gelöscht, sondern nur aus der Gruppe entfernt.",
            "Gruppe löschen",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            _dataSourceManager.RemoveGroup(SelectedGroup.Group.Id, removeDataSources: false);
            RefreshDataSources();
            StatusMessage = $"Gruppe '{SelectedGroup.Group.Name}' gelöscht";
        }
    }

    public void RefreshDataSources()
    {
        DataSources.Clear();
        foreach (var ds in GlobalState.DataSources)
        {
            DataSources.Add(new SelectableDataSource(ds));
        }

        Groups.Clear();
        foreach (var group in GlobalState.Groups)
        {
            Groups.Add(new SelectableGroup(group));
        }
    }

    private async Task ExecuteSearchAsync(object? parameter)
    {
        if (string.IsNullOrWhiteSpace(SearchTerm))
            return;

        IsSearching = true;
        StatusMessage = "Suche läuft...";
        SearchResults.Clear();

        _searchCancellation = new CancellationTokenSource();

        try
        {
            var selectedDataSourceIds = DataSources
                .Where(ds => ds.IsSelected)
                .Select(ds => ds.DataSource.Id)
                .ToList();

            var selectedGroupIds = Groups
                .Where(g => g.IsSelected)
                .Select(g => g.Group.Id)
                .ToList();

            // Wenn nichts ausgewählt ist, alle aktivierten Datenquellen verwenden
            if (selectedDataSourceIds.Count == 0 && selectedGroupIds.Count == 0)
            {
                selectedDataSourceIds = DataSources
                    .Where(ds => ds.DataSource.IsEnabled)
                    .Select(ds => ds.DataSource.Id)
                    .ToList();
            }

            if (selectedDataSourceIds.Count == 0)
            {
                StatusMessage = "Keine Datenquellen ausgewaehlt oder verfuegbar";
                IsSearching = false;
                return;
            }

            StatusMessage = $"Suche in {selectedDataSourceIds.Count} Datenquelle(n)...";

            var progress = new Progress<(string sourceName, int resultCount)>(p =>
            {
                StatusMessage = $"Ergebnisse von {p.sourceName}: {p.resultCount}";
            });

            CurrentQuery = await _searchService.ExecuteSearchAsync(
                SearchTerm,
                selectedDataSourceIds,
                selectedGroupIds,
                cancellationToken: _searchCancellation.Token);

            var results = await _searchService.GetSearchResultsAsync(
                CurrentQuery,
                progress: progress,
                cancellationToken: _searchCancellation.Token);

            foreach (var result in results)
            {
                var labelable = new LabelableSearchResult(result);
                
                // Lade vorhandene Labels aus der Abfrage
                foreach (var label in CurrentQuery.Labels.Where(l => l.DataReference == result.OriginalReference))
                {
                    labelable.Labels.Add(label);
                }
                
                SearchResults.Add(labelable);
            }

            StatusMessage = $"Suche abgeschlossen: {results.Count} Ergebnisse in {selectedDataSourceIds.Count} Quelle(n)";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Suche abgebrochen";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler bei der Suche: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
            _searchCancellation?.Dispose();
            _searchCancellation = null;
        }
    }

    private void CancelSearch()
    {
        _searchCancellation?.Cancel();
    }

    private void AddLabel(object? parameter)
    {
        if (SelectedResult == null || CurrentQuery == null)
            return;

        if (string.IsNullOrWhiteSpace(SelectedResult.NewLabel))
            return;

        _searchService.AddLabel(CurrentQuery, SelectedResult.Result, SelectedResult.NewLabel);

        var label = CurrentQuery.Labels.Last();
        SelectedResult.Labels.Add(label);
        SelectedResult.NewLabel = string.Empty;

        StatusMessage = $"Label '{label.Keyword}' hinzugefügt";
    }

    private void RemoveLabel(object? parameter)
    {
        if (parameter is not QueryLabel label || CurrentQuery == null)
            return;

        if (_searchService.RemoveLabel(CurrentQuery, label.Id))
        {
            var result = SearchResults.FirstOrDefault(r => 
                r.Labels.Any(l => l.Id == label.Id));
            result?.Labels.Remove(label);

            StatusMessage = $"Label '{label.Keyword}' entfernt";
        }
    }

    private void SaveCurrentQuery(object? parameter)
    {
        if (CurrentQuery == null)
            return;

        var name = parameter as string ?? $"Abfrage vom {DateTime.Now:g}";
        _searchService.SaveQuery(CurrentQuery, name);
        StatusMessage = $"Abfrage '{name}' gespeichert";
    }

    private void LoadSavedQuery(object? parameter)
    {
        if (parameter is not string queryId)
            return;

        var query = _searchService.LoadQuery(queryId);
        if (query != null)
        {
            SearchTerm = query.SearchTerm;
            CurrentQuery = query;
            StatusMessage = $"Abfrage '{query.Name}' geladen";
        }
    }

    private async Task SearchByKeywordAsync(object? parameter)
    {
        if (parameter is not string keyword)
            return;

        var queries = _searchService.SearchByKeyword(keyword);
        StatusMessage = $"Gefundene Abfragen mit Keyword '{keyword}': {queries.Count()}";
    }
}
