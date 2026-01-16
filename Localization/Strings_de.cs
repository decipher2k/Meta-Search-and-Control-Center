//Meta Search and Control Center (c) 2026 Dennis Michael Heine
namespace MSCC.Localization;

/// <summary>
/// Deutsche Übersetzungen.
/// </summary>
public static class Strings_de
{
    public static Dictionary<string, string> Resources { get; } = new()
    {
        // Application
        ["AppTitle"] = "MSCC - Meta Search Command Center",
        ["Ready"] = "Bereit",
        ["Error"] = "Fehler",
        ["Warning"] = "Warnung",
        ["Success"] = "Erfolg",
        ["Cancel"] = "Abbrechen",
        ["Save"] = "Speichern",
        ["Delete"] = "Löschen",
        ["Edit"] = "Bearbeiten",
        ["Add"] = "Hinzufügen",
        ["Close"] = "Schließen",
        ["Yes"] = "Ja",
        ["No"] = "Nein",
        ["OK"] = "OK",

        // Menu
        ["MenuFile"] = "Datei",
        ["MenuExit"] = "Beenden",
        ["MenuPlugins"] = "Plugins",
        ["MenuScriptManager"] = "Script Manager...",
        ["MenuReloadScripts"] = "Scripts neu laden",
        ["MenuSettings"] = "Einstellungen...",
        ["MenuHelp"] = "Hilfe",
        ["MenuAbout"] = "Über MSCC",

        // Search
        ["Search"] = "Suchen",
        ["SearchResults"] = "Suchergebnisse",
        ["Searching"] = "Suche läuft...",
        ["SearchCompleted"] = "Suche abgeschlossen",
        ["NoResults"] = "Keine Ergebnisse gefunden",
        ["ResultsFound"] = "{0} Ergebnisse gefunden",
        ["Source"] = "Quelle",

        // DataSources
        ["DataSources"] = "Datenquellen",
        ["NewDataSource"] = "Neue Datenquelle",
        ["EditDataSource"] = "Datenquelle bearbeiten",
        ["DeleteDataSource"] = "Datenquelle löschen",
        ["DataSourceName"] = "Name",
        ["ConnectorType"] = "Konnektor-Typ",
        ["Configuration"] = "Konfiguration",
        ["Enabled"] = "Aktiviert",
        ["NoDataSourcesSelected"] = "Keine Datenquellen ausgewählt oder verfügbar",

        // Groups
        ["Groups"] = "Gruppen",
        ["NewGroup"] = "Neue Gruppe",
        ["EditGroup"] = "Gruppe bearbeiten",
        ["DeleteGroup"] = "Gruppe löschen",
        ["GroupName"] = "Name",
        ["GroupColor"] = "Farbe",
        ["NoGroup"] = "(Keine Gruppe)",

        // Details
        ["DetailView"] = "Detailansicht",
        ["SelectResultForDetails"] = "Wählen Sie ein Suchergebnis aus, um Details anzuzeigen.",
        ["Open"] = "Öffnen",
        ["OpenFolder"] = "Ordner öffnen",
        ["CopyPath"] = "Pfad kopieren",

        // Labels
        ["AddLabel"] = "Label hinzufügen",
        ["SelectResultFirst"] = "Wählen Sie zuerst ein Ergebnis aus",
        ["KeywordSearch"] = "Keyword-Suche",
        ["SearchByKeyword"] = "Nach Keyword suchen",
        ["CurrentQuery"] = "Aktuelle Abfrage",
        ["SavedQueries"] = "Gespeicherte Abfragen",
        ["Load"] = "Laden",
        ["Labels"] = "Labels",

        // Script Editor
        ["ScriptEditor"] = "Script Editor",
        ["ScriptManager"] = "Script Manager",
        ["NewScript"] = "Neues Script",
        ["Compile"] = "Kompilieren",
        ["Validate"] = "Validieren",
        ["Insert"] = "Einfügen",
        ["Errors"] = "Fehler",
        ["Warnings"] = "Warnungen",
        ["Line"] = "Zeile",
        ["Column"] = "Spalte",
        ["CompileSuccess"] = "Kompilierung erfolgreich - Konnektor registriert",
        ["CompileFailed"] = "Kompilierung fehlgeschlagen - {0} Fehler",

        // Settings
        ["Settings"] = "Einstellungen",
        ["Language"] = "Sprache",
        ["SelectLanguage"] = "Sprache auswählen",
        ["RestartRequired"] = "Einige Änderungen erfordern einen Neustart.",
        ["General"] = "Allgemein",
        ["Appearance"] = "Darstellung",

        // Validation
        ["FieldRequired"] = "Das Feld '{0}' ist erforderlich.",
        ["InvalidValue"] = "Ungültiger Wert",
        ["ValidationError"] = "Validierungsfehler",

        // Status
        ["Loading"] = "Wird geladen...",
        ["Saving"] = "Wird gespeichert...",
        ["Compiling"] = "Kompiliere...",
        ["ScriptConnectorsLoaded"] = "{0} Script-Konnektoren geladen",
        ["DataSourceCreated"] = "Datenquelle '{0}' erstellt",
        ["DataSourceUpdated"] = "Datenquelle '{0}' aktualisiert",
        ["DataSourceDeleted"] = "Datenquelle '{0}' gelöscht",
        ["GroupCreated"] = "Gruppe '{0}' erstellt",
        ["GroupUpdated"] = "Gruppe '{0}' aktualisiert",
        ["GroupDeleted"] = "Gruppe '{0}' gelöscht",

        // Confirmation dialogs
        ["ConfirmDelete"] = "Möchten Sie '{0}' wirklich löschen?",
        ["ConfirmDeleteDataSource"] = "Möchten Sie die Datenquelle '{0}' wirklich löschen?",
        ["ConfirmDeleteGroup"] = "Möchten Sie die Gruppe '{0}' wirklich löschen?\n\nDatenquellen in dieser Gruppe werden nicht gelöscht.",
    };
}
