//Meta Search and Control Center (c) 2026 Dennis Michael Heine
namespace MSCC.Localization;

/// <summary>
/// Englische Übersetzungen (Standard/Fallback).
/// </summary>
public static class Strings_en
{
    public static Dictionary<string, string> Resources { get; } = new()
    {
        // Application
        ["AppTitle"] = "MSCC - Meta Search Command Center",
        ["Ready"] = "Ready",
        ["Error"] = "Error",
        ["Warning"] = "Warning",
        ["Success"] = "Success",
        ["Cancel"] = "Cancel",
        ["Save"] = "Save",
        ["Delete"] = "Delete",
        ["Edit"] = "Edit",
        ["Add"] = "Add",
        ["Close"] = "Close",
        ["Yes"] = "Yes",
        ["No"] = "No",
        ["OK"] = "OK",

        // Menu
        ["MenuFile"] = "File",
        ["MenuExit"] = "Exit",
        ["MenuPlugins"] = "Plugins",
        ["MenuScriptManager"] = "Script Manager...",
        ["MenuReloadScripts"] = "Reload Scripts",
        ["MenuSettings"] = "Settings...",
        ["MenuHelp"] = "Help",
        ["MenuAbout"] = "About MSCC",

        // Search
        ["Search"] = "Search",
        ["SearchResults"] = "Search Results",
        ["Searching"] = "Searching...",
        ["SearchCompleted"] = "Search completed",
        ["NoResults"] = "No results found",
        ["ResultsFound"] = "{0} results found",
        ["Source"] = "Source",

        // DataSources
        ["DataSources"] = "Data Sources",
        ["NewDataSource"] = "New Data Source",
        ["EditDataSource"] = "Edit Data Source",
        ["DeleteDataSource"] = "Delete Data Source",
        ["DataSourceName"] = "Name",
        ["ConnectorType"] = "Connector Type",
        ["Configuration"] = "Configuration",
        ["Enabled"] = "Enabled",
        ["NoDataSourcesSelected"] = "No data sources selected or available",

        // Groups
        ["Groups"] = "Groups",
        ["NewGroup"] = "New Group",
        ["EditGroup"] = "Edit Group",
        ["DeleteGroup"] = "Delete Group",
        ["GroupName"] = "Name",
        ["GroupColor"] = "Color",
        ["NoGroup"] = "(No Group)",

        // Details
        ["DetailView"] = "Detail View",
        ["SelectResultForDetails"] = "Select a search result to view details.",
        ["Open"] = "Open",
        ["OpenFolder"] = "Open Folder",
        ["CopyPath"] = "Copy Path",

        // Labels
        ["AddLabel"] = "Add Label",
        ["SelectResultFirst"] = "Select a result first",
        ["KeywordSearch"] = "Keyword Search",
        ["SearchByKeyword"] = "Search by Keyword",
        ["CurrentQuery"] = "Current Query",
        ["SavedQueries"] = "Saved Queries",
        ["Load"] = "Load",
        ["Labels"] = "Labels",

        // Script Editor
        ["ScriptEditor"] = "Script Editor",
        ["ScriptManager"] = "Script Manager",
        ["NewScript"] = "New Script",
        ["Compile"] = "Compile",
        ["Validate"] = "Validate",
        ["Insert"] = "Insert",
        ["Errors"] = "Errors",
        ["Warnings"] = "Warnings",
        ["Line"] = "Line",
        ["Column"] = "Column",
        ["CompileSuccess"] = "Compilation successful - Connector registered",
        ["CompileFailed"] = "Compilation failed - {0} errors",

        // Settings
        ["Settings"] = "Settings",
        ["Language"] = "Language",
        ["SelectLanguage"] = "Select Language",
        ["RestartRequired"] = "Some changes require a restart to take effect.",
        ["General"] = "General",
        ["Appearance"] = "Appearance",

        // Validation
        ["FieldRequired"] = "The field '{0}' is required.",
        ["InvalidValue"] = "Invalid value",
        ["ValidationError"] = "Validation Error",

        // Status
        ["Loading"] = "Loading...",
        ["Saving"] = "Saving...",
        ["Compiling"] = "Compiling...",
        ["ScriptConnectorsLoaded"] = "{0} script connectors loaded",
        ["DataSourceCreated"] = "Data source '{0}' created",
        ["DataSourceUpdated"] = "Data source '{0}' updated",
        ["DataSourceDeleted"] = "Data source '{0}' deleted",
        ["GroupCreated"] = "Group '{0}' created",
        ["GroupUpdated"] = "Group '{0}' updated",
        ["GroupDeleted"] = "Group '{0}' deleted",

        // Confirmation dialogs
        ["ConfirmDelete"] = "Are you sure you want to delete '{0}'?",
        ["ConfirmDeleteDataSource"] = "Do you really want to delete the data source '{0}'?",
        ["ConfirmDeleteGroup"] = "Do you really want to delete the group '{0}'?\n\nData sources in this group will not be deleted.",
    };
}
