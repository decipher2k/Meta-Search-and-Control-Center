//Meta Search and Control Center (c) 2026 Dennis Michael Heine
namespace MSCC.Models;

/// <summary>
/// Repräsentiert eine Datenquelle (Konnektor-Instanz).
/// </summary>
public class DataSource
{
    /// <summary>
    /// Eindeutige ID der Datenquelle.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Anzeigename der Datenquelle.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Beschreibung der Datenquelle.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// ID des zugehörigen Konnektors.
    /// </summary>
    public string ConnectorId { get; set; } = string.Empty;

    /// <summary>
    /// Konfiguration für den Konnektor (z.B. Verbindungsstring, Pfad).
    /// </summary>
    public Dictionary<string, string> Configuration { get; set; } = new();

    /// <summary>
    /// Gibt an, ob die Datenquelle aktiviert ist.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// ID der Gruppe, zu der diese Datenquelle gehört (optional).
    /// </summary>
    public string? GroupId { get; set; }
}

/// <summary>
/// Repräsentiert eine Gruppe von Datenquellen.
/// </summary>
public class DataSourceGroup
{
    /// <summary>
    /// Eindeutige ID der Gruppe.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Name der Gruppe.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Beschreibung der Gruppe.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Farbe der Gruppe für die UI-Darstellung.
    /// </summary>
    public string Color { get; set; } = "#3498db";

    /// <summary>
    /// Icon-Name für die Gruppe.
    /// </summary>
    public string? IconName { get; set; }
}
