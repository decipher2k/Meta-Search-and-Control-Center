// Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.ComponentModel;
using System.Windows;
using MSCC.Connectors;
using MSCC.Models;
using MSCC.Scripting;
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

            // Scripts beim Start laden
            Loaded += async (s, e) => await LoadScriptsAsync();
        }

        private async Task LoadScriptsAsync()
        {
            try
            {
                var count = await _scriptRepository.LoadAllAsync();
                if (count > 0)
                {
                    var (success, failed) = await _scriptRepository.CompileAllAsync();
                    _viewModel.RefreshDataSources();
                    
                    if (success > 0)
                    {
                        _viewModel.StatusMessage = $"{success} Script-Konnektoren geladen - neue Datenquellen koennen erstellt werden";
                    }
                    else if (failed > 0)
                    {
                        _viewModel.StatusMessage = $"Fehler beim Kompilieren von {failed} Script(s)";
                    }
                }
                else
                {
                    _viewModel.StatusMessage = "Bereit";
                }
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"Fehler beim Laden der Scripts: {ex.Message}";
            }
        }

        // Menu Event Handlers
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
            _viewModel.StatusMessage = "Lade Scripts neu...";
            await LoadScriptsAsync();
        }

        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MenuItem_About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "MSCC - Meta Search Command Center\n\nVersion 1.0.0\n\nEine Metasuchmaschine mit Plugin-Konnektoren.",
                "Ueber MSCC",
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
                _viewModel.StatusMessage = $"Datenquelle '{dialog.ResultDataSource?.Name}' erstellt";
            }
        }

        private void OnEditDataSourceRequested(DataSource dataSource)
        {
            var dialog = new DataSourceDialog(_viewModel.DataSourceManager, dataSource) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                _viewModel.RefreshDataSources();
                _viewModel.StatusMessage = $"Datenquelle '{dataSource.Name}' aktualisiert";
            }
        }

        private void OnAddGroupRequested()
        {
            var dialog = new GroupDialog(_viewModel.DataSourceManager) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                _viewModel.RefreshDataSources();
                _viewModel.StatusMessage = $"Gruppe '{dialog.ResultGroup?.Name}' erstellt";
            }
        }

        private void OnEditGroupRequested(DataSourceGroup group)
        {
            var dialog = new GroupDialog(_viewModel.DataSourceManager, group) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                _viewModel.RefreshDataSources();
                _viewModel.StatusMessage = $"Gruppe '{group.Name}' aktualisiert";
            }
        }
    }
}