//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using MSCC.Connectors;
using MSCC.Models;

namespace MSCC.Services;

/// <summary>
/// Coordinates hybrid live RAG: source profiling, AI planning, validation, live execution and reranking.
/// </summary>
public class LiveRagOrchestrator
{
    private static readonly TimeSpan ProfileTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PlanningTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SourceOperationTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RerankTimeout = TimeSpan.FromSeconds(30);

    private static readonly string[] DangerousQueryTokens =
    [
        " insert ", " update ", " delete ", " drop ", " alter ", " truncate ",
        " create ", " replace ", " grant ", " revoke ", " exec ", " execute ",
        " merge ", " into outfile ", " load_file "
    ];

    private readonly DataSourceManager _dataSourceManager;
    private readonly AiSearchService _aiSearchService;

    public LiveRagOrchestrator(DataSourceManager dataSourceManager, AiSearchService? aiSearchService = null)
    {
        _dataSourceManager = dataSourceManager;
        _aiSearchService = aiSearchService ?? new AiSearchService();
    }

    public async Task<LiveRagContextResult> GetLiveRagContextAsync(
        string question,
        IEnumerable<string> dataSourceIds,
        IEnumerable<string>? groupIds = null,
        int maxResultsPerOperation = 20,
        int maxContextItemsPerSource = 10,
        int maxContextItemsTotal = 40,
        int maxCharactersPerItem = 2500,
        bool includeMetadata = true,
        IEnumerable<string>? seedSearchTerms = null,
        bool useAiPlanning = true,
        CancellationToken cancellationToken = default,
        IProgress<(string sourceName, int contextCount, bool isNativeLiveRag)>? progress = null,
        IProgress<string>? statusProgress = null)
    {
        var result = new LiveRagContextResult
        {
            Question = question,
            Mode = LiveRagMode.HybridLive,
            SearchTerms = LiveRagConnectorHelpers.CreateSearchTerms(question, seedSearchTerms, 12)
        };

        var resolvedSourceIds = ResolveDataSourceIds(dataSourceIds, groupIds);
        if (resolvedSourceIds.Count == 0)
        {
            result.Success = false;
            result.ErrorMessage = "Keine Datenquellen ausgewählt oder verfügbar.";
            return result;
        }

        var sourceEntries = ResolveEnabledSources(resolvedSourceIds).ToList();
        if (sourceEntries.Count == 0)
        {
            result.Success = false;
            result.ErrorMessage = "Keine aktivierten Datenquellen verfügbar.";
            return result;
        }

        statusProgress?.Report("Live-RAG: Profile der Datenquellen werden gelesen...");
        var profiles = await BuildSourceProfilesAsync(sourceEntries, maxResultsPerOperation, cancellationToken);
        result.SourceProfiles.AddRange(profiles);

        statusProgress?.Report("Live-RAG: KI-Rechercheplan wird erstellt...");
        var plan = useAiPlanning
            ? await CreatePlanWithTimeoutAsync(
                question,
                profiles,
                maxOperationsPerSource: 3,
                maxCandidateItems: Math.Max(maxResultsPerOperation, maxContextItemsPerSource),
                cancellationToken)
            : new LiveRagPlanResult
            {
                Success = false,
                IsDegradedFallback = true,
                ErrorMessage = "AI planning disabled for this request."
            };

        result.Diagnostics["plannerSuccess"] = plan.Success;
        result.Diagnostics["plannerDegraded"] = plan.IsDegradedFallback;
        result.Diagnostics["plannerError"] = plan.ErrorMessage ?? "";
        if (!string.IsNullOrWhiteSpace(plan.RawResponse))
            result.Diagnostics["rawPlan"] = plan.RawResponse;

        var validation = ValidateOperations(
            question,
            profiles,
            plan.Operations,
            maxOperationsPerSource: 3,
            maxCandidateItems: Math.Max(maxResultsPerOperation, maxContextItemsPerSource));

        var acceptedOperations = validation.AcceptedOperations;
        var rejectedOperations = validation.RejectedOperations;

        if (acceptedOperations.Count == 0)
        {
            acceptedOperations = BuildProfileAwareFallbackOperations(
                question,
                profiles,
                Math.Max(1, maxResultsPerOperation),
                seedSearchTerms)
                .ToList();
            result.IsDegradedFallback = acceptedOperations.All(operation => operation.IsDegradedFallback);
            result.Mode = result.IsDegradedFallback
                ? LiveRagMode.DegradedKeywordFallback
                : LiveRagMode.HybridLive;
            result.Diagnostics["degradedReason"] = plan.ErrorMessage ?? "No valid AI-planned live operations were available.";
        }
        else
        {
            result.IsDegradedFallback = acceptedOperations.Any(operation => operation.IsDegradedFallback);
        }

        result.PlanOperations.AddRange(acceptedOperations);
        result.PlanOperations.AddRange(rejectedOperations);
        result.ExecutionTrace.AddRange(validation.Traces);

        await ExecuteOperationsAsync(
            result,
            sourceEntries,
            acceptedOperations,
            question,
            maxResultsPerOperation,
            maxContextItemsPerSource,
            maxCharactersPerItem,
            includeMetadata,
            cancellationToken,
            progress);

        if (result.ContextItems.Count == 0)
        {
            var recoveryOperations = BuildProfileAwareFallbackOperations(
                    question,
                    profiles,
                    Math.Max(1, maxResultsPerOperation),
                    seedSearchTerms)
                .Where(operation => !acceptedOperations.Any(existing => HasSameOperationSignature(existing, operation)))
                .ToList();

            if (recoveryOperations.Count > 0)
            {
                result.Diagnostics["recoveryPlanReason"] = "The first validated plan returned no context; executing source-profile recovery operations.";
                result.PlanOperations.AddRange(recoveryOperations);
                result.IsDegradedFallback = result.IsDegradedFallback || recoveryOperations.Any(operation => operation.IsDegradedFallback);
                if (recoveryOperations.Any(operation => !operation.IsDegradedFallback))
                    result.Mode = LiveRagMode.HybridLive;

                await ExecuteOperationsAsync(
                    result,
                    sourceEntries,
                    recoveryOperations,
                    question,
                    maxResultsPerOperation,
                    maxContextItemsPerSource,
                    maxCharactersPerItem,
                    includeMetadata,
                    cancellationToken,
                    progress);
            }
        }

        result.SearchTerms = result.SearchTerms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        result.ContextItems = result.ContextItems
            .GroupBy(item => string.IsNullOrWhiteSpace(item.OriginalReference)
                ? $"{item.ConnectorId}:{item.Title}:{item.Content}"
                : $"{item.ConnectorId}:{item.OriginalReference}")
            .Select(group => group
                .OrderByDescending(item => item.RelevanceScore)
                .First())
            .OrderByDescending(item => item.RelevanceScore)
            .ToList();

        statusProgress?.Report("Live-RAG: Evidenz wird gerankt...");
        result.ContextItems = await RerankWithTimeoutAsync(
            question,
            result.ContextItems,
            Math.Max(1, maxContextItemsTotal),
            cancellationToken);

        result.Success = result.ContextItems.Count > 0;
        if (!result.Success)
            result.ErrorMessage = "Es konnte kein Live-RAG-Kontext aus den ausgewählten Datenquellen geladen werden.";

        return result;
    }

