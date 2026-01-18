//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using MimeKit;
using MSCC.Localization;
using MSCC.Models;

// Alias to resolve ambiguity
using ImapSearchQuery = MailKit.Search.SearchQuery;

namespace MSCC.Connectors;

/// <summary>
/// Authentication types for the IMAP Connector.
/// </summary>
public enum ImapAuthenticationType
{
    Password,
    OAuth2
}

/// <summary>
/// Encryption methods for IMAP connection.
/// </summary>
public enum ImapEncryptionMethod
{
    None,
    SslTls,
    StartTls
}

/// <summary>
/// IMAP connector for searching emails via IMAP protocol.
/// Supports OAuth2 and password authentication.
/// </summary>
public class ImapConnector : IDataSourceConnector
{
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceNormRegex = new(@"\s+", RegexOptions.Compiled);

    public string Id => "imap-connector";
    public string Name => Strings.Instance.ImapConnectorName;
    public string Description => Strings.Instance.ImapConnectorDescription;
    public string Version => "1.0.0";

    private string _server = string.Empty;
    private int _port = 993;
    private string _emailAddress = string.Empty;
    private string _password = string.Empty;
    private ImapAuthenticationType _authType = ImapAuthenticationType.Password;
    private ImapEncryptionMethod _encryption = ImapEncryptionMethod.SslTls;
    private string _oauth2AccessToken = string.Empty;
    private int _maxResults = 50;
    private int _maxDaysBack = 30;
    private string _folderName = "INBOX";

    public IEnumerable<ConnectorParameter> ConfigurationParameters => new[]
    {
        new ConnectorParameter
        {
            Name = "Server",
            DisplayName = Strings.Instance.ImapServer,
            Description = Strings.Instance.ImapServerDesc,
            ParameterType = "string",
            IsRequired = true,
            DefaultValue = "imap.gmail.com"
        },
        new ConnectorParameter
        {
            Name = "Port",
            DisplayName = Strings.Instance.ImapPort,
            Description = Strings.Instance.ImapPortDesc,
            ParameterType = "int",
            IsRequired = true,
            DefaultValue = "993"
        },
        new ConnectorParameter
        {
            Name = "EmailAddress",
            DisplayName = Strings.Instance.ImapEmailAddress,
            Description = Strings.Instance.ImapEmailAddressDesc,
            ParameterType = "string",
            IsRequired = true
        },
        new ConnectorParameter
        {
            Name = "AuthType",
            DisplayName = Strings.Instance.ImapAuthType,
            Description = Strings.Instance.ImapAuthTypeDesc,
            ParameterType = "string",
            IsRequired = true,
            DefaultValue = "Password"
        },
        new ConnectorParameter
        {
            Name = "Password",
            DisplayName = Strings.Instance.ImapPassword,
            Description = Strings.Instance.ImapPasswordDesc,
            ParameterType = "string",
            IsRequired = false
        },
        new ConnectorParameter
        {
            Name = "OAuth2AccessToken",
            DisplayName = Strings.Instance.ImapOAuth2Token,
            Description = Strings.Instance.ImapOAuth2TokenDesc,
            ParameterType = "string",
            IsRequired = false
        },
        new ConnectorParameter
        {
            Name = "Encryption",
            DisplayName = Strings.Instance.ImapEncryption,
            Description = Strings.Instance.ImapEncryptionDesc,
            ParameterType = "string",
            IsRequired = true,
            DefaultValue = "SslTls"
        },
        new ConnectorParameter
        {
            Name = "FolderName",
            DisplayName = Strings.Instance.ImapFolderName,
            Description = Strings.Instance.ImapFolderNameDesc,
            ParameterType = "string",
            IsRequired = false,
            DefaultValue = "INBOX"
        },
        new ConnectorParameter
        {
            Name = "MaxResults",
            DisplayName = Strings.Instance.ImapMaxResults,
            Description = Strings.Instance.ImapMaxResultsDesc,
            ParameterType = "int",
            IsRequired = false,
            DefaultValue = "50"
        },
        new ConnectorParameter
        {
            Name = "MaxDaysBack",
            DisplayName = Strings.Instance.ImapMaxDaysBack,
            Description = Strings.Instance.ImapMaxDaysBackDesc,
            ParameterType = "int",
            IsRequired = false,
            DefaultValue = "30"
        }
    };

