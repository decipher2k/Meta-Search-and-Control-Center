//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.ComponentModel;
using System.Globalization;

namespace MSCC.Localization;

/// <summary>
/// Unterstützte Sprachen.
/// </summary>
public class LanguageInfo
{
    public string Code { get; init; } = "en";
    public string NativeName { get; init; } = "English";
    public string EnglishName { get; init; } = "English";
}

/// <summary>
/// Stellt lokalisierte Strings für die Anwendung bereit.
/// Unterstützt Sprachwechsel zur Laufzeit.
/// </summary>
public class Strings : INotifyPropertyChanged
{
    private static readonly Strings _instance = new();
    private static Dictionary<string, string> _currentResources;
    private static string _currentLanguage = "en";

    public static Strings Instance => _instance;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Liste aller unterstützten Sprachen.
    /// </summary>
    public static List<LanguageInfo> SupportedLanguages { get; } = new()
    {
        new() { Code = "en", NativeName = "English", EnglishName = "English" },
        new() { Code = "de", NativeName = "Deutsch", EnglishName = "German" },
        new() { Code = "es", NativeName = "Español", EnglishName = "Spanish" },
        new() { Code = "fr", NativeName = "Français", EnglishName = "French" },
        new() { Code = "it", NativeName = "Italiano", EnglishName = "Italian" },
        new() { Code = "hi", NativeName = "??????", EnglishName = "Hindi" },
        new() { Code = "ja", NativeName = "???", EnglishName = "Japanese" },
        new() { Code = "zh", NativeName = "??", EnglishName = "Chinese" },
        new() { Code = "ru", NativeName = "???????", EnglishName = "Russian" },
    };

    static Strings()
    {
        _currentResources = Strings_en.Resources;
    }

    /// <summary>
    /// Wechselt die Sprache zur Laufzeit.
    /// </summary>
    public static void SetLanguage(string languageCode)
    {
        _currentLanguage = languageCode;
        _currentResources = languageCode switch
        {
            "de" => Strings_de.Resources,
            "es" => Strings_es.Resources,
            "fr" => Strings_fr.Resources,
            "it" => Strings_it.Resources,
            "hi" => Strings_hi.Resources,
            "ja" => Strings_ja.Resources,
            "zh" => Strings_zh.Resources,
            "ru" => Strings_ru.Resources,
            _ => Strings_en.Resources
        };

        CultureInfo.CurrentUICulture = new CultureInfo(languageCode);
        _instance.OnPropertyChanged(string.Empty); // Alle Properties aktualisieren
    }

    /// <summary>
    /// Gibt den aktuellen Sprachcode zurück.
    /// </summary>
    public static string CurrentLanguage => _currentLanguage;

    private static string GetString(string key)
    {
        return _currentResources.TryGetValue(key, out var value) ? value : key;
    }

