//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MSCC.Models;

namespace MSCC.Services;

/// <summary>
/// Datenmodell für die persistente Speicherung.
/// </summary>
public class StoredState
{
    public List<DataSource> DataSources { get; set; } = new();
    public List<DataSourceGroup> Groups { get; set; } = new();
    public List<StoredQuery> SavedQueries { get; set; } = new();
}

/// <summary>
/// Vereinfachtes Query-Modell für Speicherung.
/// </summary>
public class StoredQuery
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SearchTerm { get; set; } = string.Empty;
    public List<string> Labels { get; set; } = new();
    public List<string> SelectedDataSourceIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Service für persistente Speicherung von Datenquellen, Gruppen und Abfragen.
/// </summary>
public class DataSourceStorageService
{
    private static readonly Lazy<DataSourceStorageService> _instance = new(() => new DataSourceStorageService());
    public static DataSourceStorageService Instance => _instance.Value;

    private readonly string _storagePath;
    private readonly JsonSerializerOptions _jsonOptions;

    private DataSourceStorageService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var msccPath = Path.Combine(appDataPath, "MSCC");
        Directory.CreateDirectory(msccPath);
        _storagePath = Path.Combine(msccPath, "datasources.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Speichert den aktuellen Zustand.
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            var state = new StoredState
            {
                DataSources = GlobalState.DataSources.ToList(),
                Groups = GlobalState.Groups.ToList(),
                SavedQueries = GlobalState.Queries.Select(q => new StoredQuery
                {
                    Id = q.Id,
                    Name = q.Name ?? string.Empty,
                    SearchTerm = q.SearchTerm,
                    Labels = q.Labels.Select(l => l.Keyword).ToList(),
                    SelectedDataSourceIds = q.SelectedDataSourceIds.ToList(),
                    CreatedAt = q.CreatedAt
                }).ToList()
            };

            var json = JsonSerializer.Serialize(state, _jsonOptions);
            await File.WriteAllTextAsync(_storagePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving state: {ex.Message}");
        }
    }

    /// <summary>
    /// Lädt den gespeicherten Zustand.
    /// </summary>
    public async Task<StoredState?> LoadAsync()
    {
        try
        {
            if (!File.Exists(_storagePath))
                return null;

            var json = await File.ReadAllTextAsync(_storagePath);
            return JsonSerializer.Deserialize<StoredState>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Pfad zur Speicherdatei.
    /// </summary>
    public string StoragePath => _storagePath;
}