    public Task<bool> InitializeAsync(Dictionary<string, string> configuration)
    {
        if (!configuration.TryGetValue("Server", out var server) || string.IsNullOrWhiteSpace(server))
            return Task.FromResult(false);

        if (!configuration.TryGetValue("EmailAddress", out var email) || string.IsNullOrWhiteSpace(email))
            return Task.FromResult(false);

        _server = server.Trim();
        _emailAddress = email.Trim();

        if (configuration.TryGetValue("Port", out var portStr) && int.TryParse(portStr, out var port))
            _port = port;

        if (configuration.TryGetValue("AuthType", out var authType))
            _authType = ParseAuthType(authType);

        if (configuration.TryGetValue("Password", out var password))
            _password = password;

        if (configuration.TryGetValue("OAuth2AccessToken", out var token))
            _oauth2AccessToken = token;

        if (configuration.TryGetValue("Encryption", out var encryption))
            _encryption = ParseEncryption(encryption);

        if (configuration.TryGetValue("FolderName", out var folder) && !string.IsNullOrWhiteSpace(folder))
            _folderName = folder.Trim();

        if (configuration.TryGetValue("MaxResults", out var maxResultsStr) && int.TryParse(maxResultsStr, out var maxResults))
            _maxResults = Math.Max(1, Math.Min(maxResults, 500));

        if (configuration.TryGetValue("MaxDaysBack", out var maxDaysStr) && int.TryParse(maxDaysStr, out var maxDays))
            _maxDaysBack = Math.Max(1, Math.Min(maxDays, 365));

        // Validate authentication
        if (_authType == ImapAuthenticationType.Password && string.IsNullOrWhiteSpace(_password))
            return Task.FromResult(false);

        if (_authType == ImapAuthenticationType.OAuth2 && string.IsNullOrWhiteSpace(_oauth2AccessToken))
            return Task.FromResult(false);

        return Task.FromResult(true);
    }

    private static ImapAuthenticationType ParseAuthType(string authType)
    {
        return authType.Trim().ToLowerInvariant() switch
        {
            "oauth" or "oauth2" => ImapAuthenticationType.OAuth2,
            _ => ImapAuthenticationType.Password
        };
    }

    private static ImapEncryptionMethod ParseEncryption(string encryption)
    {
        return encryption.Trim().ToLowerInvariant() switch
        {
            "none" => ImapEncryptionMethod.None,
            "starttls" or "tls" => ImapEncryptionMethod.StartTls,
            _ => ImapEncryptionMethod.SslTls
        };
    }

    private SecureSocketOptions GetSecureSocketOptions()
    {
        return _encryption switch
        {
            ImapEncryptionMethod.None => SecureSocketOptions.None,
            ImapEncryptionMethod.StartTls => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.SslOnConnect
        };
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var client = new ImapClient();
            await client.ConnectAsync(_server, _port, GetSecureSocketOptions());

            if (_authType == ImapAuthenticationType.OAuth2)
            {
                var oauth2 = new SaslMechanismOAuth2(_emailAddress, _oauth2AccessToken);
                await client.AuthenticateAsync(oauth2);
            }
            else
            {
                await client.AuthenticateAsync(_emailAddress, _password);
            }

            await client.DisconnectAsync(true);
            return true;
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
            using var client = new ImapClient();
            await client.ConnectAsync(_server, _port, GetSecureSocketOptions(), cancellationToken);

            if (_authType == ImapAuthenticationType.OAuth2)
            {
                var oauth2 = new SaslMechanismOAuth2(_emailAddress, _oauth2AccessToken);
                await client.AuthenticateAsync(oauth2, cancellationToken);
            }
            else
            {
                await client.AuthenticateAsync(_emailAddress, _password, cancellationToken);
            }

            var folder = await client.GetFolderAsync(_folderName, cancellationToken);
            await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

            // Build search query
            var sinceDate = DateTime.Now.AddDays(-_maxDaysBack);
            MailKit.Search.SearchQuery query = ImapSearchQuery.DeliveredAfter(sinceDate);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                // MailKit Or() only takes 2 arguments, so we need to nest them
                var textQuery = ImapSearchQuery.Or(
                    ImapSearchQuery.SubjectContains(searchTerm),
                    ImapSearchQuery.Or(
                        ImapSearchQuery.BodyContains(searchTerm),
                        ImapSearchQuery.FromContains(searchTerm)
                    )
                );
                query = query.And(textQuery);
            }

            var uids = await folder.SearchAsync(query, cancellationToken);
            var limitedUids = uids.Reverse().Take(Math.Min(_maxResults, maxResults)).ToList();

            foreach (var uid in limitedUids)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var message = await folder.GetMessageAsync(uid, cancellationToken);

