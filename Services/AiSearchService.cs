//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Net.Http;
using System.Text;
using System.Text.Json;
using MSCC.Models;

namespace MSCC.Services;

/// <summary>
/// Service for AI-powered search result analysis.
/// </summary>
public class AiSearchService
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(120) };

    /// <summary>
    /// Sends search results to an OpenAI-compatible API with a system prompt.
    /// </summary>
    public async Task<AiSearchResponse> AnalyzeResultsAsync(
        IEnumerable<SearchResult> results,
        string systemPrompt,
        string? userQuery = null,
        CancellationToken cancellationToken = default)
    {
        var settings = SettingsService.Instance.Settings;
        
        if (string.IsNullOrWhiteSpace(settings.AiApiEndpoint))
        {
            return new AiSearchResponse
            {
                Success = false,
                ErrorMessage = "AI API endpoint is not configured. Please configure it in Settings."
            };
        }

        if (string.IsNullOrWhiteSpace(settings.AiApiKey))
        {
            return new AiSearchResponse
            {
                Success = false,
                ErrorMessage = "AI API key is not configured. Please configure it in Settings."
            };
        }

        try
        {
            // Build the context from search results
            var resultsContext = BuildResultsContext(results);
            
            // Build user message
            var userMessage = string.IsNullOrWhiteSpace(userQuery)
                ? $"Here are the search results to analyze:\n\n{resultsContext}"
                : $"Query: {userQuery}\n\nSearch results:\n\n{resultsContext}";

            return await SendChatCompletionAsync(
                systemPrompt,
                userMessage,
                maxTokens: 4000,
                temperature: 0.7,
                cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return new AiSearchResponse
            {
                Success = false,
                ErrorMessage = "Request was cancelled or timed out."
            };
        }
        catch (Exception ex)
        {
            return new AiSearchResponse
            {
                Success = false,
                ErrorMessage = $"Error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Creates a compact live RAG search plan from a natural-language question.
    /// </summary>
    public async Task<AiSearchPlanResponse> CreateLiveRagSearchPlanAsync(
        string question,
        IEnumerable<DataSource> dataSources,
        CancellationToken cancellationToken = default)
    {
        var fallbackTerms = CreateFallbackSearchTerms(question);

        var sourceList = string.Join("\n", dataSources
            .Take(50)
            .Select((source, index) =>
                $"{index + 1}. {source.Name} ({source.ConnectorId}) - {source.Description}"));

        var systemPrompt = """
            You create retrieval plans for live RAG over dynamic desktop data sources.
            Return ONLY a valid JSON object with this exact schema:
            {
              "searchTerms": ["term 1", "term 2", "term 3"]
            }
            Use concise terms that a plugin can execute immediately. Prefer 3 to 6 terms.
            Include synonyms and entity names when helpful. Do not answer the question.
            """;

        var userMessage = $"""
            Question:
            {question}

            Available data sources:
            {sourceList}
            """;

        var response = await SendChatCompletionAsync(
            systemPrompt,
            userMessage,
            maxTokens: 800,
            temperature: 0.2,
            cancellationToken);

        if (!response.Success)
        {
            return new AiSearchPlanResponse
            {
                Success = false,
                ErrorMessage = response.ErrorMessage,
                SearchTerms = fallbackTerms
            };
        }

        var searchTerms = ParseSearchTermsFromJson(response.Response);
        if (searchTerms.Count == 0)
        {
            searchTerms = fallbackTerms;
        }

        return new AiSearchPlanResponse
        {
            Success = true,
            SearchTerms = searchTerms,
            RawResponse = response.Response,
            Model = response.Model,
            PromptTokens = response.PromptTokens,
            CompletionTokens = response.CompletionTokens,
            TotalTokens = response.TotalTokens
        };
    }

    /// <summary>
    /// Generates the final RAG answer from live context items.
    /// </summary>
    public async Task<AiSearchResponse> AnswerLiveRagAsync(
        LiveRagContextResult context,
        string systemPrompt,
        CancellationToken cancellationToken = default)
    {
        if (context.ContextItems.Count == 0)
        {
            return new AiSearchResponse
            {
                Success = false,
                ErrorMessage = BuildLiveRagFailureMessage(context)
            };
        }

        var liveContext = BuildLiveRagContext(context.ContextItems);
        var searchTerms = string.Join(", ", context.SearchTerms);
        var planContext = JsonSerializer.Serialize(context.PlanOperations.Select(operation => new
        {
            operation.Id,
            type = operation.Type.ToString(),
            operation.SourceName,
            operation.Query,
            operation.Target,
            operation.SortField,
            operation.Limit,
            operation.IsDegradedFallback
        }));

        var userMessage = $"""
            User question:
            {context.Question}

            Retrieval terms used:
            {searchTerms}

            Validated live retrieval plan:
            {planContext}

            Live RAG context:
            {liveContext}
            """;

        return await SendChatCompletionAsync(
            systemPrompt,
            userMessage,
            maxTokens: 4000,
            temperature: 0.4,
            cancellationToken);
    }

    public static string BuildLiveRagFailureMessage(LiveRagContextResult context)
    {
        var sb = new StringBuilder();
        sb.AppendLine(context.ErrorMessage ?? "Es konnte kein Live-RAG-Kontext aus den Datenquellen geladen werden.");

        if (context.Diagnostics.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Diagnose:");
            foreach (var diagnostic in context.Diagnostics.Take(8))
            {
                var value = diagnostic.Value?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    sb.AppendLine($"- {diagnostic.Key}: {value}");
            }
        }

        foreach (var source in context.SourceResults.Take(5))
        {
            sb.AppendLine();
            sb.AppendLine($"Quelle: {source.SourceName} ({source.ConnectorId})");
            sb.AppendLine($"- Kontext-Chunks: {source.ContextItems.Count}");

            if (!string.IsNullOrWhiteSpace(source.ErrorMessage))
                sb.AppendLine($"- Fehler: {source.ErrorMessage}");

            if (source.ExecutedQueries.Count > 0)
                sb.AppendLine($"- Ausgefuehrte Queries: {string.Join(" | ", source.ExecutedQueries.Take(3))}");

            if (source.RejectedOperations.Count > 0)
                sb.AppendLine($"- Verworfene Operationen: {source.RejectedOperations.Count}");
        }

        if (context.ExecutionTrace.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Operationen:");
            foreach (var trace in context.ExecutionTrace.Take(8))
            {
                var status = trace.Accepted ? "ok" : "abgelehnt";
                var detail = !string.IsNullOrWhiteSpace(trace.ErrorMessage)
                    ? trace.ErrorMessage
                    : trace.Reason;
                sb.AppendLine($"- {trace.SourceName}: {trace.OperationType} ({status}), Ergebnisse: {trace.ResultCount}. {detail}");
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Answers a live RAG question through an MCP-style tool bridge.
    /// The LLM calls the bridge on demand; selected plugins are queried only for each tool call.
    /// </summary>
    public async Task<AiSearchResponse> AnswerLiveRagWithToolBridgeAsync(
        string question,
        string systemPrompt,
        SearchService searchService,
        IEnumerable<DataSource> selectedDataSources,
        int maxResultsPerSearchTerm,
        int maxContextItemsPerSource,
        int maxContextItemsTotal,
        int maxCharactersPerItem,
        bool includeMetadata,
        CancellationToken cancellationToken = default,
        IProgress<(string query, string sourceName, int contextCount, bool isNativeLiveRag)>? progress = null)
    {
        var selectedSources = selectedDataSources
            .Where(source => source.IsEnabled)
            .ToList();

        if (selectedSources.Count == 0)
        {
            return new AiSearchResponse
            {
                Success = false,
                ErrorMessage = "No data sources selected."
            };
        }

        var settings = SettingsService.Instance.Settings;
        var validationError = ValidateAiSettings(settings);
        if (validationError != null)
            return validationError;

        var selectedSourceIds = selectedSources
            .Select(source => source.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sourceCatalog = string.Join("\n", selectedSources.Select(source =>
            $"- id: {source.Id}; name: {source.Name}; connector: {source.ConnectorId}; description: {source.Description}"));

        var bridgeSystemPrompt = $"""
            {systemPrompt}

            Live RAG mode:
            You have access to a live retrieval tool named search_live_rag_sources.
            Use the tool whenever you need facts from the user's data sources.
            Do not assume data source contents without using the tool.
            Ask focused retrieval queries. You may call the tool multiple times for different sub-questions.
            When the user's wording is broad, infer concrete retrieval terms, synonyms, file names, table/field names, subject words, entity names, and date words.
            Put those concrete terms into searchTerms so keyword-oriented plugins can still return useful live context.
            The tool returns compact, cited context chunks from plugins; cite them as [Source N].
            If the context is insufficient, say that clearly.

            Available selected data sources:
            {sourceCatalog}
            """;

        var messages = new List<Dictionary<string, object?>>
        {
            new() { ["role"] = "system", ["content"] = bridgeSystemPrompt },
            new() { ["role"] = "user", ["content"] = question }
        };

        var aggregatedContext = new LiveRagContextResult
        {
            Question = question
        };

        var promptTokens = 0;
        var completionTokens = 0;
        var totalTokens = 0;
        var model = settings.AiModel;
        string? finishReason = null;

        for (var round = 0; round < 5; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var completion = await SendChatCompletionWithToolsAsync(
                messages,
                CreateLiveRagToolsDefinition(),
                maxTokens: 4000,
                temperature: 0.2,
                cancellationToken);

            if (!completion.Success)
            {
                completion.LiveRagContext = aggregatedContext;
                return completion;
            }

            promptTokens += completion.PromptTokens;
            completionTokens += completion.CompletionTokens;
            totalTokens += completion.TotalTokens;
            model = completion.Model ?? model;
            finishReason = completion.FinishReason;

            if (completion.ToolCalls.Count == 0)
            {
                completion.PromptTokens = promptTokens;
                completion.CompletionTokens = completionTokens;
                completion.TotalTokens = totalTokens;
                completion.Model = model;
                completion.FinishReason = finishReason;
                completion.LiveRagContext = aggregatedContext;
                completion.ToolCallCount = aggregatedContext.SourceResults.Count;
                return completion;
            }

            messages.Add(new Dictionary<string, object?>
            {
                ["role"] = "assistant",
                ["content"] = completion.Response,
                ["tool_calls"] = completion.ToolCalls.Select(call => new Dictionary<string, object?>
                {
                    ["id"] = call.Id,
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object?>
                    {
                        ["name"] = call.Name,
                        ["arguments"] = call.Arguments
                    }
                }).ToList()
            });

            foreach (var toolCall in completion.ToolCalls)
            {
                if (!string.Equals(toolCall.Name, "search_live_rag_sources", StringComparison.Ordinal))
                {
                    messages.Add(CreateToolMessage(toolCall.Id, "{\"success\":false,\"error\":\"Unknown tool.\"}"));
                    continue;
                }

                var toolRequest = ParseLiveRagToolRequest(toolCall.Arguments);
                var query = string.IsNullOrWhiteSpace(toolRequest.Query)
                    ? question
                    : toolRequest.Query;

                var requestedSourceIds = toolRequest.SourceIds.Count > 0
                    ? toolRequest.SourceIds
                        .Where(id => selectedSourceIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                    : selectedSourceIds;

                if (requestedSourceIds.Count == 0)
                {
                    requestedSourceIds = selectedSourceIds;
                }

                var requestedContextLimit = toolRequest.MaxContextItems.HasValue
                    ? Math.Clamp(toolRequest.MaxContextItems.Value, 1, maxContextItemsTotal)
                    : maxContextItemsTotal;

                var sourceProgress = new Progress<(string sourceName, int contextCount, bool isNativeLiveRag)>(p =>
                {
                    progress?.Report((query, p.sourceName, p.contextCount, p.isNativeLiveRag));
                });

                var context = await searchService.GetLiveRagContextAsync(
                    query,
                    requestedSourceIds,
                    groupIds: null,
                    searchTerms: toolRequest.SearchTerms.Count > 0
                        ? toolRequest.SearchTerms
                        : new[] { query },
                    maxResultsPerSearchTerm: maxResultsPerSearchTerm,
                    maxContextItemsPerSource: maxContextItemsPerSource,
                    maxContextItemsTotal: requestedContextLimit,
                    maxCharactersPerItem: maxCharactersPerItem,
                    includeMetadata: includeMetadata,
                    cancellationToken: cancellationToken,
                    progress: sourceProgress);

                MergeLiveRagContext(aggregatedContext, context);

                var toolResponseJson = BuildLiveRagToolResponseJson(context, aggregatedContext.ContextItems.Count);
                messages.Add(CreateToolMessage(toolCall.Id, toolResponseJson));
            }
        }

        var finalResponse = await SendChatCompletionAsync(
            systemPrompt,
            $"""
            User question:
            {question}

            The live RAG tool was called repeatedly. Use the retrieved source context below to provide the final answer.
            {BuildLiveRagContext(aggregatedContext.ContextItems)}
            """,
            maxTokens: 4000,
            temperature: 0.2,
            cancellationToken);

        finalResponse.LiveRagContext = aggregatedContext;
        finalResponse.ToolCallCount = aggregatedContext.SourceResults.Count;
        return finalResponse;
    }

    private static string BuildResultsContext(IEnumerable<SearchResult> results)
    {
        var sb = new StringBuilder();
        int index = 1;

        foreach (var result in results)
        {
            sb.AppendLine($"--- Result {index} ---");
            sb.AppendLine($"Title: {result.Title}");
            sb.AppendLine($"Description: {result.Description}");
            sb.AppendLine($"Source: {result.SourceName}");
            sb.AppendLine($"Relevance: {result.RelevanceScore}%");

            if (result.Metadata.Count > 0)
            {
                sb.AppendLine("Metadata:");
                foreach (var kvp in result.Metadata)
                {
                    var value = kvp.Value?.ToString() ?? "";
                    // Truncate long values
                    if (value.Length > 500)
                        value = value.Substring(0, 500) + "...";
                    sb.AppendLine($"  {kvp.Key}: {value}");
                }
            }

            sb.AppendLine();
            index++;
        }

        return sb.ToString();
    }

    private async Task<AiSearchResponse> SendChatCompletionAsync(
        string systemPrompt,
        string userMessage,
        int maxTokens,
        double temperature,
        CancellationToken cancellationToken)
    {
        var settings = SettingsService.Instance.Settings;
        var validationError = ValidateAiSettings(settings);
        if (validationError != null)
            return validationError;

        try
        {
            var requestBody = new
            {
                model = settings.AiModel,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                },
                max_tokens = maxTokens,
                temperature
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.AiApiEndpoint);
            request.Content = content;
            request.Headers.Add("Authorization", $"Bearer {settings.AiApiKey}");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new AiSearchResponse
                {
                    Success = false,
                    ErrorMessage = $"API Error ({response.StatusCode}): {responseJson}"
                };
            }

            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            var aiResponse = new AiSearchResponse { Success = true };

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var contentElement))
                {
                    aiResponse.Response = contentElement.GetString() ?? "";
                }

                if (firstChoice.TryGetProperty("finish_reason", out var finishReason))
                {
                    aiResponse.FinishReason = finishReason.GetString();
                }
            }

            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var promptTokens))
                    aiResponse.PromptTokens = promptTokens.GetInt32();
                if (usage.TryGetProperty("completion_tokens", out var completionTokens))
                    aiResponse.CompletionTokens = completionTokens.GetInt32();
                if (usage.TryGetProperty("total_tokens", out var totalTokens))
                    aiResponse.TotalTokens = totalTokens.GetInt32();
            }

            if (root.TryGetProperty("model", out var model))
            {
                aiResponse.Model = model.GetString();
            }

            return aiResponse;
        }
        catch (TaskCanceledException)
        {
            return new AiSearchResponse
            {
                Success = false,
                ErrorMessage = "AI request was cancelled or timed out."
            };
        }
        catch (HttpRequestException ex)
        {
            return new AiSearchResponse
            {
                Success = false,
                ErrorMessage = $"AI API connection failed: {ex.Message}"
            };
        }
        catch (JsonException ex)
        {
            return new AiSearchResponse
            {
                Success = false,
                ErrorMessage = $"AI API response parsing failed: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new AiSearchResponse
            {
                Success = false,
                ErrorMessage = $"AI request error: {ex.Message}"
            };
        }
    }

    private async Task<AiSearchResponse> SendChatCompletionWithToolsAsync(
        List<Dictionary<string, object?>> messages,
        object[] tools,
        int maxTokens,
        double temperature,
        CancellationToken cancellationToken)
    {
        var settings = SettingsService.Instance.Settings;
        var validationError = ValidateAiSettings(settings);
        if (validationError != null)
            return validationError;

        try
        {
            var requestBody = new Dictionary<string, object?>
            {
                ["model"] = settings.AiModel,
                ["messages"] = messages,
                ["tools"] = tools,
                ["tool_choice"] = "auto",
                ["max_tokens"] = maxTokens,
                ["temperature"] = temperature
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.AiApiEndpoint);
            request.Content = content;
            request.Headers.Add("Authorization", $"Bearer {settings.AiApiKey}");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new AiSearchResponse
                {
                    Success = false,
                    ErrorMessage = $"API Error ({response.StatusCode}): {responseJson}"
                };
            }

            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            var aiResponse = new AiSearchResponse { Success = true };

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message))
                {
                    if (message.TryGetProperty("content", out var contentElement) &&
                        contentElement.ValueKind != JsonValueKind.Null)
                    {
                        aiResponse.Response = contentElement.GetString() ?? "";
                    }

                    if (message.TryGetProperty("tool_calls", out var toolCalls) &&
                        toolCalls.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var toolCall in toolCalls.EnumerateArray())
                        {
                            var id = toolCall.TryGetProperty("id", out var idElement)
                                ? idElement.GetString() ?? Guid.NewGuid().ToString()
                                : Guid.NewGuid().ToString();

                            if (!toolCall.TryGetProperty("function", out var functionElement))
                                continue;

                            var name = functionElement.TryGetProperty("name", out var nameElement)
                                ? nameElement.GetString() ?? ""
                                : "";
                            var arguments = functionElement.TryGetProperty("arguments", out var argumentsElement)
                                ? argumentsElement.GetString() ?? "{}"
                                : "{}";

                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                aiResponse.ToolCalls.Add(new AiToolCall
                                {
                                    Id = id,
                                    Name = name,
                                    Arguments = arguments
                                });
                            }
                        }
                    }
                }

                if (firstChoice.TryGetProperty("finish_reason", out var finishReason))
                {
                    aiResponse.FinishReason = finishReason.GetString();
                }
            }

            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var promptTokens))
                    aiResponse.PromptTokens = promptTokens.GetInt32();
                if (usage.TryGetProperty("completion_tokens", out var completionTokens))
                    aiResponse.CompletionTokens = completionTokens.GetInt32();
                if (usage.TryGetProperty("total_tokens", out var totalTokens))
                    aiResponse.TotalTokens = totalTokens.GetInt32();
            }

            if (root.TryGetProperty("model", out var model))
            {
                aiResponse.Model = model.GetString();
            }

            return aiResponse;
        }
        catch (TaskCanceledException)
        {
            return new AiSearchResponse
            {
                Success = false,
                ErrorMessage = "AI request was cancelled or timed out."
            };
        }
        catch (HttpRequestException ex)
        {
            return new AiSearchResponse
            {
                Success = false,
                ErrorMessage = $"AI API connection failed: {ex.Message}"
            };
        }
        catch (JsonException ex)
        {
            return new AiSearchResponse
            {
                Success = false,
                ErrorMessage = $"AI API response parsing failed: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new AiSearchResponse
            {
                Success = false,
                ErrorMessage = $"AI request error: {ex.Message}"
            };
        }
    }

    private static AiSearchResponse? ValidateAiSettings(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.AiApiEndpoint))
        {
            return new AiSearchResponse
            {
                Success = false,
                ErrorMessage = "AI API endpoint is not configured. Please configure it in Settings."
            };
        }

        if (string.IsNullOrWhiteSpace(settings.AiApiKey))
        {
            return new AiSearchResponse
            {
                Success = false,
                ErrorMessage = "AI API key is not configured. Please configure it in Settings."
            };
        }

        return null;
    }

    public async Task<LiveRagPlanResult> CreateHybridLiveRagPlanAsync(
        string question,
        IEnumerable<LiveRagSourceProfile> sourceProfiles,
        int maxOperationsPerSource,
        int maxCandidateItems,
        CancellationToken cancellationToken = default)
    {
        var settings = SettingsService.Instance.Settings;
        var validationError = ValidateAiSettings(settings);
        if (validationError != null)
        {
            return new LiveRagPlanResult
            {
                Success = false,
                IsDegradedFallback = true,
                ErrorMessage = validationError.ErrorMessage
            };
        }

        var profiles = sourceProfiles.ToList();
        var profileJson = JsonSerializer.Serialize(profiles.Select(profile => new
        {
            profile.DataSourceId,
            profile.SourceName,
            profile.ConnectorId,
            profile.Description,
            supportedOperations = profile.SupportedOperations.Select(operation => operation.ToString()),
            profile.Fields,
            profile.Targets,
            profile.MaxOperations,
            profile.MaxResults,
            profile.Metadata
        }));

        var systemPrompt = """
            You plan live RAG retrieval for a desktop meta-search application.
            Return ONLY a valid JSON object with this exact shape:
            {
              "operations": [
                {
                  "dataSourceId": "source id",
                  "type": "KeywordSearch|ContentScan|FilteredFetch|StructuredQuery|TopN|Aggregate|FetchById|NativeSemantic",
                  "query": "focused natural language retrieval intent or safe read-only query",
                  "searchTerms": ["term or phrase"],
                  "target": "table, folder, message scope, API resource, or empty",
                  "selectFields": ["field"],
                  "filters": { "field": "value" },
                  "sortField": "field or empty",
                  "sortDescending": true,
                  "limit": 10,
                  "rationale": "short reason"
                }
              ]
            }
            Use only dataSourceId values and operation types present in the source profiles.
            Prefer connector-native structured operations when fields/targets are available.
            Do not request a full dump. Keep limits small and source-specific.
            Do not answer the user question.
            """;

        var userMessage = $"""
            User question:
            {question}

            Max operations per source: {Math.Max(1, maxOperationsPerSource)}
            Max candidate items per source: {Math.Max(1, maxCandidateItems)}

            Source profiles:
            {profileJson}
            """;

        var response = await SendChatCompletionAsync(
            systemPrompt,
            userMessage,
            maxTokens: 2000,
            temperature: 0.1,
            cancellationToken);

        if (!response.Success)
        {
            return new LiveRagPlanResult
            {
                Success = false,
                IsDegradedFallback = true,
                ErrorMessage = response.ErrorMessage,
                RawResponse = response.Response
            };
        }

        var operations = ParseLiveRagOperationsFromJson(response.Response);
        return new LiveRagPlanResult
        {
            Success = operations.Count > 0,
            ErrorMessage = operations.Count == 0 ? "The AI planner returned no valid operations." : null,
            RawResponse = response.Response,
            Operations = operations,
            Diagnostics =
            {
                ["model"] = response.Model ?? settings.AiModel,
                ["promptTokens"] = response.PromptTokens,
                ["completionTokens"] = response.CompletionTokens,
                ["totalTokens"] = response.TotalTokens
            }
        };
    }

    public async Task<List<LiveRagContextItem>> RerankLiveRagContextAsync(
        string question,
        IEnumerable<LiveRagContextItem> contextItems,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        var items = contextItems
            .OrderByDescending(item => item.RelevanceScore)
            .Take(Math.Max(1, maxItems) * 3)
            .ToList();

        if (items.Count <= Math.Max(1, maxItems))
            return items;

        var settings = SettingsService.Instance.Settings;
        if (ValidateAiSettings(settings) != null)
            return items.Take(Math.Max(1, maxItems)).ToList();

        var evidenceJson = JsonSerializer.Serialize(items.Select(item => new
        {
            item.Id,
            item.Title,
            item.SourceName,
            item.ConnectorId,
            item.OperationType,
            item.RelevanceScore,
            content = item.Content.Length > 900 ? item.Content[..900] : item.Content
        }));

        var response = await SendChatCompletionAsync(
            """
            Rank live RAG evidence chunks for answering the user question.
            Return ONLY JSON: { "rankedIds": ["id1", "id2"] }.
            Keep only chunks that directly help answer the question.
            """,
            $"""
            Question:
            {question}

            Evidence:
            {evidenceJson}
            """,
            maxTokens: 800,
            temperature: 0,
            cancellationToken);

        if (!response.Success)
            return items.Take(Math.Max(1, maxItems)).ToList();

        var rankedIds = ParseRankedIds(response.Response);
        if (rankedIds.Count == 0)
            return items.Take(Math.Max(1, maxItems)).ToList();

        var byId = items.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var ranked = rankedIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .Take(Math.Max(1, maxItems))
            .ToList();

        if (ranked.Count < Math.Max(1, maxItems))
        {
            ranked.AddRange(items
                .Where(item => !ranked.Any(existing => existing.Id == item.Id))
                .Take(Math.Max(1, maxItems) - ranked.Count));
        }

        return ranked;
    }

    private static object[] CreateLiveRagToolsDefinition()
    {
        return
        [
            new
            {
                type = "function",
                function = new
                {
                    name = "search_live_rag_sources",
                    description = "Search selected desktop data sources through their live plugins. Use focused queries. Returns only compact cited context chunks, not full data dumps.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new
                            {
                                type = "string",
                                description = "Focused retrieval query or sub-question for the plugins."
                            },
                            sourceIds = new
                            {
                                type = "array",
                                description = "Optional selected data source IDs to query. Omit to query all selected sources.",
                                items = new { type = "string" }
                            },
                            searchTerms = new
                            {
                                type = "array",
                                description = "Concrete keyword phrases, synonyms, entity names, file names, table/field names, date words, or API search terms for keyword-oriented plugins.",
                                items = new { type = "string" }
                            },
                            maxContextItems = new
                            {
                                type = "integer",
                                description = "Optional maximum context chunks to return for this tool call."
                            }
                        },
                        required = new[] { "query" }
                    }
                }
            }
        ];
    }

    private static LiveRagToolRequest ParseLiveRagToolRequest(string arguments)
    {
        var request = new LiveRagToolRequest();
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(arguments) ? "{}" : arguments);
            var root = doc.RootElement;

            if (root.TryGetProperty("query", out var query) && query.ValueKind == JsonValueKind.String)
                request.Query = query.GetString() ?? "";

            if (root.TryGetProperty("sourceIds", out var sourceIds) && sourceIds.ValueKind == JsonValueKind.Array)
            {
                request.SourceIds = sourceIds
                    .EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? "")
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (root.TryGetProperty("searchTerms", out var searchTerms) && searchTerms.ValueKind == JsonValueKind.Array)
            {
                request.SearchTerms = searchTerms
                    .EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? "")
                    .Where(term => !string.IsNullOrWhiteSpace(term))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(12)
                    .ToList();
            }

            if (root.TryGetProperty("maxContextItems", out var maxContextItems) &&
                maxContextItems.TryGetInt32(out var value))
            {
                request.MaxContextItems = value;
            }
        }
        catch
        {
            request.Query = arguments;
        }

        return request;
    }

    private static Dictionary<string, object?> CreateToolMessage(string toolCallId, string content)
    {
        return new Dictionary<string, object?>
        {
            ["role"] = "tool",
            ["tool_call_id"] = toolCallId,
            ["content"] = content
        };
    }

    private static string BuildLiveRagToolResponseJson(LiveRagContextResult context, int globalContextCount)
    {
        var startIndex = Math.Max(0, globalContextCount - context.ContextItems.Count);
        var response = new
        {
            success = context.Success,
            question = context.Question,
            error = context.ErrorMessage,
            contextItems = context.ContextItems.Select((item, index) => new
            {
                sourceNumber = startIndex + index + 1,
                title = item.Title,
                sourceName = item.SourceName,
                connectorId = item.ConnectorId,
                reference = item.OriginalReference,
                relevance = item.RelevanceScore,
                nativeLiveRag = item.FromNativeLiveRag,
                retrievalQuery = item.RetrievalQuery,
                content = item.Content
            }),
            diagnostics = context.SourceResults.Select(source => new
            {
                sourceName = source.SourceName,
                connectorId = source.ConnectorId,
                nativeLiveRag = source.IsNativeLiveRag,
                contextCount = source.ContextItems.Count,
                executedQueries = source.ExecutedQueries,
                error = source.ErrorMessage
            })
        };

        return JsonSerializer.Serialize(response);
    }

    private static void MergeLiveRagContext(LiveRagContextResult aggregate, LiveRagContextResult next)
    {
        aggregate.SearchTerms.AddRange(next.SearchTerms);
        aggregate.SourceResults.AddRange(next.SourceResults);

        var knownReferences = aggregate.ContextItems
            .Select(item => string.IsNullOrWhiteSpace(item.OriginalReference)
                ? $"{item.ConnectorId}:{item.Title}:{item.Content}"
                : $"{item.ConnectorId}:{item.OriginalReference}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in next.ContextItems)
        {
            var key = string.IsNullOrWhiteSpace(item.OriginalReference)
                ? $"{item.ConnectorId}:{item.Title}:{item.Content}"
                : $"{item.ConnectorId}:{item.OriginalReference}";

            if (knownReferences.Add(key))
            {
                aggregate.ContextItems.Add(item);
            }
        }

        aggregate.Success = aggregate.ContextItems.Count > 0 || aggregate.SourceResults.Any(result => result.Success);
        aggregate.ErrorMessage = aggregate.Success ? null : next.ErrorMessage;
    }

    private static string BuildLiveRagContext(IEnumerable<LiveRagContextItem> contextItems)
    {
        var sb = new StringBuilder();
        var index = 1;

        foreach (var item in contextItems)
        {
            sb.AppendLine($"[Source {index}]");
            sb.AppendLine($"Title: {item.Title}");
            sb.AppendLine($"Source: {item.SourceName}");
            sb.AppendLine($"Connector: {item.ConnectorId}");
            sb.AppendLine($"Reference: {item.OriginalReference}");
            sb.AppendLine($"Relevance: {item.RelevanceScore}%");
            sb.AppendLine($"Native Live RAG: {item.FromNativeLiveRag}");
            if (!string.IsNullOrWhiteSpace(item.RetrievalQuery))
                sb.AppendLine($"Retrieval query: {item.RetrievalQuery}");
            sb.AppendLine("Content:");
            sb.AppendLine(item.Content);

            if (item.Metadata.Count > 0)
            {
                sb.AppendLine("Metadata:");
                foreach (var kvp in item.Metadata.Take(20))
                {
                    var value = kvp.Value?.ToString() ?? "";
                    if (value.Length > 300)
                        value = value[..300] + "...";
                    sb.AppendLine($"  {kvp.Key}: {value}");
                }
            }

            sb.AppendLine();
            index++;
        }

        return sb.ToString();
    }

    private static List<string> ParseSearchTermsFromJson(string response)
    {
        var json = ExtractJsonObject(response);
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("searchTerms", out var searchTerms) ||
                searchTerms.ValueKind != JsonValueKind.Array)
            {
                return new List<string>();
            }

            return searchTerms
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? "")
                .Where(term => !string.IsNullOrWhiteSpace(term))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start
            ? text[start..(end + 1)]
            : string.Empty;
    }

    private static List<LiveRagOperation> ParseLiveRagOperationsFromJson(string response)
    {
        var json = ExtractJsonObject(response);
        if (string.IsNullOrWhiteSpace(json))
            return new List<LiveRagOperation>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("operations", out var operationsElement) ||
                operationsElement.ValueKind != JsonValueKind.Array)
            {
                return new List<LiveRagOperation>();
            }

            var operations = new List<LiveRagOperation>();
            foreach (var operationElement in operationsElement.EnumerateArray())
            {
                if (operationElement.ValueKind != JsonValueKind.Object)
                    continue;

                var operation = new LiveRagOperation
                {
                    DataSourceId = TryGetString(operationElement, "dataSourceId"),
                    Query = TryGetString(operationElement, "query"),
                    Target = TryGetOptionalString(operationElement, "target"),
                    SortField = TryGetOptionalString(operationElement, "sortField"),
                    SortDescending = TryGetBool(operationElement, "sortDescending", true),
                    Limit = TryGetInt(operationElement, "limit", 10, 1, 100),
                    Rationale = TryGetString(operationElement, "rationale")
                };

                if (Enum.TryParse<LiveRagOperationType>(TryGetString(operationElement, "type"), ignoreCase: true, out var operationType))
                    operation.Type = operationType;

                operation.SearchTerms = TryGetStringArray(operationElement, "searchTerms");
                operation.SelectFields = TryGetStringArray(operationElement, "selectFields");
                operation.Filters = TryGetStringMap(operationElement, "filters");

                if (!string.IsNullOrWhiteSpace(operation.DataSourceId))
                    operations.Add(operation);
            }

            return operations;
        }
        catch
        {
            return new List<LiveRagOperation>();
        }
    }

    private static List<string> ParseRankedIds(string response)
    {
        var json = ExtractJsonObject(response);
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("rankedIds", out var rankedIds) ||
                rankedIds.ValueKind != JsonValueKind.Array)
            {
                return new List<string>();
            }

            return rankedIds
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? "")
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string? TryGetOptionalString(JsonElement element, string propertyName)
    {
        var value = TryGetString(element, propertyName);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int TryGetInt(JsonElement element, string propertyName, int defaultValue, int min, int max)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var parsed))
            return Math.Clamp(parsed, min, max);

        return Math.Clamp(defaultValue, min, max);
    }

    private static bool TryGetBool(JsonElement element, string propertyName, bool defaultValue)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : defaultValue;
    }

    private static List<string> TryGetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            return new List<string>();

        return value
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? "")
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
    }

    private static Dictionary<string, string> TryGetStringMap(JsonElement element, string propertyName)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
            return map;

        foreach (var property in value.EnumerateObject())
        {
            var propertyValue = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : property.Value.ToString();

            if (!string.IsNullOrWhiteSpace(property.Name) && !string.IsNullOrWhiteSpace(propertyValue))
                map[property.Name] = propertyValue!;
        }

        return map;
    }

    private static List<string> CreateFallbackSearchTerms(string question)
    {
        var cleanedQuestion = question.Trim();
        if (string.IsNullOrWhiteSpace(cleanedQuestion))
            return new List<string>();

        var terms = new List<string> { cleanedQuestion };
        var keywords = cleanedQuestion
            .Split(new[] { ' ', '\t', '\r', '\n', '.', ',', ';', ':', '!', '?', '"', '\'', '(', ')' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        if (keywords.Count > 0)
            terms.Add(string.Join(" ", keywords));

        terms.AddRange(keywords.Take(4));

        return terms
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

/// <summary>
/// Response from AI analysis.
/// </summary>
public class AiSearchResponse
{
    public bool Success { get; set; }
    public string Response { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public string? Model { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public string? FinishReason { get; set; }
    public List<AiToolCall> ToolCalls { get; set; } = new();
    public int ToolCallCount { get; set; }
    public LiveRagContextResult? LiveRagContext { get; set; }
}

public class AiToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = "{}";
}

internal class LiveRagToolRequest
{
    public string Query { get; set; } = string.Empty;
    public List<string> SourceIds { get; set; } = new();
    public List<string> SearchTerms { get; set; } = new();
    public int? MaxContextItems { get; set; }
}

/// <summary>
/// AI-generated retrieval plan for live RAG.
/// </summary>
public class AiSearchPlanResponse
{
    public bool Success { get; set; }
    public List<string> SearchTerms { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string RawResponse { get; set; } = "";
    public string? Model { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}
