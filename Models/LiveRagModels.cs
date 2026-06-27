//Meta Search and Control Center (c) 2026 Dennis Michael Heine
namespace MSCC.Models;

/// <summary>
/// Request passed to data source connectors for live RAG context retrieval.
/// </summary>
public class LiveRagQueryRequest
{
    /// <summary>
    /// The natural-language question asked by the user.
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Search terms generated from the question. Native RAG connectors may ignore them.
    /// </summary>
    public List<string> SearchTerms { get; set; } = new();

    /// <summary>
    /// How this request should be executed by live RAG components.
    /// </summary>
    public LiveRagMode Mode { get; set; } = LiveRagMode.HybridLive;

    /// <summary>
    /// Validated live operations that a connector should execute against its source.
    /// </summary>
    public List<LiveRagOperation> Operations { get; set; } = new();

    /// <summary>
    /// Maximum number of live operations the connector should execute.
    /// </summary>
    public int MaxOperationsPerSource { get; set; } = 3;

    /// <summary>
    /// Maximum number of candidate records or chunks retrieved before final clipping/reranking.
    /// </summary>
    public int MaxCandidateItems { get; set; } = 50;

    /// <summary>
    /// Maximum number of search terms that should be tried per connector.
    /// </summary>
    public int MaxSearchTerms { get; set; } = 5;

    /// <summary>
    /// Maximum number of raw results to retrieve per connector query.
    /// </summary>
    public int MaxResultsPerSearchTerm { get; set; } = 20;

    /// <summary>
    /// Maximum number of context chunks returned by a connector.
    /// </summary>
    public int MaxContextItems { get; set; } = 20;

    /// <summary>
    /// Maximum characters copied into each context chunk.
    /// </summary>
    public int MaxCharactersPerItem { get; set; } = 2500;

    /// <summary>
    /// Includes metadata in the RAG context when available.
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;

    /// <summary>
    /// Connector-specific options for native RAG implementations.
    /// </summary>
    public Dictionary<string, string> Options { get; set; } = new();
}

/// <summary>
/// A single live RAG context chunk returned by a data source connector.
/// </summary>
public class LiveRagContextItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string ConnectorId { get; set; } = string.Empty;
    public string OriginalReference { get; set; } = string.Empty;
    public int RelevanceScore { get; set; }
    public bool FromNativeLiveRag { get; set; }
    public string? RetrievalQuery { get; set; }
    public string? OperationId { get; set; }
    public LiveRagOperationType? OperationType { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime RetrievedAt { get; set; } = DateTime.Now;

    public static LiveRagContextItem FromSearchResult(
        SearchResult result,
        string? retrievalQuery,
        int maxCharacters,
        bool includeMetadata)
    {
        var content = string.IsNullOrWhiteSpace(result.Description)
            ? result.Title
            : result.Description;

        if (maxCharacters > 0 && content.Length > maxCharacters)
        {
            content = content[..maxCharacters] + "...";
        }

        return new LiveRagContextItem
        {
            Title = result.Title,
            Content = content,
            SourceName = result.SourceName,
            ConnectorId = result.ConnectorId,
            OriginalReference = result.OriginalReference,
            RelevanceScore = result.RelevanceScore,
            RetrievalQuery = retrievalQuery,
            Metadata = includeMetadata
                ? new Dictionary<string, object>(result.Metadata)
                : new Dictionary<string, object>()
        };
    }
}

/// <summary>
/// Result returned by one connector during live RAG context retrieval.
/// </summary>
public class LiveRagRetrievalResult
{
    public bool Success { get; set; } = true;
    public bool IsNativeLiveRag { get; set; }
    public string DataSourceId { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string ConnectorId { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public List<string> ExecutedQueries { get; set; } = new();
    public List<LiveRagOperation> ExecutedOperations { get; set; } = new();
    public List<LiveRagOperation> RejectedOperations { get; set; } = new();
    public List<LiveRagExecutionTrace> Diagnostics { get; set; } = new();
    public List<LiveRagContextItem> ContextItems { get; set; } = new();
}

/// <summary>
/// Aggregated context and diagnostics for one complete live RAG request.
/// </summary>
public class LiveRagContextResult
{
    public bool Success { get; set; } = true;
    public string Question { get; set; } = string.Empty;
    public List<string> SearchTerms { get; set; } = new();
    public LiveRagMode Mode { get; set; } = LiveRagMode.HybridLive;
    public bool IsDegradedFallback { get; set; }
    public List<LiveRagSourceProfile> SourceProfiles { get; set; } = new();
    public List<LiveRagOperation> PlanOperations { get; set; } = new();
    public List<LiveRagExecutionTrace> ExecutionTrace { get; set; } = new();
    public Dictionary<string, object> Diagnostics { get; set; } = new();
    public List<LiveRagContextItem> ContextItems { get; set; } = new();
    public List<LiveRagRetrievalResult> SourceResults { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class LiveRagPlanResult
{
    public bool Success { get; set; }
    public bool IsDegradedFallback { get; set; }
    public string? ErrorMessage { get; set; }
    public string RawResponse { get; set; } = string.Empty;
    public List<LiveRagOperation> Operations { get; set; } = new();
    public List<LiveRagOperation> RejectedOperations { get; set; } = new();
    public Dictionary<string, object> Diagnostics { get; set; } = new();
}

public enum LiveRagMode
{
    DegradedKeywordFallback,
    HybridLive
}

public enum LiveRagOperationType
{
    KeywordSearch,
    ContentScan,
    FilteredFetch,
    StructuredQuery,
    TopN,
    Aggregate,
    FetchById,
    NativeSemantic
}

public class LiveRagOperation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DataSourceId { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string ConnectorId { get; set; } = string.Empty;
    public LiveRagOperationType Type { get; set; } = LiveRagOperationType.KeywordSearch;
    public string Query { get; set; } = string.Empty;
    public List<string> SearchTerms { get; set; } = new();
    public string? Target { get; set; }
    public List<string> SelectFields { get; set; } = new();
    public Dictionary<string, string> Filters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? SortField { get; set; }
    public bool SortDescending { get; set; } = true;
    public int Limit { get; set; } = 10;
    public string Rationale { get; set; } = string.Empty;
    public bool IsDegradedFallback { get; set; }
    public Dictionary<string, string> Options { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class LiveRagSourceProfile
{
    public string DataSourceId { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string ConnectorId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool SupportsNativeLiveRag { get; set; }
    public bool IsLiveProfile { get; set; } = true;
    public List<LiveRagOperationType> SupportedOperations { get; set; } = new();
    public List<string> Fields { get; set; } = new();
    public List<string> Targets { get; set; } = new();
    public int MaxOperations { get; set; } = 3;
    public int MaxResults { get; set; } = 20;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class LiveRagExecutionTrace
{
    public string OperationId { get; set; } = string.Empty;
    public string DataSourceId { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string ConnectorId { get; set; } = string.Empty;
    public LiveRagOperationType OperationType { get; set; }
    public bool Accepted { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int ResultCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
}