    private async Task<LiveRagPlanResult> CreatePlanWithTimeoutAsync(
        string question,
        IEnumerable<LiveRagSourceProfile> profiles,
        int maxOperationsPerSource,
        int maxCandidateItems,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PlanningTimeout);

        try
        {
            return await _aiSearchService.CreateHybridLiveRagPlanAsync(
                question,
                profiles,
                maxOperationsPerSource,
                maxCandidateItems,
                timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new LiveRagPlanResult
            {
                Success = false,
                IsDegradedFallback = true,
                ErrorMessage = $"AI planning timed out after {PlanningTimeout.TotalSeconds:0} seconds."
            };
        }
    }

    private async Task<List<LiveRagContextItem>> RerankWithTimeoutAsync(
        string question,
        IEnumerable<LiveRagContextItem> contextItems,
        int maxItems,
        CancellationToken cancellationToken)
    {
        var items = contextItems.ToList();
        if (items.Count == 0)
            return items;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RerankTimeout);

        try
        {
            return await _aiSearchService.RerankLiveRagContextAsync(
                question,
                items,
                maxItems,
                timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return items
                .OrderByDescending(item => item.RelevanceScore)
                .Take(Math.Max(1, maxItems))
                .ToList();
        }
    }

    public static LiveRagValidationResult ValidateOperations(
        string question,
        IEnumerable<LiveRagSourceProfile> profiles,
        IEnumerable<LiveRagOperation> operations,
        int maxOperationsPerSource,
        int maxCandidateItems)
    {
        var profileById = profiles.ToDictionary(profile => profile.DataSourceId, StringComparer.OrdinalIgnoreCase);
        var accepted = new List<LiveRagOperation>();
        var rejected = new List<LiveRagOperation>();
        var traces = new List<LiveRagExecutionTrace>();
        var countsBySource = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var operation in operations)
        {
            var trace = new LiveRagExecutionTrace
            {
                OperationId = operation.Id,
                DataSourceId = operation.DataSourceId,
                ConnectorId = operation.ConnectorId,
                SourceName = operation.SourceName,
                OperationType = operation.Type,
                StartedAt = DateTime.Now,
                CompletedAt = DateTime.Now
            };

            if (!profileById.TryGetValue(operation.DataSourceId, out var profile))
            {
                Reject(operation, trace, "Unknown or unavailable data source.", rejected, traces);
                continue;
            }

            operation.SourceName = profile.SourceName;
            operation.ConnectorId = profile.ConnectorId;
            trace.SourceName = profile.SourceName;
            trace.ConnectorId = profile.ConnectorId;

            if (!profile.SupportedOperations.Contains(operation.Type))
            {
                Reject(operation, trace, $"Operation {operation.Type} is not supported by this connector.", rejected, traces);
                continue;
            }

            if (ContainsDangerousQuery(operation.Query))
            {
                Reject(operation, trace, "Operation query contains a forbidden write or execution token.", rejected, traces);
                continue;
            }

            countsBySource.TryGetValue(operation.DataSourceId, out var sourceCount);
            if (sourceCount >= Math.Max(1, maxOperationsPerSource))
            {
                Reject(operation, trace, "Too many operations for this data source.", rejected, traces);
                continue;
            }

            operation.Query = string.IsNullOrWhiteSpace(operation.Query)
                ? question
                : operation.Query.Trim();
            if (operation.SearchTerms.Count == 0)
                operation.SearchTerms = LiveRagConnectorHelpers.CreateSearchTerms(operation.Query, null, 8);
            operation.Limit = Math.Clamp(operation.Limit <= 0 ? profile.MaxResults : operation.Limit, 1, Math.Max(1, maxCandidateItems));
            if (!profile.SupportsNativeLiveRag)
                operation.IsDegradedFallback = true;

            countsBySource[operation.DataSourceId] = sourceCount + 1;
            trace.Accepted = true;
            trace.Reason = operation.IsDegradedFallback
                ? "Accepted degraded fallback operation for a connector without native live RAG."
                : "Accepted validated live operation.";
            accepted.Add(operation);
            traces.Add(trace);
        }

