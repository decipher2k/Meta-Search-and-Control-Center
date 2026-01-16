//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using MSCC.Localization;
using MSCC.Models;
using MSCC.Services;

namespace MSCC.Views;

/// <summary>
/// Farbauswahl-Element.
/// </summary>
public class ColorItem
{
    public string Name { get; set; } = string.Empty;
    public Color Color { get; set; }
    public string HexColor => $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}";
}

/// <summary>
/// Dialog zum Erstellen und Bearbeiten von Gruppen.
/// </summary>
public partial class GroupDialog : Window
{
    private readonly DataSourceManager _dataSourceManager;
    private readonly DataSourceGroup? _existingGroup;
    private readonly bool _isEditMode;
    private readonly List<ColorItem> _colors;

    public DataSourceGroup? ResultGroup { get; private set; }

    public GroupDialog(DataSourceManager dataSourceManager, DataSourceGroup? existingGroup = null)
    {
        InitializeComponent();
        
        _dataSourceManager = dataSourceManager;
        _existingGroup = existingGroup;
        _isEditMode = existingGroup != null;

        Title = _isEditMode ? "Gruppe bearbeiten" : "Neue Gruppe";

        _colors = new List<ColorItem>
        {
            new() { Name = "Blau", Color = (Color)ColorConverter.ConvertFromString("#3498DB") },
            new() { Name = "Rot", Color = (Color)ColorConverter.ConvertFromString("#E74C3C") },
            new() { Name = "Grün", Color = (Color)ColorConverter.ConvertFromString("#27AE60") },
            new() { Name = "Orange", Color = (Color)ColorConverter.ConvertFromString("#E67E22") },
            new() { Name = "Lila", Color = (Color)ColorConverter.ConvertFromString("#9B59B6") },
            new() { Name = "Türkis", Color = (Color)ColorConverter.ConvertFromString("#1ABC9C") },
            new() { Name = "Gelb", Color = (Color)ColorConverter.ConvertFromString("#F1C40F") },
            new() { Name = "Pink", Color = (Color)ColorConverter.ConvertFromString("#E91E63") },
            new() { Name = "Grau", Color = (Color)ColorConverter.ConvertFromString("#95A5A6") },
            new() { Name = "Dunkelblau", Color = (Color)ColorConverter.ConvertFromString("#2C3E50") }
        };

        ColorComboBox.ItemsSource = _colors;
        ColorComboBox.SelectionChanged += (s, e) => UpdateColorPreview();
        ColorComboBox.SelectedIndex = 0;
        
        // Auf Sprachwechsel reagieren
        Strings.Instance.PropertyChanged += OnStringsPropertyChanged;
        ApplyLocalization();
        
        if (_isEditMode)
        {
            LoadExistingGroup();
        }
    }

    private void OnStringsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        var loc = Strings.Instance;
        
        Title = _isEditMode ? loc.EditGroup : loc.NewGroup;
        NameLabel.Text = loc.GroupName + ":";
        ColorLabel.Text = loc.GroupColor + ":";
        CancelButton.Content = loc.Cancel;
        SaveButton.Content = loc.Save;
    }

    private void LoadExistingGroup()
    {
        if (_existingGroup == null) return;

        NameTextBox.Text = _existingGroup.Name;
        DescriptionTextBox.Text = _existingGroup.Description;

        // Farbe auswählen
        var matchingColor = _colors.FirstOrDefault(c => 
            c.HexColor.Equals(_existingGroup.Color, StringComparison.OrdinalIgnoreCase));
        
        if (matchingColor != null)
        {
            ColorComboBox.SelectedItem = matchingColor;
        }
    }

    private void UpdateColorPreview()
    {
        if (ColorComboBox.SelectedItem is ColorItem colorItem)
        {
            ColorPreview.Fill = new SolidColorBrush(colorItem.Color);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var loc = Strings.Instance;
        
        // Validierung
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            MessageBox.Show(loc.FieldRequired, loc.ValidationError, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var colorItem = ColorComboBox.SelectedItem as ColorItem;
        var color = colorItem?.HexColor ?? "#3498DB";

        if (_isEditMode && _existingGroup != null)
        {
            // Update
            var success = _dataSourceManager.UpdateGroup(
                _existingGroup.Id,
                NameTextBox.Text,
                DescriptionTextBox.Text,
                color);

            if (success)
            {
                ResultGroup = _existingGroup;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show(loc.Error, loc.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            // Create
            var group = _dataSourceManager.CreateGroup(
                NameTextBox.Text,
                DescriptionTextBox.Text,
                color);

            ResultGroup = group;
            DialogResult = true;
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