                var fromAddress = message.From.Mailboxes.FirstOrDefault()?.Address ?? "";
                var fromName = message.From.Mailboxes.FirstOrDefault()?.Name ?? fromAddress;
                var subject = message.Subject ?? Strings.Instance.ImapNoSubject;
                var date = message.Date.LocalDateTime;
                var bodyPreview = GetBodyPreview(message, 200);

                var hasAttachments = message.Attachments.Any();

                results.Add(new SearchResult
                {
                    Title = subject,
                    Description = $"{fromName}: {bodyPreview}",
                    SourceName = Name,
                    ConnectorId = Id,
                    OriginalReference = $"imap://{_server}/{_folderName}/{uid}",
                    RelevanceScore = CalculateRelevance(message, searchTerm),
                    Metadata = new Dictionary<string, object>
                    {
                        [Strings.Instance.ImapMetaFrom] = fromName,
                        [Strings.Instance.ImapMetaFromEmail] = fromAddress,
                        [Strings.Instance.ImapMetaSubject] = subject,
                        [Strings.Instance.ImapMetaDate] = date.ToString("yyyy-MM-dd HH:mm"),
                        [Strings.Instance.ImapMetaHasAttachments] = hasAttachments ? Strings.Instance.Yes : Strings.Instance.No,
                        [Strings.Instance.ImapMetaFolder] = _folderName,
                        ["MessageUid"] = uid.Id,
                        ["FullBody"] = GetBodyPreview(message, 10000)
                    }
                });
            }

            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            results.Add(new SearchResult
            {
                Title = Strings.Instance.ImapError,
                Description = $"{Strings.Instance.ImapErrorDesc}: {ex.Message}",
                SourceName = Name,
                ConnectorId = Id,
                OriginalReference = _server,
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

    private static string GetBodyPreview(MimeMessage message, int maxLength)
    {
        var body = message.TextBody ?? message.HtmlBody ?? "";

        if (!string.IsNullOrEmpty(message.HtmlBody) && string.IsNullOrEmpty(message.TextBody))
        {
            // Strip HTML tags
            body = HtmlTagRegex.Replace(body, " ");
            body = System.Net.WebUtility.HtmlDecode(body);
        }

        // Normalize whitespace
        body = WhitespaceNormRegex.Replace(body, " ").Trim();

        if (body.Length > maxLength)
        {
            body = body[..maxLength] + "...";
        }

        return body;
    }

    private static int CalculateRelevance(MimeMessage message, string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return 50;

        var score = 50;
        var term = searchTerm.ToLowerInvariant();

        if (message.Subject?.ToLowerInvariant().Contains(term) == true)
            score += 30;

        if (message.From.Mailboxes.Any(m => 
            m.Address?.ToLowerInvariant().Contains(term) == true ||
            m.Name?.ToLowerInvariant().Contains(term) == true))
            score += 20;

        // Recent messages get higher score
        var daysOld = (DateTime.Now - message.Date.LocalDateTime).TotalDays;
        if (daysOld < 1)
            score += 10;
        else if (daysOld < 7)
            score += 5;

        return Math.Min(score, 100);
    }

    public DetailViewConfiguration GetDetailViewConfiguration(SearchResult result)
    {
        return new DetailViewConfiguration
        {
            ViewType = DetailViewType.Default,
            DisplayProperties = new List<string>
            {
                Strings.Instance.ImapMetaFrom,
                Strings.Instance.ImapMetaFromEmail,
                Strings.Instance.ImapMetaSubject,
                Strings.Instance.ImapMetaDate,
                Strings.Instance.ImapMetaHasAttachments,
                Strings.Instance.ImapMetaFolder
            },
            Actions = new List<ResultAction>
            {
                new ResultAction
                {
                    Id = "copy-body",
                    Name = Strings.Instance.ImapActionCopyBody,
                    Icon = "\ud83d\udccb",
                    Description = Strings.Instance.ImapActionCopyBodyDesc
                },
                new ResultAction
                {
                    Id = "copy-sender",
                    Name = Strings.Instance.ImapActionCopySender,
                    Icon = "\ud83d\udce7",
                    Description = Strings.Instance.ImapActionCopySenderDesc
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
                case "copy-body":
                    if (result.Metadata.TryGetValue("FullBody", out var body))
                    {
                        Clipboard.SetText(body?.ToString() ?? result.Description);
                    }
                    else
                    {
                        Clipboard.SetText(result.Description);
                    }
                    return Task.FromResult(true);

                case "copy-sender":
                    if (result.Metadata.TryGetValue(Strings.Instance.ImapMetaFromEmail, out var email))
                    {
                        Clipboard.SetText(email?.ToString() ?? "");
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
        GC.SuppressFinalize(this);
    }
}
