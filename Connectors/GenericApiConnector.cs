//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows;
using MSCC.Localization;
using MSCC.Models;

namespace MSCC.Connectors;

/// <summary>
/// Authentication types for the Generic API Connector.
/// </summary>
public enum ApiAuthenticationType
{
    None,
    HeaderBased,
    BearerToken,
    OAuth2,
    JWT,
    QueryParameter,
    PostParameter
}

/// <summary>
/// Generic API connector supporting multiple authentication methods and flexible search parameter placement.
/// </summary>
public class GenericApiConnector : IDataSourceConnector
{
    public string Id => "generic-api-connector";
    public string Name => Strings.Instance.GenericApiConnectorName;
    public string Description => Strings.Instance.GenericApiConnectorDescription;
    public string Version => "1.0.0";

    private const string SearchPlaceholder = "[SEARCH]";

    private string _apiEndpoint = string.Empty;
    private string _httpMethod = "GET";
    private ApiAuthenticationType _authType = ApiAuthenticationType.None;
    private string _authHeaderName = "Authorization";
    private string _authHeaderValue = string.Empty;
    private string _authToken = string.Empty;
    private string _oauth2TokenEndpoint = string.Empty;
    private string _oauth2ClientId = string.Empty;
    private string _oauth2ClientSecret = string.Empty;
    private string _oauth2Scope = string.Empty;
    private string _queryParameters = string.Empty;
    private string _postBody = string.Empty;
    private string _contentType = "application/json";
    private string _resultJsonPath = string.Empty;
    private string _resultTitleProperty = "title";
    private string _resultDescriptionProperty = "description";
    private string _resultUrlProperty = "url";
    private string _customHeaders = string.Empty;
    private int _timeoutSeconds = 30;

    private readonly HttpClient _httpClient;
    private string? _cachedOAuthToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public GenericApiConnector()
    {
        _httpClient = new HttpClient();
    }

