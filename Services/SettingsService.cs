//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.IO;
using System.Text.Json;

namespace MSCC.Services;

/// <summary>
/// Anwendungseinstellungen.
/// </summary>
public class AppSettings
{
    public string Language { get; set; } = "en";
    public bool StartMaximized { get; set; } = false;
    public bool RememberWindowPosition { get; set; } = true;
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 700;
    public double WindowLeft { get; set; } = -1;
    public double WindowTop { get; set; } = -1;
}

/// <summary>
/// Service zum Laden und Speichern von Einstellungen.
/// </summary>
public class SettingsService
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MSCC");
    
    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static SettingsService? _instance;
    private AppSettings _settings;

    public static SettingsService Instance => _instance ??= new SettingsService();

    public AppSettings Settings => _settings;

    private SettingsService()
    {
        _settings = Load();
    }

    /// <summary>
    /// Lädt die Einstellungen aus der Datei.
    /// </summary>
    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    _settings = settings;
                    return settings;
                }
            }
        }
        catch
        {
            // Bei Fehlern Standardeinstellungen verwenden
        }

        _settings = new AppSettings();
        return _settings;
    }

    /// <summary>
    /// Speichert die aktuellen Einstellungen.
    /// </summary>
    public bool Save()
    {
        try
        {
            if (!Directory.Exists(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
            }

            var json = JsonSerializer.Serialize(_settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Setzt die Sprache und speichert die Einstellungen.
    /// </summary>
    public void SetLanguage(string languageCode)
    {
        _settings.Language = languageCode;
        Localization.Strings.SetLanguage(languageCode);
        Save();
    }

    /// <summary>
    /// Wendet die gespeicherte Sprache an.
    /// </summary>
    public void ApplyLanguage()
    {
        Localization.Strings.SetLanguage(_settings.Language);
    }
}