    /// <summary>
    /// Gibt einen formatierten String zurück.
    /// </summary>
    public static string Format(string key, params object[] args)
    {
        var template = GetString(key);
        return string.Format(template, args);
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // === Anwendung ===
    public string AppTitle => GetString("AppTitle");
    public string Ready => GetString("Ready");
    public string Error => GetString("Error");
    public string Warning => GetString("Warning");
    public string Success => GetString("Success");
    public string Cancel => GetString("Cancel");
    public string Save => GetString("Save");
    public string Delete => GetString("Delete");
    public string Edit => GetString("Edit");
    public string Add => GetString("Add");
    public string Close => GetString("Close");
    public string Yes => GetString("Yes");
    public string No => GetString("No");
    public string OK => GetString("OK");

    // === Menü ===
    public string MenuFile => GetString("MenuFile");
    public string MenuExit => GetString("MenuExit");
    public string MenuPlugins => GetString("MenuPlugins");
    public string MenuScriptManager => GetString("MenuScriptManager");
    public string MenuReloadScripts => GetString("MenuReloadScripts");
    public string MenuSettings => GetString("MenuSettings");
    public string MenuHelp => GetString("MenuHelp");
    public string MenuAbout => GetString("MenuAbout");

    // === Suche ===
    public string Search => GetString("Search");
    public string SearchResults => GetString("SearchResults");
    public string Searching => GetString("Searching");
    public string SearchCompleted => GetString("SearchCompleted");
    public string NoResults => GetString("NoResults");
    public string ResultsFound => GetString("ResultsFound");
    public string Source => GetString("Source");

    // === Datenquellen ===
    public string DataSources => GetString("DataSources");
    public string NewDataSource => GetString("NewDataSource");
    public string EditDataSource => GetString("EditDataSource");
    public string DeleteDataSource => GetString("DeleteDataSource");
    public string DataSourceName => GetString("DataSourceName");
    public string ConnectorType => GetString("ConnectorType");
    public string Configuration => GetString("Configuration");
    public string Enabled => GetString("Enabled");
    public string NoDataSourcesSelected => GetString("NoDataSourcesSelected");

    // === Gruppen ===
    public string Groups => GetString("Groups");
    public string NewGroup => GetString("NewGroup");
    public string EditGroup => GetString("EditGroup");
    public string DeleteGroup => GetString("DeleteGroup");
    public string GroupName => GetString("GroupName");
    public string GroupColor => GetString("GroupColor");
    public string NoGroup => GetString("NoGroup");

    // === Details ===
    public string DetailView => GetString("DetailView");
    public string SelectResultForDetails => GetString("SelectResultForDetails");
    public string Open => GetString("Open");
    public string OpenFolder => GetString("OpenFolder");
    public string CopyPath => GetString("CopyPath");

    // === Labels ===
    public string AddLabel => GetString("AddLabel");
    public string SelectResultFirst => GetString("SelectResultFirst");
    public string KeywordSearch => GetString("KeywordSearch");
    public string SearchByKeyword => GetString("SearchByKeyword");
    public string CurrentQuery => GetString("CurrentQuery");
    public string SavedQueries => GetString("SavedQueries");
    public string Load => GetString("Load");
    public string Labels => GetString("Labels");

    // === Script Editor ===
    public string ScriptEditor => GetString("ScriptEditor");
    public string ScriptManager => GetString("ScriptManager");
    public string NewScript => GetString("NewScript");
    public string Compile => GetString("Compile");
    public string Validate => GetString("Validate");
    public string Insert => GetString("Insert");
    public string Errors => GetString("Errors");
    public string Warnings => GetString("Warnings");
    public string Line => GetString("Line");
    public string Column => GetString("Column");
    public string CompileSuccess => GetString("CompileSuccess");
    public string CompileFailed => GetString("CompileFailed");

    // === Einstellungen ===
    public string Settings => GetString("Settings");
    public string Language => GetString("Language");
    public string SelectLanguage => GetString("SelectLanguage");
    public string RestartRequired => GetString("RestartRequired");
    public string General => GetString("General");
    public string Appearance => GetString("Appearance");

    // === Validierung ===
    public string FieldRequired => GetString("FieldRequired");
    public string InvalidValue => GetString("InvalidValue");
    public string ValidationError => GetString("ValidationError");

    // === Status ===
    public string Loading => GetString("Loading");
    public string Saving => GetString("Saving");
    public string Compiling => GetString("Compiling");
    public string ScriptConnectorsLoaded => GetString("ScriptConnectorsLoaded");
    public string DataSourceCreated => GetString("DataSourceCreated");
    public string DataSourceUpdated => GetString("DataSourceUpdated");
    public string DataSourceDeleted => GetString("DataSourceDeleted");
    public string GroupCreated => GetString("GroupCreated");
    public string GroupUpdated => GetString("GroupUpdated");
    public string GroupDeleted => GetString("GroupDeleted");

    // === Bestätigungsdialoge ===
    public string ConfirmDelete => GetString("ConfirmDelete");
    public string ConfirmDeleteDataSource => GetString("ConfirmDeleteDataSource");
    public string ConfirmDeleteGroup => GetString("ConfirmDeleteGroup");
}