    public IEnumerable<ConnectorParameter> ConfigurationParameters => new[]
    {
        new ConnectorParameter
        {
            Name = "ApiEndpoint",
            DisplayName = Strings.Instance.GenericApiEndpoint,
            Description = Strings.Instance.GenericApiEndpointDesc,
            ParameterType = "string",
            IsRequired = true,
            DefaultValue = "https://api.example.com/search"
        },
        new ConnectorParameter
        {
            Name = "HttpMethod",
            DisplayName = Strings.Instance.GenericApiHttpMethod,
            Description = Strings.Instance.GenericApiHttpMethodDesc,
            ParameterType = "string",
            IsRequired = true,
            DefaultValue = "GET"
        },
        new ConnectorParameter
        {
            Name = "AuthType",
            DisplayName = Strings.Instance.GenericApiAuthType,
            Description = Strings.Instance.GenericApiAuthTypeDesc,
            ParameterType = "string",
            IsRequired = true,
            DefaultValue = "None"
        },
        new ConnectorParameter
        {
            Name = "AuthHeaderName",
            DisplayName = Strings.Instance.GenericApiAuthHeaderName,
            Description = Strings.Instance.GenericApiAuthHeaderNameDesc,
            ParameterType = "string",
            IsRequired = false,
            DefaultValue = "Authorization"
        },
        new ConnectorParameter
        {
            Name = "AuthHeaderValue",
            DisplayName = Strings.Instance.GenericApiAuthHeaderValue,
            Description = Strings.Instance.GenericApiAuthHeaderValueDesc,
            ParameterType = "string",
            IsRequired = false
        },
        new ConnectorParameter
        {
            Name = "AuthToken",
            DisplayName = Strings.Instance.GenericApiAuthToken,
            Description = Strings.Instance.GenericApiAuthTokenDesc,
            ParameterType = "string",
            IsRequired = false
        },
        new ConnectorParameter
        {
            Name = "OAuth2TokenEndpoint",
            DisplayName = Strings.Instance.GenericApiOAuth2TokenEndpoint,
            Description = Strings.Instance.GenericApiOAuth2TokenEndpointDesc,
            ParameterType = "string",
            IsRequired = false
        },
        new ConnectorParameter
        {
            Name = "OAuth2ClientId",
            DisplayName = Strings.Instance.GenericApiOAuth2ClientId,
            Description = Strings.Instance.GenericApiOAuth2ClientIdDesc,
            ParameterType = "string",
            IsRequired = false
        },
        new ConnectorParameter
        {
            Name = "OAuth2ClientSecret",
            DisplayName = Strings.Instance.GenericApiOAuth2ClientSecret,
            Description = Strings.Instance.GenericApiOAuth2ClientSecretDesc,
            ParameterType = "string",
            IsRequired = false
        },
        new ConnectorParameter
        {
            Name = "OAuth2Scope",
            DisplayName = Strings.Instance.GenericApiOAuth2Scope,
            Description = Strings.Instance.GenericApiOAuth2ScopeDesc,
            ParameterType = "string",
            IsRequired = false
        },
        new ConnectorParameter
        {
            Name = "QueryParameters",
            DisplayName = Strings.Instance.GenericApiQueryParameters,
            Description = Strings.Instance.GenericApiQueryParametersDesc,
            ParameterType = "string",
            IsRequired = false,
            DefaultValue = "q=[SEARCH]"
        },
        new ConnectorParameter
        {
            Name = "PostBody",
            DisplayName = Strings.Instance.GenericApiPostBody,
            Description = Strings.Instance.GenericApiPostBodyDesc,
            ParameterType = "string",
            IsRequired = false
        },
        new ConnectorParameter
        {
            Name = "ContentType",
            DisplayName = Strings.Instance.GenericApiContentType,
            Description = Strings.Instance.GenericApiContentTypeDesc,
            ParameterType = "string",
            IsRequired = false,
            DefaultValue = "application/json"
        },
        new ConnectorParameter
        {
            Name = "CustomHeaders",
            DisplayName = Strings.Instance.GenericApiCustomHeaders,
            Description = Strings.Instance.GenericApiCustomHeadersDesc,
            ParameterType = "string",
            IsRequired = false
        },
        new ConnectorParameter
        {
            Name = "ResultJsonPath",
            DisplayName = Strings.Instance.GenericApiResultJsonPath,
            Description = Strings.Instance.GenericApiResultJsonPathDesc,
            ParameterType = "string",
            IsRequired = false,
            DefaultValue = "results"
        },
        new ConnectorParameter
        {
            Name = "ResultTitleProperty",
            DisplayName = Strings.Instance.GenericApiResultTitleProperty,
            Description = Strings.Instance.GenericApiResultTitlePropertyDesc,
            ParameterType = "string",
            IsRequired = false,
            DefaultValue = "title"
        },
        new ConnectorParameter
        {
            Name = "ResultDescriptionProperty",
            DisplayName = Strings.Instance.GenericApiResultDescriptionProperty,
            Description = Strings.Instance.GenericApiResultDescriptionPropertyDesc,
            ParameterType = "string",
            IsRequired = false,
            DefaultValue = "description"
        },
        new ConnectorParameter
        {
            Name = "ResultUrlProperty",
            DisplayName = Strings.Instance.GenericApiResultUrlProperty,
            Description = Strings.Instance.GenericApiResultUrlPropertyDesc,
            ParameterType = "string",
            IsRequired = false,
            DefaultValue = "url"
        },
        new ConnectorParameter
        {
            Name = "TimeoutSeconds",
            DisplayName = Strings.Instance.GenericApiTimeout,
            Description = Strings.Instance.GenericApiTimeoutDesc,
            ParameterType = "int",
            IsRequired = false,
            DefaultValue = "30"
        }
    };

