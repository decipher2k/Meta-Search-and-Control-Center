//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Collections.ObjectModel;
using MSCC.Connectors;
using MSCC.Models;

namespace MSCC.Services;

/// <summary>
/// Globale statische Klasse zum Zugriff auf Abfragen und Datenquellen.
/// </summary>
public static class GlobalState
{
    private static readonly object _lock = new();

    /// <summary>
    /// Alle gespeicherten Suchabfragen.
    /// </summary>
    public static ObservableCollection<SearchQuery> Queries { get; } = new();

    /// <summary>
    /// Alle registrierten Datenquellen.
    /// </summary>
    public static ObservableCollection<DataSource> DataSources { get; } = new();

    /// <summary>
    /// Alle Datenquellen-Gruppen.
    /// </summary>
    public static ObservableCollection<DataSourceGroup> Groups { get; } = new();

    /// <summary>
    /// Registrierte Konnektor-Instanzen (Plugin-Instanzen).
    /// </summary>
    public static Dictionary<string, IDataSourceConnector> Connectors { get; } = new();

    /// <summary>
    /// Die aktuell aktive Abfrage.
    /// </summary>
    public static SearchQuery? CurrentQuery { get; set; }

    /// <summary>
    /// Fügt eine neue Abfrage hinzu.
    /// </summary>
    public static void AddQuery(SearchQuery query)
    {
        lock (_lock)
        {
            Queries.Add(query);
        }
    }

    /// <summary>
    /// Entfernt eine Abfrage.
    /// </summary>
    public static bool RemoveQuery(string queryId)
    {
        lock (_lock)
        {
            var query = Queries.FirstOrDefault(q => q.Id == queryId);
            if (query != null)
            {
                return Queries.Remove(query);
            }
            return false;
        }
    }

    /// <summary>
    /// Sucht Abfragen nach Keywords in den Labels.
    /// </summary>
    public static IEnumerable<SearchQuery> SearchQueriesByLabel(string keyword)
    {
        lock (_lock)
        {
            return Queries
                .Where(q => q.Labels.Any(l => 
                    l.Keyword.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
    }

    /// <summary>
    /// Registriert einen Konnektor.
    /// </summary>
    public static void RegisterConnector(IDataSourceConnector connector)
    {
        lock (_lock)
        {
            Connectors[connector.Id] = connector;
        }
    }

    /// <summary>
    /// Fügt eine Datenquelle hinzu.
    /// </summary>
    public static void AddDataSource(DataSource dataSource)
    {
        lock (_lock)
        {
            DataSources.Add(dataSource);
        }
    }

    /// <summary>
    /// Fügt eine Gruppe hinzu.
    /// </summary>
    public static void AddGroup(DataSourceGroup group)
    {
        lock (_lock)
        {
            Groups.Add(group);
        }
    }

    /// <summary>
    /// Holt alle Datenquellen einer Gruppe.
    /// </summary>
    public static IEnumerable<DataSource> GetDataSourcesByGroup(string groupId)
    {
        lock (_lock)
        {
            return DataSources.Where(ds => ds.GroupId == groupId).ToList();
        }
    }

    /// <summary>
    /// Holt alle Datenquellen ohne Gruppe.
    /// </summary>
    public static IEnumerable<DataSource> GetUngroupedDataSources()
    {
        lock (_lock)
        {
            return DataSources.Where(ds => string.IsNullOrEmpty(ds.GroupId)).ToList();
        }
    }

    /// <summary>
    /// Speichert den aktuellen Zustand persistent.
    /// </summary>
    public static async Task SaveStateAsync()
    {
        await DataSourceStorageService.Instance.SaveAsync();
    }

    /// <summary>
    /// Lädt den Zustand aus der persistenten Speicherung.
    /// </summary>
    public static async Task LoadStateAsync()
    {
        var state = await DataSourceStorageService.Instance.LoadAsync();
        if (state == null)
            return;

        lock (_lock)
        {
            // Gruppen laden
            Groups.Clear();
            foreach (var group in state.Groups)
            {
                Groups.Add(group);
            }

            // Datenquellen laden
            DataSources.Clear();
            foreach (var dataSource in state.DataSources)
            {
                DataSources.Add(dataSource);
            }

            // Abfragen laden
            Queries.Clear();
            foreach (var storedQuery in state.SavedQueries)
            {
                var query = new SearchQuery
                {
                    Id = storedQuery.Id,
                    Name = storedQuery.Name,
                    SearchTerm = storedQuery.SearchTerm,
                    CreatedAt = storedQuery.CreatedAt
                };
                
                foreach (var keyword in storedQuery.Labels)
                {
                    query.Labels.Add(new QueryLabel { Keyword = keyword });
                }
                
                foreach (var dsId in storedQuery.SelectedDataSourceIds)
                {
                    query.SelectedDataSourceIds.Add(dsId);
                }
                
                Queries.Add(query);
            }
        }
    }
}