        return new LiveRagValidationResult(accepted, rejected, traces);
    }

    private async Task ExecuteOperationsAsync(
        LiveRagContextResult result,
        IEnumerable<(DataSource DataSource, IDataSourceConnector Connector)> sourceEntries,
        IReadOnlyCollection<LiveRagOperation> operations,
        string question,
        int maxResultsPerOperation,
        int maxContextItemsPerSource,
        int maxCharactersPerItem,
        bool includeMetadata,
        CancellationToken cancellationToken,
        IProgress<(string sourceName, int contextCount, bool isNativeLiveRag)>? progress)
    {
        var operationsBySource = operations
            .GroupBy(operation => operation.DataSourceId)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var entry in sourceEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!operationsBySource.TryGetValue(entry.DataSource.Id, out var sourceOperations) ||
                sourceOperations.Count == 0)
            {
                continue;
            }

            var requestSearchTerms = sourceOperations
                .SelectMany(operation => new[] { operation.Query }.Concat(operation.SearchTerms))
                .Where(term => !string.IsNullOrWhiteSpace(term))
                .DefaultIfEmpty(question)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var request = new LiveRagQueryRequest
            {
                Question = question,
                SearchTerms = requestSearchTerms,
                Mode = sourceOperations.Any(operation => operation.IsDegradedFallback)
                    ? LiveRagMode.DegradedKeywordFallback
                    : LiveRagMode.HybridLive,
                Operations = sourceOperations,
                MaxSearchTerms = 6,
                MaxResultsPerSearchTerm = Math.Max(1, maxResultsPerOperation),
                MaxContextItems = Math.Max(1, maxContextItemsPerSource),
                MaxCharactersPerItem = Math.Max(250, maxCharactersPerItem),
                IncludeMetadata = includeMetadata,
                MaxOperationsPerSource = 3,
                MaxCandidateItems = Math.Max(maxResultsPerOperation, maxContextItemsPerSource)
            };

            progress?.Report((entry.DataSource.Name, 0, sourceOperations.Any(operation => !operation.IsDegradedFallback)));

            LiveRagRetrievalResult sourceResult;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(SourceOperationTimeout);

                try
                {
                    sourceResult = await entry.Connector.RetrieveLiveRagContextByOperationsAsync(request, timeout.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    sourceResult = new LiveRagRetrievalResult
                    {
                        Success = false,
                        DataSourceId = entry.DataSource.Id,
                        SourceName = entry.DataSource.Name,
                        ConnectorId = entry.Connector.Id,
                        ErrorMessage = $"Live-RAG operation timed out after {SourceOperationTimeout.TotalSeconds:0} seconds.",
                        RejectedOperations = sourceOperations,
                        Diagnostics =
                        {
                            new LiveRagExecutionTrace
                            {
                                DataSourceId = entry.DataSource.Id,
                                SourceName = entry.DataSource.Name,
                                ConnectorId = entry.Connector.Id,
                                OperationType = sourceOperations.FirstOrDefault()?.Type ?? LiveRagOperationType.KeywordSearch,
                                Accepted = false,
                                StartedAt = DateTime.Now - SourceOperationTimeout,
                                CompletedAt = DateTime.Now,
                                Reason = "Connector live operation timed out."
                            }
                        }
                    };
                }
            }

            sourceResult.DataSourceId = entry.DataSource.Id;
            if (string.IsNullOrWhiteSpace(sourceResult.SourceName))
                sourceResult.SourceName = entry.DataSource.Name;
            if (string.IsNullOrWhiteSpace(sourceResult.ConnectorId))
                sourceResult.ConnectorId = entry.Connector.Id;

            foreach (var item in sourceResult.ContextItems)
            {
                if (string.IsNullOrWhiteSpace(item.SourceName))
                    item.SourceName = sourceResult.SourceName;
                if (string.IsNullOrWhiteSpace(item.ConnectorId))
                    item.ConnectorId = sourceResult.ConnectorId;
            }

            result.SourceResults.Add(sourceResult);
            result.ContextItems.AddRange(sourceResult.ContextItems);
            result.ExecutionTrace.AddRange(sourceResult.Diagnostics);
            result.SearchTerms.AddRange(request.SearchTerms);

            if (!sourceResult.Success && !string.IsNullOrWhiteSpace(sourceResult.ErrorMessage))
                result.Diagnostics[$"sourceError:{entry.DataSource.Id}"] = sourceResult.ErrorMessage;

            progress?.Report((sourceResult.SourceName, sourceResult.ContextItems.Count, sourceResult.IsNativeLiveRag));
        }
    }

    private async Task<List<LiveRagSourceProfile>> BuildSourceProfilesAsync(
        IEnumerable<(DataSource DataSource, IDataSourceConnector Connector)> sourceEntries,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var profiles = new List<LiveRagSourceProfile>();

        foreach (var entry in sourceEntries)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(ProfileTimeout);

                var profile = await entry.Connector.DescribeLiveRagCapabilitiesAsync(entry.DataSource, timeout.Token);
                profile.DataSourceId = entry.DataSource.Id;
                profile.SourceName = string.IsNullOrWhiteSpace(profile.SourceName) ? entry.DataSource.Name : profile.SourceName;
                profile.ConnectorId = string.IsNullOrWhiteSpace(profile.ConnectorId) ? entry.Connector.Id : profile.ConnectorId;
                profile.Description = string.IsNullOrWhiteSpace(profile.Description) ? entry.DataSource.Description : profile.Description;
                profile.MaxResults = Math.Max(1, Math.Min(profile.MaxResults <= 0 ? maxResults : profile.MaxResults, maxResults));
                profiles.Add(profile);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                profiles.Add(new LiveRagSourceProfile
                {
                    DataSourceId = entry.DataSource.Id,
                    SourceName = entry.DataSource.Name,
                    ConnectorId = entry.Connector.Id,
                    Description = entry.DataSource.Description,
                    SupportsNativeLiveRag = false,
                    SupportedOperations = [LiveRagOperationType.KeywordSearch],
                    MaxResults = Math.Max(1, maxResults),
                    Metadata = { ["profileError"] = $"Profile read timed out after {ProfileTimeout.TotalSeconds:0} seconds." }
                });
            }
            catch (Exception ex)
            {
                profiles.Add(new LiveRagSourceProfile
                {
                    DataSourceId = entry.DataSource.Id,
                    SourceName = entry.DataSource.Name,
                    ConnectorId = entry.Connector.Id,
                    Description = entry.DataSource.Description,
                    SupportsNativeLiveRag = false,
                    SupportedOperations = [LiveRagOperationType.KeywordSearch],
                    MaxResults = Math.Max(1, maxResults),
                    Metadata = { ["profileError"] = ex.Message }
                });
            }
        }

        return profiles;
    }

    private IEnumerable<(DataSource DataSource, IDataSourceConnector Connector)> ResolveEnabledSources(HashSet<string> sourceIds)
    {
        foreach (var sourceId in sourceIds)
        {
            var dataSource = GlobalState.DataSources.FirstOrDefault(source =>
                source.IsEnabled && string.Equals(source.Id, sourceId, StringComparison.OrdinalIgnoreCase));
            if (dataSource == null)
                continue;

            var connector = _dataSourceManager.GetConnectorInstance(dataSource.Id);
            if (connector == null)
                continue;

            yield return (dataSource, connector);
        }
    }

    private static HashSet<string> ResolveDataSourceIds(
        IEnumerable<string> dataSourceIds,
        IEnumerable<string>? groupIds)
    {
        var resolved = new HashSet<string>(dataSourceIds, StringComparer.OrdinalIgnoreCase);

        foreach (var groupId in resolved.Count == 0
            ? (groupIds ?? Enumerable.Empty<string>())
            : Enumerable.Empty<string>())
        {
            foreach (var source in GlobalState.GetDataSourcesByGroup(groupId))
            {
                if (source.IsEnabled)
                {
                    resolved.Add(source.Id);
                }
            }
        }

        return resolved;
    }

    private static IEnumerable<LiveRagOperation> BuildProfileAwareFallbackOperations(
        string question,
        IEnumerable<LiveRagSourceProfile> profiles,
        int limit,
        IEnumerable<string>? seedSearchTerms)
    {
        var searchTerms = LiveRagConnectorHelpers.CreateSearchTerms(question, seedSearchTerms, 12);
        var foldedQuestion = LiveRagConnectorHelpers.FoldText($"{question} {string.Join(" ", searchTerms)}");

        foreach (var profile in profiles)
        {
            var target = ChooseBestTarget(profile, foldedQuestion);
            var sortField = ChooseBestSortField(profile, target, foldedQuestion);
            var operationType = ChooseProfileAwareOperationType(profile, foldedQuestion, target, sortField);
            var selectedFields = ChooseBestSelectFields(profile, target, sortField, foldedQuestion);
            var isDegraded = !profile.SupportsNativeLiveRag;

            yield return new LiveRagOperation
            {
                DataSourceId = profile.DataSourceId,
                SourceName = profile.SourceName,
                ConnectorId = profile.ConnectorId,
                Type = operationType,
                Query = question,
                SearchTerms = searchTerms.ToList(),
                Target = target,
                SelectFields = selectedFields,
                SortField = sortField,
                SortDescending = !LooksLikeAscendingRequest(foldedQuestion),
                Limit = Math.Max(1, limit),
                IsDegradedFallback = isDegraded,
                Rationale = isDegraded
                    ? "AI planning unavailable or invalid; connector has no native live RAG operation, using bounded fallback."
                    : "AI planning unavailable or invalid; using bounded source-profile live operation."
            };
        }
    }

    private static LiveRagOperationType ChooseProfileAwareOperationType(
        LiveRagSourceProfile profile,
        string foldedQuestion,
        string? target,
        string? sortField)
    {
        var supported = profile.SupportedOperations;
        var isStructuredSource = supported.Contains(LiveRagOperationType.TopN) ||
                                 supported.Contains(LiveRagOperationType.Aggregate) ||
                                 supported.Contains(LiveRagOperationType.StructuredQuery);
        var hasStructuredAnchor = !string.IsNullOrWhiteSpace(target) ||
                                  !string.IsNullOrWhiteSpace(sortField) ||
                                  profile.Targets.Count == 1;

        if (isStructuredSource && hasStructuredAnchor && LooksLikeAggregateRequest(foldedQuestion) && supported.Contains(LiveRagOperationType.Aggregate))
            return LiveRagOperationType.Aggregate;

        if (isStructuredSource && hasStructuredAnchor && supported.Contains(LiveRagOperationType.TopN))
            return LiveRagOperationType.TopN;

        if (supported.Contains(LiveRagOperationType.ContentScan))
            return LiveRagOperationType.ContentScan;

        if (supported.Contains(LiveRagOperationType.KeywordSearch))
            return LiveRagOperationType.KeywordSearch;

        if (supported.Contains(LiveRagOperationType.FilteredFetch))
            return LiveRagOperationType.FilteredFetch;

        if (supported.Contains(LiveRagOperationType.NativeSemantic))
            return LiveRagOperationType.NativeSemantic;

        return LiveRagOperationType.KeywordSearch;
    }

    private static string? ChooseBestTarget(LiveRagSourceProfile profile, string foldedQuestion)
    {
        var scoredTargets = profile.Targets
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Select(target => new
            {
                Target = target,
                Score = ScoreTarget(profile, target, foldedQuestion)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Target)
            .ToList();

        var best = scoredTargets.FirstOrDefault(item => item.Score > 0);
        if (best != null)
            return best.Target;

        return profile.Targets.Count == 1 ? profile.Targets[0] : null;
    }

    private static int ScoreTarget(LiveRagSourceProfile profile, string target, string foldedQuestion)
    {
        var score = IdentifierAppears(foldedQuestion, target) || TableConceptAppears(target, foldedQuestion)
            ? 100
            : 0;

        foreach (var field in FieldsForTarget(profile, target))
        {
            if (IdentifierAppears(foldedQuestion, FieldNameOnly(field)) || FieldConceptAppears(field, foldedQuestion))
                score += 15;
        }

        return score;
    }

    private static string? ChooseBestSortField(
        LiveRagSourceProfile profile,
        string? target,
        string foldedQuestion)
    {
        var fields = FieldsForTarget(profile, target).ToList();
        var best = fields
            .Select(field => new
            {
                Field = field,
                Score = ScoreSortField(field, foldedQuestion)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => FieldNameOnly(item.Field))
            .FirstOrDefault();

        return best == null ? null : FieldNameOnly(best.Field);
    }

    private static int ScoreSortField(string field, string foldedQuestion)
    {
        var name = FieldNameOnly(field);
        var foldedName = LiveRagConnectorHelpers.FoldText(name);
        var score = 0;

        if (IdentifierAppears(foldedQuestion, name))
            score += 80;
        if (FieldConceptAppears(field, foldedQuestion))
            score += 70;
        if (LooksNumericMeasureName(foldedName))
            score += 20;
        if (foldedName is "id" or "rowid")
            score -= 30;

        return score;
    }

    private static List<string> ChooseBestSelectFields(
        LiveRagSourceProfile profile,
        string? target,
        string? sortField,
        string foldedQuestion)
    {
        var selected = new List<string>();
        var fields = FieldsForTarget(profile, target).ToList();

        foreach (var field in fields.Where(IsNameLikeField))
            AddField(selected, FieldNameOnly(field));

        foreach (var field in fields.Where(field =>
            IdentifierAppears(foldedQuestion, FieldNameOnly(field)) || FieldConceptAppears(field, foldedQuestion)))
        {
            AddField(selected, FieldNameOnly(field));
        }

        if (!string.IsNullOrWhiteSpace(sortField))
            AddField(selected, sortField);

        foreach (var field in fields.Take(3))
            AddField(selected, FieldNameOnly(field));

        return selected.Take(8).ToList();
    }

    private static IEnumerable<string> FieldsForTarget(LiveRagSourceProfile profile, string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return profile.Fields;

        return profile.Fields.Where(field =>
            field.StartsWith($"{target}.", StringComparison.OrdinalIgnoreCase) ||
            !field.Contains('.', StringComparison.Ordinal));
    }

    private static string FieldNameOnly(string field)
    {
        if (string.IsNullOrWhiteSpace(field))
            return string.Empty;

        var index = field.LastIndexOf('.');
        return index >= 0 && index + 1 < field.Length
            ? field[(index + 1)..]
            : field;
    }

    private static void AddField(List<string> selected, string? field)
    {
        if (!string.IsNullOrWhiteSpace(field) &&
            !selected.Contains(field, StringComparer.OrdinalIgnoreCase))
        {
            selected.Add(field);
        }
    }

    private static bool HasSameOperationSignature(LiveRagOperation left, LiveRagOperation right)
    {
        return string.Equals(left.DataSourceId, right.DataSourceId, StringComparison.OrdinalIgnoreCase) &&
               left.Type == right.Type &&
               string.Equals(left.Query, right.Query, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.Target, right.Target, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.SortField, right.SortField, StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeAggregateRequest(string foldedText)
    {
        return ContainsAny(
            foldedText,
            "count",
            "how many",
            "wie viele",
            "anzahl",
            "sum",
            "total",
            "gesamt",
            "durchschnitt",
            "average",
            "avg");
    }

    private static bool LooksLikeAscendingRequest(string foldedText)
    {
        return ContainsAny(
            foldedText,
            "kleinste",
            "kleinsten",
            "niedrigste",
            "niedrigsten",
            "lowest",
            "least",
            "smallest",
            "ascending",
            "aufsteigend",
            "asc");
    }

    private static bool IdentifierAppears(string foldedText, string identifier)
    {
        var foldedIdentifier = LiveRagConnectorHelpers.FoldText(identifier);
        if (string.IsNullOrWhiteSpace(foldedIdentifier))
            return false;

        if (foldedText.Contains(foldedIdentifier, StringComparison.Ordinal))
            return true;

        var spaced = foldedIdentifier.Replace("_", " ");
        if (!string.Equals(spaced, foldedIdentifier, StringComparison.Ordinal) &&
            foldedText.Contains(spaced, StringComparison.Ordinal))
        {
            return true;
        }

        var singular = ToSimpleSingular(foldedIdentifier);
        return !string.Equals(singular, foldedIdentifier, StringComparison.Ordinal) &&
               foldedText.Contains(singular, StringComparison.Ordinal);
    }

    private static bool TableConceptAppears(string tableName, string foldedText)
    {
        var foldedName = LiveRagConnectorHelpers.FoldText(tableName);
        if ((foldedName.Contains("city", StringComparison.Ordinal) ||
             foldedName.Contains("cities", StringComparison.Ordinal) ||
             foldedName.Contains("stadt", StringComparison.Ordinal)) &&
            ContainsAny(foldedText, "city", "cities", "stadt", "staedte"))
        {
            return true;
        }

        return false;
    }

    private static bool FieldConceptAppears(string field, string foldedText)
    {
        var foldedName = LiveRagConnectorHelpers.FoldText(FieldNameOnly(field));

        if ((foldedName.Contains("population", StringComparison.Ordinal) ||
             foldedName.Contains("inhabitant", StringComparison.Ordinal) ||
             foldedName.Contains("einwohner", StringComparison.Ordinal) ||
             foldedName is "pop" or "pop_total") &&
            ContainsAny(foldedText, "population", "inhabitants", "einwohner", "einwohnerzahl", "bevoelkerung"))
        {
            return true;
        }

        if (IsNameLikeField(field) && ContainsAny(foldedText, "name", "namen", "stadtname", "city name", "city_name"))
            return true;

        if ((foldedName.Contains("date", StringComparison.Ordinal) ||
             foldedName.Contains("datum", StringComparison.Ordinal) ||
             foldedName.Contains("created", StringComparison.Ordinal) ||
             foldedName.Contains("modified", StringComparison.Ordinal)) &&
            ContainsAny(foldedText, "date", "datum", "zeit", "created", "modified", "geaendert", "erstellt"))
        {
            return true;
        }

        return false;
    }

    private static bool IsNameLikeField(string field)
    {
        var name = LiveRagConnectorHelpers.FoldText(FieldNameOnly(field));
        return name is "name" or "title" or "city" or "city_name" or "cityname" or "stadt" or "stadtname" ||
               name.EndsWith("_name", StringComparison.Ordinal) ||
               name.EndsWith("name", StringComparison.Ordinal);
    }

    private static bool LooksNumericMeasureName(string foldedName)
    {
        return foldedName.Contains("count", StringComparison.Ordinal) ||
               foldedName.Contains("total", StringComparison.Ordinal) ||
               foldedName.Contains("sum", StringComparison.Ordinal) ||
               foldedName.Contains("amount", StringComparison.Ordinal) ||
               foldedName.Contains("price", StringComparison.Ordinal) ||
               foldedName.Contains("population", StringComparison.Ordinal) ||
               foldedName.Contains("einwohner", StringComparison.Ordinal) ||
               foldedName.Contains("score", StringComparison.Ordinal) ||
               foldedName.Contains("rating", StringComparison.Ordinal) ||
               foldedName.Contains("size", StringComparison.Ordinal) ||
               foldedName.Contains("revenue", StringComparison.Ordinal) ||
               foldedName.Contains("umsatz", StringComparison.Ordinal);
    }

    private static string ToSimpleSingular(string value)
    {
        if (value.EndsWith("ies", StringComparison.Ordinal) && value.Length > 3)
            return value[..^3] + "y";
        if (value.EndsWith("s", StringComparison.Ordinal) && value.Length > 2)
            return value[..^1];
        return value;
    }

    private static bool ContainsAny(string text, params string[] candidates)
    {
        return candidates.Any(candidate => text.Contains(LiveRagConnectorHelpers.FoldText(candidate), StringComparison.Ordinal));
    }

    private static void Reject(
        LiveRagOperation operation,
        LiveRagExecutionTrace trace,
        string reason,
        List<LiveRagOperation> rejected,
        List<LiveRagExecutionTrace> traces)
    {
        operation.Options["rejectedReason"] = reason;
        trace.Accepted = false;
        trace.Reason = reason;
        rejected.Add(operation);
        traces.Add(trace);
    }

    private static bool ContainsDangerousQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;

        var folded = $" {LiveRagConnectorHelpers.FoldText(query)} ";
        return DangerousQueryTokens.Any(token => folded.Contains(token, StringComparison.Ordinal));
    }
}

public sealed record LiveRagValidationResult(
    List<LiveRagOperation> AcceptedOperations,
    List<LiveRagOperation> RejectedOperations,
    List<LiveRagExecutionTrace> Traces);