    public Task<bool> InitializeAsync(Dictionary<string, string> configuration)
    {
        if (!configuration.TryGetValue("ApiEndpoint", out var endpoint) || string.IsNullOrWhiteSpace(endpoint))
            return Task.FromResult(false);

        _apiEndpoint = endpoint.Trim();

        if (configuration.TryGetValue("HttpMethod", out var method))
            _httpMethod = method.Trim().ToUpperInvariant();

        if (configuration.TryGetValue("AuthType", out var authType))
            _authType = ParseAuthType(authType);

        if (configuration.TryGetValue("AuthHeaderName", out var authHeaderName))
            _authHeaderName = authHeaderName.Trim();

        if (configuration.TryGetValue("AuthHeaderValue", out var authHeaderValue))
            _authHeaderValue = authHeaderValue;

        if (configuration.TryGetValue("AuthToken", out var authToken))
            _authToken = authToken;

        if (configuration.TryGetValue("OAuth2TokenEndpoint", out var tokenEndpoint))
            _oauth2TokenEndpoint = tokenEndpoint.Trim();

        if (configuration.TryGetValue("OAuth2ClientId", out var clientId))
            _oauth2ClientId = clientId;

        if (configuration.TryGetValue("OAuth2ClientSecret", out var clientSecret))
            _oauth2ClientSecret = clientSecret;

        if (configuration.TryGetValue("OAuth2Scope", out var scope))
            _oauth2Scope = scope;

        if (configuration.TryGetValue("QueryParameters", out var queryParams))
            _queryParameters = queryParams;

        if (configuration.TryGetValue("PostBody", out var postBody))
            _postBody = postBody;

        if (configuration.TryGetValue("ContentType", out var contentType))
            _contentType = contentType.Trim();

        if (configuration.TryGetValue("CustomHeaders", out var customHeaders))
            _customHeaders = customHeaders;

        if (configuration.TryGetValue("ResultJsonPath", out var jsonPath))
            _resultJsonPath = jsonPath.Trim();

        if (configuration.TryGetValue("ResultTitleProperty", out var titleProp))
            _resultTitleProperty = titleProp.Trim();

        if (configuration.TryGetValue("ResultDescriptionProperty", out var descProp))
            _resultDescriptionProperty = descProp.Trim();

        if (configuration.TryGetValue("ResultUrlProperty", out var urlProp))
            _resultUrlProperty = urlProp.Trim();

        if (configuration.TryGetValue("TimeoutSeconds", out var timeoutStr) && int.TryParse(timeoutStr, out var timeout))
            _timeoutSeconds = Math.Max(1, Math.Min(timeout, 300));

        _httpClient.Timeout = TimeSpan.FromSeconds(_timeoutSeconds);

        return Task.FromResult(true);
    }

