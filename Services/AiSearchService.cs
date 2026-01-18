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

            // Build request body
            var requestBody = new
            {
                model = settings.AiModel,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                },
                max_tokens = 4000,
                temperature = 0.7
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

            // Parse response
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
}
