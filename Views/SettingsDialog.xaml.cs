//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MSCC.Localization;
using MSCC.Services;

namespace MSCC.Views;

/// <summary>
/// Dialog für Anwendungseinstellungen.
/// </summary>
public partial class SettingsDialog : Window
{
    private readonly SettingsService _settingsService;
    private readonly string _originalLanguage;
    private bool _languageChanged;

    public SettingsDialog()
    {
        InitializeComponent();
        
        _settingsService = SettingsService.Instance;
        _originalLanguage = _settingsService.Settings.Language;
        
        // Auf Sprachwechsel reagieren
        Strings.Instance.PropertyChanged += OnStringsPropertyChanged;
        
        LoadSettings();
        ApplyLocalization();
    }

    private void OnStringsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        var loc = Strings.Instance;
        
        Title = loc.Settings;
        GeneralHeader.Text = loc.General;
        LanguageLabel.Text = loc.Language;
        RestartHintText.Text = loc.RestartRequired;
        AppearanceHeader.Text = loc.Appearance;
        CancelBtn.Content = loc.Cancel;
        SaveBtn.Content = loc.Save;
    }

    private void LoadSettings()
    {
        // Sprachen laden
        LanguageComboBox.ItemsSource = Strings.SupportedLanguages;
        
        // Aktuelle Sprache auswählen
        var currentLang = Strings.SupportedLanguages.FirstOrDefault(l => l.Code == _settingsService.Settings.Language);
        LanguageComboBox.SelectedItem = currentLang ?? Strings.SupportedLanguages.First();
        
        // Andere Einstellungen
        StartMaximizedCheckBox.IsChecked = _settingsService.Settings.StartMaximized;
        RememberPositionCheckBox.IsChecked = _settingsService.Settings.RememberWindowPosition;
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is LanguageInfo selectedLang)
        {
            // Sprache sofort wechseln für Live-Vorschau
            Strings.SetLanguage(selectedLang.Code);
            
            _languageChanged = selectedLang.Code != _originalLanguage;
            RestartHintBorder.Visibility = _languageChanged ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Sprache speichern
        if (LanguageComboBox.SelectedItem is LanguageInfo selectedLang)
        {
            _settingsService.Settings.Language = selectedLang.Code;
        }
        
        // Andere Einstellungen speichern
        _settingsService.Settings.StartMaximized = StartMaximizedCheckBox.IsChecked ?? false;
        _settingsService.Settings.RememberWindowPosition = RememberPositionCheckBox.IsChecked ?? true;
        
        _settingsService.Save();
        
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        // Sprache zurücksetzen wenn geändert
        if (_languageChanged)
        {
            Strings.SetLanguage(_originalLanguage);
        }
        
        DialogResult = false;
        Close();
    }
}