    private static ApiAuthenticationType ParseAuthType(string authType)
    {
        return authType.Trim().ToLowerInvariant() switch
        {
            "none" => ApiAuthenticationType.None,
            "header" or "headerbased" => ApiAuthenticationType.HeaderBased,
            "bearer" or "bearertoken" => ApiAuthenticationType.BearerToken,
            "oauth" or "oauth2" => ApiAuthenticationType.OAuth2,
            "jwt" => ApiAuthenticationType.JWT,
            "query" or "queryparameter" => ApiAuthenticationType.QueryParameter,
            "post" or "postparameter" => ApiAuthenticationType.PostParameter,
            _ => ApiAuthenticationType.None
        };
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var url = BuildUrl("test");
            using var request = new HttpRequestMessage(new HttpMethod(_httpMethod), url);
            await ApplyAuthenticationAsync(request, "test");
            ApplyCustomHeaders(request);

            if (_httpMethod is "POST" or "PUT" or "PATCH")
            {
                var body = ReplaceSearchPlaceholder(_postBody, "test", false);
                request.Content = new StringContent(body, Encoding.UTF8, _contentType);
            }

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
            var url = BuildUrl(searchTerm);
            using var request = new HttpRequestMessage(new HttpMethod(_httpMethod), url);
            await ApplyAuthenticationAsync(request, searchTerm);
            ApplyCustomHeaders(request);

            if (_httpMethod is "POST" or "PUT" or "PATCH" && !string.IsNullOrWhiteSpace(_postBody))
            {
                var body = ReplaceSearchPlaceholder(_postBody, searchTerm, false);
                request.Content = new StringContent(body, Encoding.UTF8, _contentType);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                results.Add(new SearchResult
                {
                    Title = Strings.Instance.GenericApiError,
                    Description = $"{response.StatusCode}: {responseContent}",
                    SourceName = Name,
                    ConnectorId = Id,
                    OriginalReference = url,
                    RelevanceScore = 0,
                    Metadata = new Dictionary<string, object>
                    {
                        ["StatusCode"] = (int)response.StatusCode,
                        ["Error"] = responseContent
                    }
                });
                return results;
            }

            results.AddRange(ParseResults(responseContent, searchTerm, maxResults));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            results.Add(new SearchResult
            {
                Title = Strings.Instance.GenericApiError,
                Description = ex.Message,
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

    private string BuildUrl(string searchTerm)
    {
        var url = ReplaceSearchPlaceholder(_apiEndpoint, searchTerm, true);

        if (!string.IsNullOrWhiteSpace(_queryParameters))
        {
            var queryParams = ReplaceSearchPlaceholder(_queryParameters, searchTerm, true);
            var separator = url.Contains('?') ? "&" : "?";
            url = $"{url}{separator}{queryParams}";
        }

        // Add token to query parameters if using QueryParameter auth
        if (_authType == ApiAuthenticationType.QueryParameter && !string.IsNullOrWhiteSpace(_authToken))
        {
            var tokenParam = ReplaceSearchPlaceholder(_authToken, searchTerm, true);
            var separator = url.Contains('?') ? "&" : "?";
            url = $"{url}{separator}{tokenParam}";
        }

        return url;
    }

    private string ReplaceSearchPlaceholder(string input, string searchTerm, bool urlEncode)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var replacement = urlEncode ? HttpUtility.UrlEncode(searchTerm) : searchTerm;
        return input.Replace(SearchPlaceholder, replacement, StringComparison.OrdinalIgnoreCase);
    }

    private async Task ApplyAuthenticationAsync(HttpRequestMessage request, string searchTerm)
    {
        switch (_authType)
        {
            case ApiAuthenticationType.HeaderBased:
                if (!string.IsNullOrWhiteSpace(_authHeaderName) && !string.IsNullOrWhiteSpace(_authHeaderValue))
                {
                    request.Headers.TryAddWithoutValidation(_authHeaderName, _authHeaderValue);
                }
                break;

            case ApiAuthenticationType.BearerToken:
                if (!string.IsNullOrWhiteSpace(_authToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
                }
                break;

            case ApiAuthenticationType.JWT:
                if (!string.IsNullOrWhiteSpace(_authToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
                }
                break;

            case ApiAuthenticationType.OAuth2:
                var token = await GetOAuth2TokenAsync();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
                break;

            case ApiAuthenticationType.PostParameter:
                // Token is added to POST body in the search method
                break;

            case ApiAuthenticationType.QueryParameter:
                // Token is added to URL in BuildUrl method
                break;

            case ApiAuthenticationType.None:
            default:
                break;
        }
    }

    private async Task<string?> GetOAuth2TokenAsync()
    {
        if (_cachedOAuthToken != null && DateTime.UtcNow < _tokenExpiry)
        {
            return _cachedOAuthToken;
        }

        if (string.IsNullOrWhiteSpace(_oauth2TokenEndpoint) ||
            string.IsNullOrWhiteSpace(_oauth2ClientId) ||
            string.IsNullOrWhiteSpace(_oauth2ClientSecret))
        {
            return null;
        }

        try
        {
            var formData = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _oauth2ClientId,
                ["client_secret"] = _oauth2ClientSecret
            };

            if (!string.IsNullOrWhiteSpace(_oauth2Scope))
            {
                formData["scope"] = _oauth2Scope;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, _oauth2TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(formData)
            };

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("access_token", out var tokenEl))
            {
                _cachedOAuthToken = tokenEl.GetString();

                if (root.TryGetProperty("expires_in", out var expiresEl) && expiresEl.TryGetInt32(out var expiresIn))
                {
                    _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);
                }
                else
                {
                    _tokenExpiry = DateTime.UtcNow.AddMinutes(55);
                }

                return _cachedOAuthToken;
            }
        }
        catch
        {
            // Token fetch failed
        }

        return null;
    }

    private void ApplyCustomHeaders(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(_customHeaders))
            return;

        var lines = _customHeaders.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex > 0)
            {
                var headerName = line[..colonIndex].Trim();
                var headerValue = line[(colonIndex + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(headerName))
                {
                    request.Headers.TryAddWithoutValidation(headerName, headerValue);
                }
            }
        }
    }

    private IEnumerable<SearchResult> ParseResults(string responseContent, string searchTerm, int maxResults)
    {
        var results = new List<SearchResult>();

        try
        {
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            JsonElement resultsArray;

            if (!string.IsNullOrWhiteSpace(_resultJsonPath))
            {
                resultsArray = NavigateJsonPath(root, _resultJsonPath);
            }
            else
            {
                resultsArray = root;
            }

            if (resultsArray.ValueKind == JsonValueKind.Array)
            {
                var count = 0;
                foreach (var item in resultsArray.EnumerateArray())
                {
                    if (count >= maxResults)
                        break;

                    var title = GetJsonPropertyValue(item, _resultTitleProperty) ?? $"Result {count + 1}";
                    var description = GetJsonPropertyValue(item, _resultDescriptionProperty) ?? string.Empty;
                    var url = GetJsonPropertyValue(item, _resultUrlProperty) ?? string.Empty;

                    var metadata = new Dictionary<string, object>();
                    foreach (var prop in item.EnumerateObject())
                    {
                        metadata[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                            JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => prop.Value.ToString()
                        };
                    }

                    results.Add(new SearchResult
                    {
                        Title = title,
                        Description = description,
                        SourceName = Name,
                        ConnectorId = Id,
                        OriginalReference = url,
                        RelevanceScore = 100 - count,
                        Metadata = metadata
                    });

                    count++;
                }
            }
            else if (resultsArray.ValueKind == JsonValueKind.Object)
            {
                // Single result object
                var title = GetJsonPropertyValue(resultsArray, _resultTitleProperty) ?? "Result";
                var description = GetJsonPropertyValue(resultsArray, _resultDescriptionProperty) ?? responseContent;
                var url = GetJsonPropertyValue(resultsArray, _resultUrlProperty) ?? string.Empty;

                var metadata = new Dictionary<string, object>();
                foreach (var prop in resultsArray.EnumerateObject())
                {
                    metadata[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                        JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => prop.Value.ToString()
                    };
                }

                results.Add(new SearchResult
                {
                    Title = title,
                    Description = description,
                    SourceName = Name,
                    ConnectorId = Id,
                    OriginalReference = url,
                    RelevanceScore = 100,
                    Metadata = metadata
                });
            }
        }
        catch
        {
            // Return raw response as single result
            results.Add(new SearchResult
            {
                Title = Strings.Instance.GenericApiRawResponse,
                Description = responseContent.Length > 500 ? responseContent[..500] + "..." : responseContent,
                SourceName = Name,
                ConnectorId = Id,
                OriginalReference = _apiEndpoint,
                RelevanceScore = 100,
                Metadata = new Dictionary<string, object>
                {
                    ["RawResponse"] = responseContent,
                    [Strings.Instance.GenericApiMetaQuery] = searchTerm
                }
            });
        }

        return results;
    }

    private static JsonElement NavigateJsonPath(JsonElement element, string path)
    {
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var current = element;

        foreach (var part in parts)
        {
            // Handle array indexing like "results[0]"
            var match = Regex.Match(part, @"^(\w+)\[(\d+)\]$");
            if (match.Success)
            {
                var propName = match.Groups[1].Value;
                var index = int.Parse(match.Groups[2].Value);

                if (current.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.Array)
                {
                    var arr = prop.EnumerateArray().ToArray();
                    if (index < arr.Length)
                    {
                        current = arr[index];
                        continue;
                    }
                }
                return default;
            }

            if (current.TryGetProperty(part, out var next))
            {
                current = next;
            }
            else
            {
                return default;
            }
        }

        return current;
    }

    private static string? GetJsonPropertyValue(JsonElement element, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            return null;

        // Support nested properties like "data.title"
        var parts = propertyName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var current = element;

        foreach (var part in parts)
        {
            if (!current.TryGetProperty(part, out current))
                return null;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => current.ToString()
        };
    }

    public DetailViewConfiguration GetDetailViewConfiguration(SearchResult result)
    {
        var displayProps = result.Metadata.Keys.Take(10).ToList();

        return new DetailViewConfiguration
        {
            ViewType = DetailViewType.Default,
            DisplayProperties = displayProps,
            Actions = new List<ResultAction>
            {
                new ResultAction
                {
                    Id = "open-url",
                    Name = Strings.Instance.GenericApiActionOpenUrl,
                    Icon = "\ud83c\udf10",
                    Description = Strings.Instance.GenericApiActionOpenUrlDesc
                },
                new ResultAction
                {
                    Id = "copy-json",
                    Name = Strings.Instance.GenericApiActionCopyJson,
                    Icon = "\ud83d\udccb",
                    Description = Strings.Instance.GenericApiActionCopyJsonDesc
                },
                new ResultAction
                {
                    Id = "copy-url",
                    Name = Strings.Instance.GenericApiActionCopyUrl,
                    Icon = "\ud83d\udd17",
                    Description = Strings.Instance.GenericApiActionCopyUrlDesc
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
                case "open-url":
                    if (!string.IsNullOrWhiteSpace(result.OriginalReference))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = result.OriginalReference,
                            UseShellExecute = true
                        });
                    }
                    return Task.FromResult(true);

                case "copy-json":
                    var json = JsonSerializer.Serialize(result.Metadata, new JsonSerializerOptions { WriteIndented = true });
                    Clipboard.SetText(json);
                    return Task.FromResult(true);

                case "copy-url":
                    if (!string.IsNullOrWhiteSpace(result.OriginalReference))
                    {
                        Clipboard.SetText(result.OriginalReference);
                    }
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
