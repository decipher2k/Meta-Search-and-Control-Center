//Meta Search and Control Center (c) 2026 Dennis Michael Heine
namespace MSCC.Models;

/// <summary>
/// Repräsentiert ein Label, das einem Datensatz innerhalb einer Abfrage zugeordnet wird.
/// </summary>
public class QueryLabel
{
    /// <summary>
    /// Eindeutige ID des Labels.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Das Schlüsselwort/Label selbst.
    /// </summary>
    public string Keyword { get; set; } = string.Empty;

    /// <summary>
    /// Referenz zum Datensatz (z.B. ID oder Pfad).
    /// </summary>
    public string DataReference { get; set; } = string.Empty;

    /// <summary>
    /// ID des Konnektors, aus dem der Datensatz stammt.
    /// </summary>
    public string ConnectorId { get; set; } = string.Empty;

    /// <summary>
    /// Zeitstempel der Label-Erstellung.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Repräsentiert eine gespeicherte Suchabfrage mit zugehörigen Labels.
/// </summary>
public class SearchQuery
{
    /// <summary>
    /// Eindeutige ID der Abfrage.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Der Suchbegriff der Abfrage.
    /// </summary>
    public string SearchTerm { get; set; } = string.Empty;

    /// <summary>
    /// IDs der Datenquellen, die durchsucht wurden.
    /// </summary>
    public List<string> SelectedDataSourceIds { get; set; } = new();

    /// <summary>
    /// IDs der Gruppen, die durchsucht wurden.
    /// </summary>
    public List<string> SelectedGroupIds { get; set; } = new();

    /// <summary>
    /// Labels, die bei dieser Abfrage zu Datensätzen zugeordnet wurden.
    /// </summary>
    public List<QueryLabel> Labels { get; set; } = new();

    /// <summary>
    /// Zeitstempel der Abfrage-Erstellung.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Zeitstempel der letzten Ausführung.
    /// </summary>
    public DateTime? LastExecutedAt { get; set; }

    /// <summary>
    /// Name der Abfrage (optional, für gespeicherte Abfragen).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Beschreibung der Abfrage (optional).
    /// </summary>
    public string? Description { get; set; }
}
