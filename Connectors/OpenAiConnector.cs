//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Windows;
using MSCC.Localization;
using MSCC.Models;

namespace MSCC.Connectors;

/// <summary>
/// Konnektor f\u00fcr OpenAI API-kompatible KI-Dienste.
/// </summary>
public class OpenAiConnector : IDataSourceConnector
{
    public string Id => "openai-connector";
    public string Name => Strings.Instance.OpenAiConnectorName;
    public string Description => Strings.Instance.OpenAiConnectorDescription;
    public string Version => "1.0.0";

    private string _apiEndpoint = string.Empty;
    private string _apiKey = string.Empty;
    private string _model = "gpt-3.5-turbo";
    private string _systemPrompt = string.Empty;
    private int _maxTokens = 1000;
    private double _temperature = 0.7;

    private readonly HttpClient _httpClient;

    public OpenAiConnector()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(120);
    }

    public IEnumerable<ConnectorParameter> ConfigurationParameters => new[]
    {
        new ConnectorParameter
        {
            Name = "ApiEndpoint",
            DisplayName = Strings.Instance.OpenAiApiEndpoint,
            Description = Strings.Instance.OpenAiApiEndpointDesc,
            ParameterType = "string",
            IsRequired = true,
            DefaultValue = "https://api.openai.com/v1/chat/completions"
        },
        new ConnectorParameter
        {
            Name = "ApiKey",
            DisplayName = Strings.Instance.OpenAiApiKey,
            Description = Strings.Instance.OpenAiApiKeyDesc,
            ParameterType = "string",
            IsRequired = true
        },
        new ConnectorParameter
        {
            Name = "Model",
            DisplayName = Strings.Instance.OpenAiModel,
            Description = Strings.Instance.OpenAiModelDesc,
            ParameterType = "string",
            IsRequired = true,
            DefaultValue = "gpt-3.5-turbo"
        },
        new ConnectorParameter
        {
            Name = "SystemPrompt",
            DisplayName = Strings.Instance.OpenAiSystemPrompt,
            Description = Strings.Instance.OpenAiSystemPromptDesc,
            ParameterType = "string",
            IsRequired = false,
            DefaultValue = "You are a helpful assistant. Please provide concise and accurate answers."
        },
        new ConnectorParameter
        {
            Name = "MaxTokens",
            DisplayName = Strings.Instance.OpenAiMaxTokens,
            Description = Strings.Instance.OpenAiMaxTokensDesc,
            ParameterType = "int",
            IsRequired = false,
            DefaultValue = "1000"
        },
        new ConnectorParameter
        {
            Name = "Temperature",
            DisplayName = Strings.Instance.OpenAiTemperature,
            Description = Strings.Instance.OpenAiTemperatureDesc,
            ParameterType = "string",
            IsRequired = false,
            DefaultValue = "0.7"
        }
    };

    public Task<bool> InitializeAsync(Dictionary<string, string> configuration)
    {
        if (!configuration.TryGetValue("ApiEndpoint", out var endpoint) || string.IsNullOrWhiteSpace(endpoint))
            return Task.FromResult(false);

        if (!configuration.TryGetValue("ApiKey", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            return Task.FromResult(false);

        if (!configuration.TryGetValue("Model", out var model) || string.IsNullOrWhiteSpace(model))
            return Task.FromResult(false);

        _apiEndpoint = endpoint.Trim();
        _apiKey = apiKey.Trim();
        _model = model.Trim();

        if (configuration.TryGetValue("SystemPrompt", out var systemPrompt))
            _systemPrompt = systemPrompt;

        if (configuration.TryGetValue("MaxTokens", out var maxTokensStr) && int.TryParse(maxTokensStr, out var maxTokens))
            _maxTokens = Math.Max(1, Math.Min(maxTokens, 128000));

        if (configuration.TryGetValue("Temperature", out var tempStr) && double.TryParse(tempStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var temp))
            _temperature = Math.Max(0, Math.Min(temp, 2.0));

        return Task.FromResult(true);
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _apiEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    model = _model,
                    messages = new[] { new { role = "user", content = "Hi" } },
                    max_tokens = 5
                }),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<SearchResult>> SearchAsync(
        string searchTerm,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SearchResult>();

        try
        {
            var messages = new List<object>();

            if (!string.IsNullOrWhiteSpace(_systemPrompt))
            {
                messages.Add(new { role = "system", content = _systemPrompt });
            }

            messages.Add(new { role = "user", content = searchTerm });

            var requestBody = new
            {
                model = _model,
                messages = messages,
                max_tokens = _maxTokens,
                temperature = _temperature
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, _apiEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                results.Add(new SearchResult
                {
                    Title = Strings.Instance.OpenAiError,
                    Description = $"{Strings.Instance.OpenAiErrorDesc}: {response.StatusCode} - {responseContent}",
                    SourceName = Name,
                    ConnectorId = Id,
                    OriginalReference = _apiEndpoint,
                    RelevanceScore = 0,
                    Metadata = new Dictionary<string, object>
                    {
                        ["StatusCode"] = (int)response.StatusCode,
                        ["Error"] = responseContent
                    }
                });
                return results;
            }

            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            var aiResponse = string.Empty;
            var model = _model;
            var promptTokens = 0;
            var completionTokens = 0;
            var totalTokens = 0;
            var finishReason = string.Empty;

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                
                if (firstChoice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var content))
                {
                    aiResponse = content.GetString() ?? string.Empty;
                }

                if (firstChoice.TryGetProperty("finish_reason", out var finishReasonEl))
                {
                    finishReason = finishReasonEl.GetString() ?? string.Empty;
                }
            }

            if (root.TryGetProperty("model", out var modelEl))
            {
                model = modelEl.GetString() ?? _model;
            }

            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var pt))
                    promptTokens = pt.GetInt32();
                if (usage.TryGetProperty("completion_tokens", out var ct))
                    completionTokens = ct.GetInt32();
                if (usage.TryGetProperty("total_tokens", out var tt))
                    totalTokens = tt.GetInt32();
            }

            var title = aiResponse.Length > 100 
                ? aiResponse.Substring(0, 100) + "..." 
                : aiResponse;
            
            // Zeilenumbr\u00fcche im Titel entfernen
            title = title.Replace("\n", " ").Replace("\r", "").Trim();

            results.Add(new SearchResult
            {
                Title = string.IsNullOrEmpty(title) ? Strings.Instance.OpenAiResponse : title,
                Description = aiResponse,
                SourceName = Name,
                ConnectorId = Id,
                OriginalReference = searchTerm,
                RelevanceScore = 100,
                Metadata = new Dictionary<string, object>
                {
                    [Strings.Instance.OpenAiMetaModel] = model,
                    [Strings.Instance.OpenAiMetaPromptTokens] = promptTokens,
                    [Strings.Instance.OpenAiMetaCompletionTokens] = completionTokens,
                    [Strings.Instance.OpenAiMetaTotalTokens] = totalTokens,
                    [Strings.Instance.OpenAiMetaFinishReason] = finishReason,
                    [Strings.Instance.OpenAiMetaQuery] = searchTerm,
                    ["FullResponse"] = aiResponse
                }
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            results.Add(new SearchResult
            {
                Title = Strings.Instance.OpenAiError,
                Description = $"{Strings.Instance.OpenAiErrorDesc}: {ex.Message}",
                SourceName = Name,
                ConnectorId = Id,
                OriginalReference = _apiEndpoint,
                RelevanceScore = 0,
                Metadata = new Dictionary<string, object>
                {
                    ["Exception"] = ex.GetType().Name,
                    ["Error"] = ex.Message
                }
            });
        }

        return results;
    }

    public DetailViewConfiguration GetDetailViewConfiguration(SearchResult result)
    {
        return new DetailViewConfiguration
        {
            ViewType = DetailViewType.Default,
            DisplayProperties = new List<string>
            {
                Strings.Instance.OpenAiMetaModel,
                Strings.Instance.OpenAiMetaPromptTokens,
                Strings.Instance.OpenAiMetaCompletionTokens,
                Strings.Instance.OpenAiMetaTotalTokens,
                Strings.Instance.OpenAiMetaFinishReason,
                Strings.Instance.OpenAiMetaQuery
            },
            Actions = new List<ResultAction>
            {
                new ResultAction
                {
                    Id = "copy-response",
                    Name = Strings.Instance.OpenAiActionCopyResponse,
                    Icon = "\ud83d\udccb",
                    Description = Strings.Instance.OpenAiActionCopyResponseDesc
                },
                new ResultAction
                {
                    Id = "copy-query",
                    Name = Strings.Instance.OpenAiActionCopyQuery,
                    Icon = "\ud83d\udcdd",
                    Description = Strings.Instance.OpenAiActionCopyQueryDesc
                }
            }
        };
    }

    public FrameworkElement? CreateCustomDetailView(SearchResult result)
    {
        return null;
    }

    public Task<bool> ExecuteActionAsync(SearchResult result, string actionId)
    {
        try
        {
            switch (actionId)
            {
                case "copy-response":
                    if (result.Metadata.TryGetValue("FullResponse", out var response))
                    {
                        Clipboard.SetText(response?.ToString() ?? result.Description);
                    }
                    else
                    {
                        Clipboard.SetText(result.Description);
                    }
                    return Task.FromResult(true);

                case "copy-query":
                    Clipboard.SetText(result.OriginalReference);
                    return Task.FromResult(true);

                default:
                    return Task.FromResult(false);
            }
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
