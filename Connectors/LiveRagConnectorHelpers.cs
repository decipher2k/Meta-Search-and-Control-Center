//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Text;
using System.Text.RegularExpressions;
using MSCC.Models;

namespace MSCC.Connectors;

internal static class LiveRagConnectorHelpers
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex TokenRegex = new(@"[\p{L}\p{N}_][\p{L}\p{N}_\-.]{1,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex QuotedPhraseRegex = new("[\"'`](?<phrase>[^\"'`]{2,120})[\"'`]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly char[] TrimChars = [' ', '\t', '\r', '\n', '.', ',', ';', ':', '!', '?', '"', '\'', '`', '(', ')', '[', ']', '{', '}'];
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "can", "could", "do", "does", "for", "from",
        "give", "has", "have", "how", "i", "in", "into", "is", "it", "list", "me", "of", "on", "or",
        "please", "show", "that", "the", "their", "there", "this", "to", "top", "was", "were", "what",
        "when", "where", "which", "who", "with", "would",
        "alle", "als", "am", "an", "auf", "aus", "bei", "bitte", "das", "dem", "den", "der", "des",
        "die", "du", "durch", "ein", "eine", "einem", "einen", "einer", "eines", "er", "es", "fuer",
        "gib", "gibt", "haben", "hat", "ich", "im", "in", "ist", "kann", "kannst", "liste", "mach",
        "mir", "mit", "nach", "oder", "sind", "und", "von", "was", "welche", "welcher", "welches",
        "wie", "wir", "zu", "zum", "zur"
    };

    public static IEnumerable<string> GetSearchTerms(LiveRagQueryRequest request)
    {
        return CreateSearchTerms(
            request.Question,
            request.SearchTerms,
            Math.Max(1, request.MaxSearchTerms));
    }

    public static List<string> CreateSearchTerms(
        string question,
        IEnumerable<string>? explicitTerms = null,
        int maxTerms = 8)
    {
        var terms = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddTerm(string? term)
        {
            term = CleanTerm(term);
            if (string.IsNullOrWhiteSpace(term))
                return;

            var key = FoldText(term);
            if (key.Length < 2 || !seen.Add(key))
                return;

            terms.Add(term);
        }

        foreach (var term in explicitTerms ?? Enumerable.Empty<string>())
        {
            AddTerm(term);
        }

        foreach (Match match in QuotedPhraseRegex.Matches(question ?? string.Empty))
        {
            AddTerm(match.Groups["phrase"].Value);
        }

        if (!string.IsNullOrWhiteSpace(question) && question.Trim().Length <= 180)
        {
            AddTerm(question);
        }

        var tokens = ExtractSignificantTokens(question)
            .Take(12)
            .ToList();

        if (tokens.Count > 1)
        {
            AddTerm(string.Join(" ", tokens.Take(6)));

            foreach (var phrase in BuildNgrams(tokens, 3).Concat(BuildNgrams(tokens, 2)))
            {
                AddTerm(phrase);
            }
        }

        foreach (var token in tokens)
        {
            AddTerm(token);
        }

        return terms
            .Take(Math.Max(1, maxTerms))
            .ToList();
    }

    public static async Task<LiveRagRetrievalResult> RetrieveFromSearchAsync(
        IDataSourceConnector connector,
        LiveRagQueryRequest request,
        Func<string, int, CancellationToken, Task<IEnumerable<SearchResult>>> searchAsync,
        Func<SearchResult, string?, LiveRagQueryRequest, LiveRagContextItem>? itemFactory,
        bool native,
        CancellationToken cancellationToken)
    {
        var result = new LiveRagRetrievalResult
        {
            IsNativeLiveRag = native,
            SourceName = connector.Name,
            ConnectorId = connector.Id
        };

        var seenReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var searchTerm in GetSearchTerms(request))
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.ExecutedQueries.Add(searchTerm);

            var searchResults = await searchAsync(
                searchTerm,
                Math.Max(1, request.MaxResultsPerSearchTerm),
                cancellationToken);

            foreach (var searchResult in searchResults
                .OrderByDescending(item => item.RelevanceScore))
            {
                var referenceKey = string.IsNullOrWhiteSpace(searchResult.OriginalReference)
                    ? $"{searchResult.ConnectorId}:{searchResult.Title}:{searchResult.Description}"
                    : $"{searchResult.ConnectorId}:{searchResult.OriginalReference}";

                if (!seenReferences.Add(referenceKey))
                    continue;

                var liveScore = CalculateLiveRagScore(searchResult, request.Question, result.ExecutedQueries);
                var contextItem = itemFactory?.Invoke(searchResult, searchTerm, request)
                    ?? LiveRagContextItem.FromSearchResult(
                        searchResult,
                        searchTerm,
                        request.MaxCharactersPerItem,
                        request.IncludeMetadata);

                contextItem.FromNativeLiveRag = native;
                contextItem.RelevanceScore = Math.Max(contextItem.RelevanceScore, liveScore);
                if (string.IsNullOrWhiteSpace(contextItem.SourceName))
                    contextItem.SourceName = searchResult.SourceName;
                if (string.IsNullOrWhiteSpace(contextItem.ConnectorId))
                    contextItem.ConnectorId = searchResult.ConnectorId;

                result.ContextItems.Add(contextItem);

                if (result.ContextItems.Count >= Math.Max(1, request.MaxContextItems))
                    return result;
            }
        }

        result.ContextItems = result.ContextItems
            .OrderByDescending(item => item.RelevanceScore)
            .ToList();

        return result;
    }

    public static LiveRagContextItem CreateContextItem(
        SearchResult result,
        string? retrievalQuery,
        LiveRagQueryRequest request,
        string content)
    {
        content = NormalizeWhitespace(content);

        if (request.MaxCharactersPerItem > 0 && content.Length > request.MaxCharactersPerItem)
        {
            content = content[..request.MaxCharactersPerItem] + "...";
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
            Metadata = request.IncludeMetadata
                ? new Dictionary<string, object>(result.Metadata)
                : new Dictionary<string, object>()
        };
    }

    public static string BuildMetadataContent(SearchResult result, params string[] excludedKeys)
    {
        var exclude = new HashSet<string>(excludedKeys, StringComparer.OrdinalIgnoreCase);
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(result.Description))
        {
            sb.AppendLine(result.Description);
        }

        foreach (var kvp in result.Metadata)
        {
            if (exclude.Contains(kvp.Key))
                continue;

            var value = kvp.Value?.ToString();
            if (string.IsNullOrWhiteSpace(value))
                continue;

            sb.AppendLine($"{kvp.Key}: {value}");
        }

        return sb.ToString();
    }

    public static int CalculateLiveRagScore(
        SearchResult result,
        string question,
        IEnumerable<string>? searchTerms = null)
    {
        var searchableText = BuildSearchableText(result);
        if (string.IsNullOrWhiteSpace(searchableText))
            return result.RelevanceScore;

        var foldedText = FoldText(searchableText);
        var candidateTerms = CreateSearchTerms(question, searchTerms, 16);
        var score = result.RelevanceScore;

        foreach (var term in candidateTerms)
        {
            var foldedTerm = FoldText(term);
            if (foldedTerm.Length < 2)
                continue;

            if (foldedText.Contains(foldedTerm, StringComparison.Ordinal))
            {
                score = Math.Max(score, Math.Min(100, 55 + Math.Min(35, foldedTerm.Length * 2)));
            }
        }

        var tokens = ExtractSignificantTokens(string.Join(" ", candidateTerms), 20)
            .Select(FoldText)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tokens.Count > 0)
        {
            var matchedTokens = tokens.Count(token => foldedText.Contains(token, StringComparison.Ordinal));
            if (matchedTokens > 0)
            {
                var tokenScore = 35 + (int)Math.Round(60.0 * matchedTokens / tokens.Count);
                score = Math.Max(score, Math.Min(100, tokenScore));
            }
        }

        return Math.Clamp(score, 0, 100);
    }

    public static List<string> ExtractSignificantTokens(string? text, int maxTokens = 20)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        var tokens = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in TokenRegex.Matches(text))
        {
            var token = CleanTerm(match.Value);
            if (string.IsNullOrWhiteSpace(token))
                continue;

            var folded = FoldText(token);
            if (folded.Length < 3 && !folded.Any(char.IsDigit))
                continue;
            if (StopWords.Contains(folded))
                continue;
            if (!seen.Add(folded))
                continue;

            tokens.Add(token);
            if (tokens.Count >= Math.Max(1, maxTokens))
                break;
        }

        return tokens;
    }

    public static string NormalizeWhitespace(string text)
    {
        return WhitespaceRegex.Replace(text ?? string.Empty, " ").Trim();
    }

    public static string FoldText(string text)
    {
        return NormalizeWhitespace(text)
            .ToLowerInvariant()
            .Replace("\u00e4", "ae")
            .Replace("\u00f6", "oe")
            .Replace("\u00fc", "ue")
            .Replace("\u00df", "ss")
            .Replace("\u00c4", "ae")
            .Replace("\u00d6", "oe")
            .Replace("\u00dc", "ue")
            .Replace("\u00c3\u00a4", "ae")
            .Replace("\u00c3\u00b6", "oe")
            .Replace("\u00c3\u00bc", "ue")
            .Replace("\u00c3\u009f", "ss");
    }

    private static IEnumerable<string> BuildNgrams(IReadOnlyList<string> tokens, int size)
    {
        if (tokens.Count < size)
            yield break;

        for (var index = 0; index <= tokens.Count - size; index++)
        {
            yield return string.Join(" ", tokens.Skip(index).Take(size));
        }
    }

    private static string CleanTerm(string? term)
    {
        return NormalizeWhitespace((term ?? string.Empty).Trim(TrimChars));
    }

    private static string BuildSearchableText(SearchResult result)
    {
        var sb = new StringBuilder();
        sb.Append(result.Title).Append(' ');
        sb.Append(result.Description).Append(' ');
        sb.Append(result.OriginalReference).Append(' ');

        foreach (var kvp in result.Metadata.Take(30))
        {
            sb.Append(kvp.Key).Append(' ');
            var value = kvp.Value?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (value.Length > 500)
                    value = value[..500];
                sb.Append(value).Append(' ');
            }
        }

        return sb.ToString();
    }
}
