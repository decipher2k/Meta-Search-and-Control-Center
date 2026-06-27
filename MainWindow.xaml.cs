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
        private readonly McpServerService _mcpServerService;
        private bool _isInitialized;
        private bool _isAutoAnalyzingKeywordResults;

        public MainWindow()
        {
            // Sprache beim Start anwenden
            SettingsService.Instance.ApplyLanguage();
            
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            _mcpServerService = new McpServerService(_viewModel.DataSourceManager);

            // Scripting initialisieren
            _scriptingService = new ScriptingService();
            _scriptRepository = new ScriptRepository(_scriptingService);

            // Detail-Ansicht initialisieren
            _detailView = new ResultDetailView(_viewModel.DataSourceManager);
            DetailViewContainer.Content = _detailView;

            // Subscribe to property changes for detail view updates
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _viewModel.KeywordSearchCompleted += OnKeywordSearchCompleted;

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
            Closed += (s, e) => _mcpServerService.Dispose();
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
                await ApplyMcpServerSettingsAsync();
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
            SearchButton.Content = "Keyword Search";
            AiSearchButton.Content = "AI Search";
            AiSearchButton.ToolTip = loc["LiveRagTooltip"];
            
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
            if (dialog.ShowDialog() == true)
            {
                _ = ApplyMcpServerSettingsAsync();
            }
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

        private async Task ApplyMcpServerSettingsAsync()
        {
            var settings = SettingsService.Instance.Settings;
            if (!settings.McpServerEnabled)
            {
                _mcpServerService.Stop();
                return;
            }

            var started = await _mcpServerService.StartAsync(settings.McpServerPort);
            if (!started)
            {
                _viewModel.StatusMessage = $"{Strings.Instance.Error}: {Strings.Instance["McpServerStartFailed"]}";
                return;
            }

            if (_isInitialized)
            {
                _viewModel.StatusMessage = $"{Strings.Instance.Ready} - MCP: {_mcpServerService.EndpointUrl}";
            }
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
        
        private async void OnKeywordSearchCompleted(IReadOnlyList<SearchResult> results, string query)
        {
            await AutoAnalyzeKeywordResultsAsync(results, query);
        }

        private async Task AutoAnalyzeKeywordResultsAsync(IReadOnlyList<SearchResult> results, string query)
        {
            if (_isAutoAnalyzingKeywordResults || results.Count == 0 || !AiSearchService.HasConfiguredAiSettings())
                return;

            _isAutoAnalyzingKeywordResults = true;
            _viewModel.StatusMessage = Strings.Instance["AiAnalyzing"];

            try
            {
                var aiService = new AiSearchService();
                var response = await aiService.AnalyzeResultsAsync(
                    results,
                    AiSearchService.DefaultSearchResultsAnalysisPrompt,
                    query);

                if (response.Success)
                {
                    var resultWindow = new AiSearchResultWindow(response) { Owner = this };
                    resultWindow.ShowDialog();
                    _viewModel.StatusMessage = Strings.Instance["AiAnalysisComplete"];
                }
                else
                {
                    _viewModel.StatusMessage = $"{Strings.Instance.Error}: {response.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"{Strings.Instance.Error}: {ex.Message}";
            }
            finally
            {
                _isAutoAnalyzingKeywordResults = false;
            }
        }

        private async void AiSearchButton_Click(object sender, RoutedEventArgs e)
        {
            var (selectedDataSourceIds, selectedGroupIds) = GetSelectedSearchScope();
            var selectedSources = ResolveSelectedDataSources(selectedDataSourceIds, selectedGroupIds);

            if (selectedSources.Count == 0)
            {
                MessageBox.Show(
                    Strings.Instance.NoDataSourcesSelected,
                    Strings.Instance.Warning,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var dialog = new LiveRagQueryDialog(selectedSources.Count, _viewModel.SearchTerm)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
                return;

            using var cancellation = new CancellationTokenSource();
            var aiService = new AiSearchService();
            var orchestrator = new LiveRagOrchestrator(_viewModel.DataSourceManager, aiService);
            IProgress<(string query, string sourceName, int contextCount, bool isNativeLiveRag)>? liveProgress = null;

            try
            {
                _viewModel.SearchTerm = dialog.Question;
                _viewModel.StatusMessage = Strings.Instance["LiveRagPlanning"];

                liveProgress = new Progress<(string query, string sourceName, int contextCount, bool isNativeLiveRag)>(p =>
                {
                    var mode = p.isNativeLiveRag ? Strings.Instance["LiveRagNativeMode"] : Strings.Instance["LiveRagFallbackMode"];
                    _viewModel.StatusMessage = string.Format(
                        Strings.Instance["LiveRagToolContextFrom"],
                        p.query,
                        p.sourceName,
                        p.contextCount,
                        mode);
                });

                var context = await orchestrator.GetLiveRagContextAsync(
                    dialog.Question,
                    selectedDataSourceIds,
                    selectedGroupIds,
                    maxResultsPerOperation: dialog.MaxResultsPerSearchTerm,
                    maxContextItemsPerSource: dialog.MaxContextItemsPerSource,
                    maxContextItemsTotal: dialog.MaxContextItemsTotal,
                    maxCharactersPerItem: dialog.MaxCharactersPerItem,
                    includeMetadata: dialog.IncludeMetadata,
                    useAiPlanning: true,
                    cancellationToken: cancellation.Token,
                    progress: new Progress<(string sourceName, int contextCount, bool isNativeLiveRag)>(p =>
                    {
                        liveProgress.Report((dialog.Question, p.sourceName, p.contextCount, p.isNativeLiveRag));
                    }),
                    statusProgress: new Progress<string>(message =>
                    {
                        _viewModel.StatusMessage = message;
                    }));

                DisplayLiveRagContextAsResults(context, selectedDataSourceIds, selectedGroupIds);

                if (context.ContextItems.Count == 0)
                {
                    var message = AiSearchService.BuildLiveRagFailureMessage(context);
                    MessageBox.Show(
                        message,
                        Strings.Instance["LiveRagSearch"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    _viewModel.StatusMessage = string.Format(
                        Strings.Instance["LiveRagComplete"],
                        0);
                    return;
                }

                _viewModel.StatusMessage = Strings.Instance["LiveRagAnswering"];
                var response = await aiService.AnswerLiveRagAsync(
                    context,
                    dialog.SystemPrompt,
                    cancellation.Token);
                response.LiveRagContext = context;
                response.ToolCallCount = context.ExecutionTrace.Count(trace => trace.Accepted);

                if (!response.Success)
                {
                    MessageBox.Show(
                        response.ErrorMessage ?? Strings.Instance.Error,
                        Strings.Instance.Error,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    _viewModel.StatusMessage = Strings.Instance.Error;
                    return;
                }

                var resultWindow = new AiSearchResultWindow(response) { Owner = this };
                resultWindow.ShowDialog();

                _viewModel.StatusMessage = response.Success
                    ? string.Format(Strings.Instance["LiveRagComplete"], response.LiveRagContext?.ContextItems.Count ?? 0)
                    : Strings.Instance.Error;
            }
            catch (OperationCanceledException)
            {
                _viewModel.StatusMessage = Strings.Instance["LiveRagCancelled"];
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

        private (List<string> dataSourceIds, List<string> groupIds) GetSelectedSearchScope()
        {
            var selectedDataSourceIds = _viewModel.DataSources
                .Where(ds => ds.IsSelected && ds.DataSource.IsEnabled)
                .Select(ds => ds.DataSource.Id)
                .ToList();

            var selectedGroupIds = _viewModel.Groups
                .Where(g => g.IsSelected)
                .Select(g => g.Group.Id)
                .ToList();

            return (selectedDataSourceIds, selectedGroupIds);
        }

        private static List<DataSource> ResolveSelectedDataSources(
            IEnumerable<string> dataSourceIds,
            IEnumerable<string> groupIds)
        {
            var resolvedIds = new HashSet<string>(dataSourceIds);
            foreach (var groupId in resolvedIds.Count == 0 ? groupIds : Enumerable.Empty<string>())
            {
                foreach (var dataSource in GlobalState.GetDataSourcesByGroup(groupId))
                {
                    if (dataSource.IsEnabled)
                    {
                        resolvedIds.Add(dataSource.Id);
                    }
                }
            }

            return GlobalState.DataSources
                .Where(ds => ds.IsEnabled && resolvedIds.Contains(ds.Id))
                .ToList();
        }

        private void DisplayLiveRagContextAsResults(
            LiveRagContextResult context,
            IEnumerable<string> selectedDataSourceIds,
            IEnumerable<string> selectedGroupIds)
        {
            _viewModel.SearchResults.Clear();
            _viewModel.CurrentQuery = new SearchQuery
            {
                SearchTerm = context.Question,
                SelectedDataSourceIds = selectedDataSourceIds.ToList(),
                SelectedGroupIds = selectedGroupIds.ToList(),
                LastExecutedAt = DateTime.Now,
                Description = "Live RAG"
            };
            GlobalState.CurrentQuery = _viewModel.CurrentQuery;

            foreach (var item in context.ContextItems)
            {
                var metadata = new Dictionary<string, object>(item.Metadata)
                {
                    ["LiveRagRetrievalQuery"] = item.RetrievalQuery ?? string.Empty,
                    ["LiveRagOperationId"] = item.OperationId ?? string.Empty,
                    ["LiveRagOperationType"] = item.OperationType?.ToString() ?? string.Empty,
                    ["LiveRagNative"] = item.FromNativeLiveRag,
                    ["LiveRagMode"] = context.Mode.ToString(),
                    ["LiveRagDegradedFallback"] = context.IsDegradedFallback,
                    ["LiveRagRetrievedAt"] = item.RetrievedAt.ToString("u")
                };

                var searchResult = new SearchResult
                {
                    Title = item.Title,
                    Description = item.Content,
                    SourceName = item.SourceName,
                    ConnectorId = item.ConnectorId,
                    OriginalReference = item.OriginalReference,
                    RelevanceScore = item.RelevanceScore,
                    Metadata = metadata,
                    FoundAt = item.RetrievedAt
                };

                _viewModel.SearchResults.Add(new LabelableSearchResult(searchResult));
            }
        }
    }
}
