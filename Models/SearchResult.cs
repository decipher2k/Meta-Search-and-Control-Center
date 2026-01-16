//Meta Search and Control Center (c) 2026 Dennis Michael Heine
namespace MSCC.Models;

/// <summary>
/// Repräsentiert ein einzelnes Suchergebnis aus einer Datenquelle.
/// </summary>
public class SearchResult
{
    /// <summary>
    /// Eindeutige ID des Suchergebnisses.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Titel oder Name des Datensatzes.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Beschreibung oder Inhalt des Datensatzes.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Name der Datenquelle, aus der das Ergebnis stammt.
    /// </summary>
    public string SourceName { get; set; } = string.Empty;

    /// <summary>
    /// ID des Konnektors, der das Ergebnis geliefert hat.
    /// </summary>
    public string ConnectorId { get; set; } = string.Empty;

    /// <summary>
    /// Originale Referenz zum Datensatz (z.B. Dateipfad, URL, etc.).
    /// </summary>
    public string OriginalReference { get; set; } = string.Empty;

    /// <summary>
    /// Zusätzliche Metadaten des Datensatzes.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Zeitstempel, wann das Ergebnis gefunden wurde.
    /// </summary>
    public DateTime FoundAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Relevanz-Score des Ergebnisses (0-100).
    /// </summary>
    public int RelevanceScore { get; set; }
}
