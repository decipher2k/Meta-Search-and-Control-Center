//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.ComponentModel;
using System.Windows;
using MSCC.Connectors;
using MSCC.Localization;
using MSCC.Models;
using MSCC.Services;

namespace MSCC.Views;

/// <summary>
/// Konfigurationsparameter mit Wert für die UI.
/// </summary>
public class ConfigParameterValue
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Dialog zum Erstellen und Bearbeiten von Datenquellen.
/// </summary>
public partial class DataSourceDialog : Window
{
    private readonly DataSourceManager _dataSourceManager;
    private readonly DataSource? _existingDataSource;
    private readonly bool _isEditMode;
    private List<ConfigParameterValue> _configParameters = new();

    public DataSource? ResultDataSource { get; private set; }

    public DataSourceDialog(DataSourceManager dataSourceManager, DataSource? existingDataSource = null)
    {
        InitializeComponent();
        
        _dataSourceManager = dataSourceManager;
        _existingDataSource = existingDataSource;
        _isEditMode = existingDataSource != null;
        
        // Auf Sprachwechsel reagieren
        Strings.Instance.PropertyChanged += OnStringsPropertyChanged;
        ApplyLocalization();
        
        LoadConnectors();
        LoadGroups();
        
        if (_isEditMode)
        {
            LoadExistingDataSource();
        }
    }

    private void OnStringsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        var loc = Strings.Instance;
        
        Title = _isEditMode ? loc.EditDataSource : loc.NewDataSource;
        NameLabel.Text = loc.DataSourceName + ":";
        ConnectorLabel.Text = loc.ConnectorType + ":";
        GroupLabel.Text = loc.Groups + ":";
        IsEnabledCheckBox.Content = loc.Enabled;
        ConfigLabel.Text = loc.Configuration + ":";
        CancelButton.Content = loc.Cancel;
        SaveButton.Content = loc.Save;
    }

    private void LoadConnectors()
    {
        var connectors = _dataSourceManager.GetAvailableConnectors().ToList();
        ConnectorComboBox.ItemsSource = connectors;
        
        if (_isEditMode)
        {
            // Im Bearbeitungsmodus kann der Konnektor nicht geändert werden
            ConnectorPanel.Visibility = Visibility.Collapsed;
        }
        else if (connectors.Count > 0)
        {
            ConnectorComboBox.SelectedIndex = 0;
        }
    }

    private void LoadGroups()
    {
        var groups = _dataSourceManager.GetAllGroups().ToList();
        
        // Füge "Keine Gruppe" Option hinzu
        var noGroup = new DataSourceGroup { Id = "", Name = "(Keine Gruppe)", Color = "#95A5A6" };
        groups.Insert(0, noGroup);
        
        GroupComboBox.ItemsSource = groups;
        GroupComboBox.SelectedIndex = 0;
    }

    private void LoadExistingDataSource()
    {
        if (_existingDataSource == null) return;

        NameTextBox.Text = _existingDataSource.Name;
        IsEnabledCheckBox.IsChecked = _existingDataSource.IsEnabled;

        // Gruppe auswählen
        var groups = GroupComboBox.ItemsSource as List<DataSourceGroup>;
        var selectedGroup = groups?.FirstOrDefault(g => g.Id == _existingDataSource.GroupId);
        if (selectedGroup != null)
        {
            GroupComboBox.SelectedItem = selectedGroup;
        }

        // Konfiguration laden
        if (GlobalState.Connectors.TryGetValue(_existingDataSource.ConnectorId, out var connector))
        {
            LoadConfigurationParameters(connector, _existingDataSource.Configuration);
        }
    }

    private void ConnectorComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ConnectorComboBox.SelectedItem is IDataSourceConnector connector)
        {
            LoadConfigurationParameters(connector, null);
        }
    }

    private void LoadConfigurationParameters(IDataSourceConnector connector, Dictionary<string, string>? existingConfig)
    {
        _configParameters = connector.ConfigurationParameters.Select(p => new ConfigParameterValue
        {
            Name = p.Name,
            DisplayName = p.DisplayName,
            Description = p.Description,
            IsRequired = p.IsRequired,
            Value = existingConfig?.GetValueOrDefault(p.Name, p.DefaultValue ?? "") ?? p.DefaultValue ?? ""
        }).ToList();

        ConfigItemsControl.ItemsSource = _configParameters;
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Validierung
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            MessageBox.Show("Bitte geben Sie einen Namen ein.", "Validierungsfehler", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Pflichtfelder prüfen
        foreach (var param in _configParameters.Where(p => p.IsRequired))
        {
            if (string.IsNullOrWhiteSpace(param.Value))
            {
                MessageBox.Show($"Das Feld '{param.DisplayName}' ist erforderlich.", "Validierungsfehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        // UI-Werte vor dem async-Aufruf erfassen
        var name = NameTextBox.Text;
        var isEnabled = IsEnabledCheckBox.IsChecked ?? true;
        var configuration = _configParameters.ToDictionary(p => p.Name, p => p.Value);
        var groupId = (GroupComboBox.SelectedItem as DataSourceGroup)?.Id;
        if (string.IsNullOrEmpty(groupId)) groupId = null;
        var connector = ConnectorComboBox.SelectedItem as IDataSourceConnector;

        try
        {
            if (_isEditMode && _existingDataSource != null)
            {
                // Update
                var success = await _dataSourceManager.UpdateDataSourceAsync(
                    _existingDataSource.Id,
                    name,
                    configuration,
                    groupId ?? string.Empty,
                    isEnabled);

                if (success)
                {
                    ResultDataSource = _existingDataSource;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Fehler beim Aktualisieren der Datenquelle.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // Create
                if (connector == null)
                {
                    MessageBox.Show("Bitte wählen Sie einen Konnektor aus.", "Validierungsfehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dataSource = await _dataSourceManager.CreateDataSourceAsync(
                    name,
                    connector.Id,
                    configuration,
                    groupId);

                if (dataSource != null)
                {
                    ResultDataSource = dataSource;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Fehler beim Erstellen der Datenquelle. Überprüfen Sie die Konfiguration.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
