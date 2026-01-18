// Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.ComponentModel;
using System.Windows;
using MSCC.Connectors;
using MSCC.Localization;
using MSCC.Models;
using MSCC.Scripting;
using MSCC.Services;
using MSCC.ViewModels;
using MSCC.Views;

namespace MSCC
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly ResultDetailView _detailView;
        private readonly ScriptingService _scriptingService;
        private readonly ScriptRepository _scriptRepository;
        private bool _isInitialized;

        public MainWindow()
        {
            // Sprache beim Start anwenden
            SettingsService.Instance.ApplyLanguage();
            
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            // Scripting initialisieren
            _scriptingService = new ScriptingService();
            _scriptRepository = new ScriptRepository(_scriptingService);

            // Detail-Ansicht initialisieren
            _detailView = new ResultDetailView(_viewModel.DataSourceManager);
            DetailViewContainer.Content = _detailView;

            // Subscribe to property changes for detail view updates
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Subscribe to dialog events
            _viewModel.AddDataSourceRequested += OnAddDataSourceRequested;
            _viewModel.EditDataSourceRequested += OnEditDataSourceRequested;
            _viewModel.AddGroupRequested += OnAddGroupRequested;
            _viewModel.EditGroupRequested += OnEditGroupRequested;

            // Auf Sprachwechsel reagieren
            Strings.Instance.PropertyChanged += (s, e) => ApplyLocalization();
            
            // Lokalisierung anwenden
            ApplyLocalization();

            // Daten und Scripts beim Start laden
            Loaded += async (s, e) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                _viewModel.StatusMessage = Strings.Instance.Loading + "...";
                
                // Gespeicherte Datenquellen laden
                await _viewModel.DataSourceManager.LoadSavedDataSourcesAsync();
                
                // Wenn keine Datenquellen vorhanden sind, Beispiel-Daten erstellen
                if (GlobalState.DataSources.Count == 0 && GlobalState.Groups.Count == 0)
                {
                    await CreateDefaultDataSourcesAsync();
                }
                
                _viewModel.RefreshDataSources();
                
                // Scripts laden und kompilieren
                var count = await _scriptRepository.LoadAllAsync();
                if (count > 0)
                {
                    var (success, failed) = await _scriptRepository.CompileAllAsync();
                    
                    // Wichtig: Nach der Kompilierung auf dem UI-Thread aktualisieren
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _viewModel.RefreshDataSources();
                        
                        if (success > 0)
                        {
                            _viewModel.StatusMessage = Strings.Format("ScriptConnectorsLoaded", success);
                        }
                        else if (failed > 0)
                        {
                            _viewModel.StatusMessage = Strings.Format("CompileFailed", failed);
                        }
                        else
                        {
                            _viewModel.StatusMessage = Strings.Instance.Ready;
                        }
                    });
                }
                else
                {
                    var dsCount = GlobalState.DataSources.Count;
                    if (dsCount > 0)
                    {
                        _viewModel.StatusMessage = $"{Strings.Instance.Ready} - {dsCount} {Strings.Instance.DataSources}";
                    }
                    else
                    {
                        _viewModel.StatusMessage = Strings.Instance.Ready;
                    }
                }
                
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"{Strings.Instance.Error}: {ex.Message}";
            }
        }

        private async Task CreateDefaultDataSourcesAsync()
        {
            try
            {
                // Erstelle Beispiel-Gruppen
                var documentsGroup = _viewModel.DataSourceManager.CreateGroup("Dokumente", "Alle Dokumenten-Datenquellen", "#3498db");
                var databaseGroup = _viewModel.DataSourceManager.CreateGroup("Datenbanken", "Alle Datenbank-Datenquellen", "#e74c3c");

                // Erstelle Beispiel-Datenquellen
                await _viewModel.DataSourceManager.CreateDataSourceAsync(
                    "Eigene Dateien",
                    "filesystem-connector",
                    new Dictionary<string, string>
                    {
                        ["BasePath"] = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        ["SearchPattern"] = "*.*",
                        ["IncludeSubdirectories"] = "true"
                    },
                    documentsGroup.Id);

                await _viewModel.DataSourceManager.CreateDataSourceAsync(
                    "Mock Datenbank",
                    "mock-database-connector",
                    new Dictionary<string, string>
                    {
                        ["ConnectionString"] = "Server=localhost;Database=Test",
                        ["TableName"] = "Documents"
                    },
                    databaseGroup.Id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating default data sources: {ex.Message}");
            }
        }

        private void ApplyLocalization()
        {
            var loc = Strings.Instance;
            
            // Window Title
            Title = loc.AppTitle;
            
            // Menu
            MenuFile.Header = loc.MenuFile;
            MenuSettings.Header = loc.MenuSettings + "...";
            MenuExit.Header = loc.MenuExit;
            MenuPlugins.Header = loc.MenuPlugins;
            MenuScriptManager.Header = loc.MenuScriptManager + "...";
            MenuReloadScripts.Header = loc.MenuReloadScripts;
            MenuHelp.Header = loc.MenuHelp;
            MenuAbout.Header = loc.MenuAbout;
            
            // Search
            SearchButton.Content = loc.Search;
            AiSearchButton.Content = loc["AiSearch"];
            AiSearchButton.ToolTip = loc["AiSearchTooltip"];
            CancelSearchButton.Content = loc.Cancel;
            
            // Headers
            GroupsHeader.Text = loc.Groups;
            DataSourcesHeader.Text = loc.DataSources;
            SearchResultsHeader.Text = loc.SearchResults;
            SearchingText.Text = loc.Searching + "...";
            DetailViewHeader.Text = loc.DetailView;
            SelectResultText.Text = loc.SelectResultForDetails;
            
            // Right sidebar
            AddLabelHeader.Text = loc.AddLabel;
            SelectResultFirstText.Text = loc.SelectResultFirst;
            KeywordSearchHeader.Text = loc.KeywordSearch;
            SearchByKeywordButton.Content = loc.SearchByKeyword;
            CurrentQueryHeader.Text = loc.CurrentQuery;
            SaveQueryButton.Content = loc.Save;
            SavedQueriesHeader.Text = loc.SavedQueries;
        }

        // Menu Event Handlers
        private void MenuItem_Settings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SettingsDialog { Owner = this };
            dialog.ShowDialog();
        }

        private void MenuItem_ScriptManager_Click(object sender, RoutedEventArgs e)
        {
            var scriptManager = new ScriptManagerWindow(_scriptingService, _scriptRepository)
            {
                Owner = this
            };
            scriptManager.ShowDialog();
            
            // Nach dem Schließen des Script Managers die Datenquellen-Liste aktualisieren
            // damit neue Konnektoren im DataSourceDialog sichtbar sind
            _viewModel.RefreshDataSources();
        }

        private async void MenuItem_ReloadScripts_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.StatusMessage = Strings.Instance.Loading + "...";
            await InitializeAsync();
        }

        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MenuItem_About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "MSCC - Meta Search Command Center\n\nVersion 1.0.0\n\n(c) 2026 Dennis Michael Heine",
                Strings.Instance.MenuAbout,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedResult))
            {
                UpdateDetailView();
            }
        }

        private void UpdateDetailView()
        {
            if (_viewModel.SelectedResult == null)
                return;

            var result = _viewModel.SelectedResult.Result;
            IDataSourceConnector? connector = null;

            var dataSource = _viewModel.DataSources
                .FirstOrDefault(ds => ds.DataSource.ConnectorId == result.ConnectorId);

            if (dataSource != null)
                connector = _viewModel.DataSourceManager.GetConnectorInstance(dataSource.DataSource.Id);

            _detailView.ShowResult(result, connector);
        }

        private void OnAddDataSourceRequested()
        {
            var dialog = new DataSourceDialog(_viewModel.DataSourceManager) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                _viewModel.RefreshDataSources();
                _viewModel.StatusMessage = Strings.Format("DataSourceCreated", dialog.ResultDataSource?.Name ?? "");
            }
        }

        private void OnEditDataSourceRequested(DataSource dataSource)
        {
            var dialog = new DataSourceDialog(_viewModel.DataSourceManager, dataSource) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                _viewModel.RefreshDataSources();
                _viewModel.StatusMessage = Strings.Format("DataSourceUpdated", dataSource.Name);
            }
        }

        private void OnAddGroupRequested()
        {
            var dialog = new GroupDialog(_viewModel.DataSourceManager) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                _viewModel.RefreshDataSources();
                _viewModel.StatusMessage = Strings.Format("GroupCreated", dialog.ResultGroup?.Name ?? "");
            }
        }

        private void OnEditGroupRequested(DataSourceGroup group)
        {
            var dialog = new GroupDialog(_viewModel.DataSourceManager, group) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                _viewModel.RefreshDataSources();
                _viewModel.StatusMessage = Strings.Format("GroupUpdated", group.Name);
            }
        }
        
        private async void AiSearchButton_Click(object sender, RoutedEventArgs e)
        {
            // First, execute normal search if search term is provided but no results yet
            if (!string.IsNullOrWhiteSpace(_viewModel.SearchTerm) && _viewModel.SearchResults.Count == 0)
            {
                if (_viewModel.SearchCommand.CanExecute(null))
                {
                    _viewModel.SearchCommand.Execute(null);
                    
                    // Wait for search to complete
                    while (_viewModel.IsSearching)
                    {
                        await Task.Delay(100);
                    }
                }
            }
            
            var results = _viewModel.SearchResults.Select(r => r.Result).ToList();
            
            if (results.Count == 0)
            {
                MessageBox.Show(
                    Strings.Instance["AiNoResults"],
                    Strings.Instance.Warning,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            
            // Show prompt dialog
            var promptDialog = new AiSearchPromptDialog(results.Count) { Owner = this };
            if (promptDialog.ShowDialog() != true)
                return;
            
            // Show loading status
            _viewModel.StatusMessage = Strings.Instance["AiAnalyzing"];
            
            try
            {
                var aiService = new AiSearchService();
                var response = await aiService.AnalyzeResultsAsync(
                    results,
                    promptDialog.SystemPrompt,
                    _viewModel.SearchTerm);
                
                // Show result window
                var resultWindow = new AiSearchResultWindow(response) { Owner = this };
                resultWindow.ShowDialog();
                
                _viewModel.StatusMessage = response.Success 
                    ? Strings.Instance["AiAnalysisComplete"]
                    : Strings.Instance.Error;
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"{Strings.Instance.Error}: {ex.Message}";
                MessageBox.Show(
                    ex.Message,
                    Strings.Instance.Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}