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
                _viewModel.RefreshDataSources();
                
                // Scripts laden
                var count = await _scriptRepository.LoadAllAsync();
                if (count > 0)
                {
                    var (success, failed) = await _scriptRepository.CompileAllAsync();
                    _viewModel.RefreshDataSources();
                    
                    if (success > 0)
                    {
                        _viewModel.StatusMessage = Strings.Format("ScriptConnectorsLoaded", success);
                    }
                    else if (failed > 0)
                    {
                        _viewModel.StatusMessage = Strings.Format("CompileFailed", failed);
                    }
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
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"{Strings.Instance.Error}: {ex.Message}";
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
    }
}