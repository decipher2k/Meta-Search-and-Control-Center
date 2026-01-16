//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using MSCC.Models;

namespace MSCC.Services;

/// <summary>
/// Service für die Durchführung von Suchen über mehrere Datenquellen.
/// </summary>
public class SearchService
{
    private readonly DataSourceManager _dataSourceManager;

    public SearchService(DataSourceManager dataSourceManager)
    {
        _dataSourceManager = dataSourceManager;
    }

    /// <summary>
    /// Führt eine Suche über die angegebenen Datenquellen durch.
    /// </summary>
    public async Task<SearchQuery> ExecuteSearchAsync(
        string searchTerm,
        IEnumerable<string> dataSourceIds,
        IEnumerable<string>? groupIds = null,
        int maxResultsPerSource = 100,
        CancellationToken cancellationToken = default)
    {
        var query = new SearchQuery
        {
            SearchTerm = searchTerm,
            SelectedDataSourceIds = dataSourceIds.ToList(),
            SelectedGroupIds = groupIds?.ToList() ?? new List<string>(),
            LastExecutedAt = DateTime.Now
        };

        GlobalState.CurrentQuery = query;

        return query;
    }

    /// <summary>
    /// Führt die eigentliche Suche aus und liefert die Ergebnisse.
    /// </summary>
    public async Task<List<SearchResult>> GetSearchResultsAsync(
        SearchQuery query,
        int maxResultsPerSource = 100,
        CancellationToken cancellationToken = default,
        IProgress<(string sourceName, int resultCount)>? progress = null)
    {
        var allResults = new List<SearchResult>();
        var dataSourceIds = new HashSet<string>(query.SelectedDataSourceIds);

        // Füge Datenquellen aus Gruppen hinzu
        foreach (var groupId in query.SelectedGroupIds)
        {
            var groupDataSources = GlobalState.GetDataSourcesByGroup(groupId);
            foreach (var ds in groupDataSources)
            {
                dataSourceIds.Add(ds.Id);
            }
        }

        // Durchsuche alle ausgewählten Datenquellen
        var tasks = new List<Task<IEnumerable<SearchResult>>>();
        var sourceNames = new Dictionary<Task<IEnumerable<SearchResult>>, string>();

        foreach (var dataSourceId in dataSourceIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var connector = _dataSourceManager.GetConnectorInstance(dataSourceId);
            if (connector == null)
                continue;

            var dataSource = GlobalState.DataSources.FirstOrDefault(ds => ds.Id == dataSourceId);
            var sourceName = dataSource?.Name ?? connector.Name;

            var task = connector.SearchAsync(query.SearchTerm, maxResultsPerSource, cancellationToken);
            tasks.Add(task);
            sourceNames[task] = sourceName;
        }

        // Warte auf alle Suchanfragen
        while (tasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(tasks);
            tasks.Remove(completedTask);

            try
            {
                var results = await completedTask;
                var resultList = results.ToList();
                allResults.AddRange(resultList);

                if (sourceNames.TryGetValue(completedTask, out var sourceName))
                {
                    progress?.Report((sourceName, resultList.Count));
                }
            }
            catch (OperationCanceledException)
            {
                // Suche wurde abgebrochen
            }
            catch (Exception)
            {
                // Fehler bei der Suche - ignorieren und mit anderen fortfahren
            }
        }

        // Sortiere nach Relevanz
        return allResults.OrderByDescending(r => r.RelevanceScore).ToList();
    }

    /// <summary>
    /// Fügt ein Label zu einem Datensatz in der aktuellen Abfrage hinzu.
    /// </summary>
    public void AddLabel(SearchQuery query, SearchResult result, string keyword)
    {
        var label = new QueryLabel
        {
            Keyword = keyword,
            DataReference = result.OriginalReference,
            ConnectorId = result.ConnectorId
        };

        query.Labels.Add(label);
    }

    /// <summary>
    /// Entfernt ein Label von einem Datensatz.
    /// </summary>
    public bool RemoveLabel(SearchQuery query, string labelId)
    {
        var label = query.Labels.FirstOrDefault(l => l.Id == labelId);
        if (label != null)
        {
            return query.Labels.Remove(label);
        }
        return false;
    }

    /// <summary>
    /// Sucht nach Abfragen, die Labels mit dem angegebenen Keyword enthalten.
    /// </summary>
    public IEnumerable<SearchQuery> SearchByKeyword(string keyword)
    {
        return GlobalState.SearchQueriesByLabel(keyword);
    }

    /// <summary>
    /// Speichert die aktuelle Abfrage.
    /// </summary>
    public void SaveQuery(SearchQuery query, string? name = null)
    {
        if (!string.IsNullOrEmpty(name))
        {
            query.Name = name;
        }

        if (!GlobalState.Queries.Contains(query))
        {
            GlobalState.AddQuery(query);
        }
    }

    /// <summary>
    /// Lädt eine gespeicherte Abfrage.
    /// </summary>
    public SearchQuery? LoadQuery(string queryId)
    {
        return GlobalState.Queries.FirstOrDefault(q => q.Id == queryId);
    }
}
