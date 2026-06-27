//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using MSCC.Connectors;
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
        var dataSourceIds = new HashSet<string>(query.SelectedDataSourceIds, StringComparer.OrdinalIgnoreCase);

        // Gruppen nur auflösen, wenn keine konkrete Data-Source-Auswahl vorliegt.
        foreach (var groupId in dataSourceIds.Count == 0
            ? query.SelectedGroupIds
            : Enumerable.Empty<string>())
        {
            var groupDataSources = GlobalState.GetDataSourcesByGroup(groupId);
            foreach (var ds in groupDataSources)
            {
                if (ds.IsEnabled)
                {
                    dataSourceIds.Add(ds.Id);
                }
            }
        }

        // Durchsuche alle ausgewählten Datenquellen
        var tasks = new List<Task<IEnumerable<SearchResult>>>();
        var sourceNames = new Dictionary<Task<IEnumerable<SearchResult>>, string>();

        foreach (var dataSourceId in dataSourceIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var dataSource = GlobalState.DataSources.FirstOrDefault(ds => ds.Id == dataSourceId);
            if (dataSource?.IsEnabled != true)
                continue;

            var connector = _dataSourceManager.GetConnectorInstance(dataSourceId);
            if (connector == null)
                continue;

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
    /// Holt Live-RAG-Kontext aus den angegebenen Datenquellen.
    /// Native RAG-Konnektoren werden direkt abgefragt; andere Konnektoren liefern Kontext
    /// über ihren SearchAsync-Fallback.
    /// </summary>
    public async Task<LiveRagContextResult> GetLiveRagContextAsync(
        string question,
        IEnumerable<string> dataSourceIds,
        IEnumerable<string>? groupIds = null,
        IEnumerable<string>? searchTerms = null,
        int maxResultsPerSearchTerm = 20,
        int maxContextItemsPerSource = 10,
        int maxContextItemsTotal = 40,
        int maxCharactersPerItem = 2500,
        bool includeMetadata = true,
        CancellationToken cancellationToken = default,
        IProgress<(string sourceName, int contextCount, bool isNativeLiveRag)>? progress = null)
    {
        var contextResult = new LiveRagContextResult
        {
            Question = question,
            SearchTerms = LiveRagConnectorHelpers.CreateSearchTerms(
                question,
                searchTerms,
                maxTerms: 12)
        };

        if (contextResult.SearchTerms.Count == 0)
        {
            contextResult.SearchTerms.Add(question);
        }

        var resolvedDataSourceIds = ResolveDataSourceIds(dataSourceIds, groupIds);
        if (resolvedDataSourceIds.Count == 0)
        {
            contextResult.Success = false;
            contextResult.ErrorMessage = "Keine Datenquellen ausgewählt oder verfügbar.";
            return contextResult;
        }

        var tasks = new List<Task<LiveRagRetrievalResult>>();
        var sourceNames = new Dictionary<Task<LiveRagRetrievalResult>, string>();
        var connectorIds = new Dictionary<Task<LiveRagRetrievalResult>, string>();
        var dataSourcesByTask = new Dictionary<Task<LiveRagRetrievalResult>, DataSource>();

        foreach (var dataSourceId in resolvedDataSourceIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var connector = _dataSourceManager.GetConnectorInstance(dataSourceId);
            if (connector == null)
                continue;

            var dataSource = GlobalState.DataSources.FirstOrDefault(ds => ds.Id == dataSourceId);
            if (dataSource?.IsEnabled == false)
                continue;

            var request = new LiveRagQueryRequest
            {
                Question = question,
                SearchTerms = contextResult.SearchTerms.ToList(),
                MaxSearchTerms = contextResult.SearchTerms.Count,
                MaxResultsPerSearchTerm = Math.Max(1, maxResultsPerSearchTerm),
                MaxContextItems = Math.Max(1, maxContextItemsPerSource),
                MaxCharactersPerItem = Math.Max(250, maxCharactersPerItem),
                IncludeMetadata = includeMetadata
            };

            var task = connector.RetrieveLiveRagContextAsync(request, cancellationToken);
            var sourceName = dataSource?.Name ?? connector.Name;
            tasks.Add(task);
            sourceNames[task] = sourceName;
            connectorIds[task] = connector.Id;
            if (dataSource != null)
                dataSourcesByTask[task] = dataSource;
        }

        while (tasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(tasks);
            tasks.Remove(completedTask);

            var sourceName = sourceNames.GetValueOrDefault(completedTask, "Unbekannte Quelle");

            try
            {
                var sourceResult = await completedTask;
                if (dataSourcesByTask.TryGetValue(completedTask, out var dataSource))
                {
                    sourceResult.DataSourceId = dataSource.Id;
                    if (string.IsNullOrWhiteSpace(sourceResult.SourceName))
                        sourceResult.SourceName = dataSource.Name;
                }

                if (string.IsNullOrWhiteSpace(sourceResult.SourceName))
                    sourceResult.SourceName = sourceName;
                if (string.IsNullOrWhiteSpace(sourceResult.ConnectorId) &&
                    connectorIds.TryGetValue(completedTask, out var connectorId))
                {
                    sourceResult.ConnectorId = connectorId;
                }

                foreach (var item in sourceResult.ContextItems)
                {
                    if (string.IsNullOrWhiteSpace(item.SourceName))
                        item.SourceName = sourceResult.SourceName;
                    if (string.IsNullOrWhiteSpace(item.ConnectorId))
                        item.ConnectorId = sourceResult.ConnectorId;
                    item.FromNativeLiveRag = item.FromNativeLiveRag || sourceResult.IsNativeLiveRag;
                }

                contextResult.SourceResults.Add(sourceResult);
                contextResult.ContextItems.AddRange(sourceResult.ContextItems);

                progress?.Report((sourceName, sourceResult.ContextItems.Count, sourceResult.IsNativeLiveRag));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                contextResult.SourceResults.Add(new LiveRagRetrievalResult
                {
                    Success = false,
                    SourceName = sourceName,
                    ErrorMessage = ex.Message
                });
            }
        }

        contextResult.ContextItems = contextResult.ContextItems
            .GroupBy(item => string.IsNullOrWhiteSpace(item.OriginalReference)
                ? $"{item.ConnectorId}:{item.Title}:{item.Content}"
                : $"{item.ConnectorId}:{item.OriginalReference}")
            .Select(group => group
                .OrderByDescending(item => item.FromNativeLiveRag)
                .ThenByDescending(item => item.RelevanceScore)
                .First())
            .OrderByDescending(item => item.FromNativeLiveRag)
            .ThenByDescending(item => item.RelevanceScore)
            .Take(Math.Max(1, maxContextItemsTotal))
            .ToList();

        contextResult.Success = contextResult.ContextItems.Count > 0 || contextResult.SourceResults.Any(r => r.Success);
        if (contextResult.ContextItems.Count == 0)
        {
            contextResult.ErrorMessage = "Es konnte kein RAG-Kontext aus den ausgewählten Datenquellen geladen werden.";
        }

        return contextResult;
    }

    private static HashSet<string> ResolveDataSourceIds(
        IEnumerable<string> dataSourceIds,
        IEnumerable<string>? groupIds)
    {
        var resolvedDataSourceIds = new HashSet<string>(dataSourceIds, StringComparer.OrdinalIgnoreCase);

        foreach (var groupId in resolvedDataSourceIds.Count == 0
            ? (groupIds ?? Enumerable.Empty<string>())
            : Enumerable.Empty<string>())
        {
            var groupDataSources = GlobalState.GetDataSourcesByGroup(groupId);
            foreach (var ds in groupDataSources)
            {
                if (ds.IsEnabled)
                {
                    resolvedDataSourceIds.Add(ds.Id);
                }
            }
        }

        return resolvedDataSourceIds;
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

        _ = GlobalState.SaveStateAsync();
    }

    /// <summary>
    /// Lädt eine gespeicherte Abfrage.
    /// </summary>
    public SearchQuery? LoadQuery(string queryId)
    {
        return GlobalState.Queries.FirstOrDefault(q => q.Id == queryId);
    }
}
