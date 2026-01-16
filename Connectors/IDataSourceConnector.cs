//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Windows;
using MSCC.Models;

namespace MSCC.Connectors;

/// <summary>
/// Konfigurationsparameter-Definition für einen Konnektor.
/// </summary>
public class ConnectorParameter
{
    /// <summary>
    /// Name des Parameters.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Anzeigename des Parameters.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Beschreibung des Parameters.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Typ des Parameters (string, int, bool, path, etc.).
    /// </summary>
    public string ParameterType { get; set; } = "string";

    /// <summary>
    /// Gibt an, ob der Parameter erforderlich ist.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Standardwert des Parameters.
    /// </summary>
    public string? DefaultValue { get; set; }
}

/// <summary>
/// Definiert den Anzeigetyp für die Detailansicht.
/// </summary>
public enum DetailViewType
{
    /// <summary>Standard-Textansicht mit Eigenschaften.</summary>
    Default,
    /// <summary>Tabellarische Darstellung.</summary>
    Table,
    /// <summary>Diagramm/Chart-Darstellung.</summary>
    Chart,
    /// <summary>Graph/Netzwerk-Darstellung.</summary>
    Graph,
    /// <summary>Bild/Medien-Vorschau.</summary>
    Media,
    /// <summary>Dokument-Vorschau.</summary>
    Document,
    /// <summary>Benutzerdefiniertes WPF-Control.</summary>
    Custom
}

/// <summary>
/// Definition einer Spalte für Tabellen-Ansicht.
/// </summary>
public class TableColumnDefinition
{
    /// <summary>Name der Eigenschaft in den Metadaten.</summary>
    public string PropertyName { get; set; } = string.Empty;
    /// <summary>Anzeigename der Spalte.</summary>
    public string Header { get; set; } = string.Empty;
    /// <summary>Breite der Spalte (z.B. "Auto", "100", "*").</summary>
    public string Width { get; set; } = "Auto";
    /// <summary>Format-String für die Anzeige.</summary>
    public string? Format { get; set; }
}

/// <summary>
/// Definition für Chart-Darstellung.
/// </summary>
public class ChartDefinition
{
    /// <summary>Typ des Charts (Bar, Line, Pie, etc.).</summary>
    public string ChartType { get; set; } = "Bar";
    /// <summary>Eigenschaft für die X-Achse/Kategorien.</summary>
    public string CategoryProperty { get; set; } = string.Empty;
    /// <summary>Eigenschaft für die Y-Achse/Werte.</summary>
    public string ValueProperty { get; set; } = string.Empty;
    /// <summary>Titel des Charts.</summary>
    public string? Title { get; set; }
}

/// <summary>
/// Konfiguration der Detailansicht für ein Suchergebnis.
/// </summary>
public class DetailViewConfiguration
{
    /// <summary>Typ der Detailansicht.</summary>
    public DetailViewType ViewType { get; set; } = DetailViewType.Default;
    
    /// <summary>Eigenschaften, die in der Detailansicht angezeigt werden sollen.</summary>
    public List<string> DisplayProperties { get; set; } = new();
    
    /// <summary>Spalten-Definitionen für Tabellen-Ansicht.</summary>
    public List<TableColumnDefinition> TableColumns { get; set; } = new();
    
    /// <summary>Chart-Definition für Diagramm-Ansicht.</summary>
    public ChartDefinition? ChartConfig { get; set; }
    
    /// <summary>Pfad zur Medien-Vorschau (aus Metadaten).</summary>
    public string? MediaPathProperty { get; set; }
    
    /// <summary>Aktionen, die auf dem Ergebnis ausgeführt werden können.</summary>
    public List<ResultAction> Actions { get; set; } = new();
}

/// <summary>
/// Definiert eine Aktion, die auf einem Suchergebnis ausgeführt werden kann.
/// </summary>
public class ResultAction
{
    /// <summary>Eindeutige ID der Aktion.</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>Anzeigename der Aktion.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Icon für die Aktion (Emoji oder Icon-Name).</summary>
    public string? Icon { get; set; }
    /// <summary>Beschreibung der Aktion.</summary>
    public string? Description { get; set; }
}

/// <summary>
/// Schnittstelle für Datenquellen-Konnektoren (Plugins).
/// </summary>
public interface IDataSourceConnector
{
    /// <summary>
    /// Eindeutige ID des Konnektors.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Anzeigename des Konnektors.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Beschreibung des Konnektors.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Version des Konnektors.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Definiert die Konfigurationsparameter, die dieser Konnektor benötigt.
    /// </summary>
    IEnumerable<ConnectorParameter> ConfigurationParameters { get; }

    /// <summary>
    /// Initialisiert den Konnektor mit der angegebenen Konfiguration.
    /// </summary>
    /// <param name="configuration">Konfigurationseinstellungen.</param>
    /// <returns>True, wenn die Initialisierung erfolgreich war.</returns>
    Task<bool> InitializeAsync(Dictionary<string, string> configuration);

    /// <summary>
    /// Führt eine Suche in der Datenquelle durch.
    /// </summary>
    /// <param name="searchTerm">Der Suchbegriff.</param>
    /// <param name="maxResults">Maximale Anzahl der Ergebnisse.</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Suche.</param>
    /// <returns>Liste der Suchergebnisse.</returns>
    Task<IEnumerable<SearchResult>> SearchAsync(
        string searchTerm, 
        int maxResults = 100, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Testet die Verbindung zur Datenquelle.
    /// </summary>
    /// <returns>True, wenn die Verbindung erfolgreich ist.</returns>
    Task<bool> TestConnectionAsync();

    /// <summary>
    /// Holt die Konfiguration für die Detailansicht eines Suchergebnisses.
    /// </summary>
    /// <param name="result">Das Suchergebnis.</param>
    /// <returns>Konfiguration der Detailansicht.</returns>
    DetailViewConfiguration GetDetailViewConfiguration(SearchResult result);

    /// <summary>
    /// Erstellt ein benutzerdefiniertes WPF-Control für die Detailansicht.
    /// Wird nur aufgerufen, wenn ViewType = Custom.
    /// </summary>
    /// <param name="result">Das Suchergebnis.</param>
    /// <returns>Ein WPF FrameworkElement oder null für Standard-Ansicht.</returns>
    FrameworkElement? CreateCustomDetailView(SearchResult result);

    /// <summary>
    /// Führt eine Aktion auf einem Suchergebnis aus.
    /// </summary>
    /// <param name="result">Das Suchergebnis.</param>
    /// <param name="actionId">Die ID der auszuführenden Aktion.</param>
    /// <returns>True, wenn die Aktion erfolgreich war.</returns>
    Task<bool> ExecuteActionAsync(SearchResult result, string actionId);

    /// <summary>
    /// Gibt Ressourcen frei.
    /// </summary>
    void Dispose();
}
