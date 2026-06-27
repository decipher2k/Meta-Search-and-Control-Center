// Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using MSCC.Localization;
using MSCC.Models;

namespace MSCC.Connectors;

/// <summary>
/// File system connector for searching files.
/// </summary>
public class FileSystemConnector : IDataSourceConnector, IDisposable
{
    private string _basePath = string.Empty;
    private string _searchPattern = "*.*";
    private bool _includeSubdirectories = true;
    private static Strings L => Strings.Instance;

    public string Id => "filesystem-connector";
    public string Name => L.Connector_FileSystem_Name;
    public string Description => L.Connector_FileSystem_Description;
    public string Version => "1.0.0";

    public IEnumerable<ConnectorParameter> ConfigurationParameters =>
    [
        new ConnectorParameter
        {
            Name = "BasePath",
            DisplayName = L.Connector_FileSystem_BasePath,
            Description = L.Connector_FileSystem_BasePath_Desc,
            ParameterType = "path",
            IsRequired = true
        },
        new ConnectorParameter
        {
            Name = "SearchPattern",
            DisplayName = L.Connector_FileSystem_SearchPattern,
            Description = L.Connector_FileSystem_SearchPattern_Desc,
            ParameterType = "string",
            IsRequired = false,
            DefaultValue = "*.*"
        },
        new ConnectorParameter
        {
            Name = "IncludeSubdirectories",
            DisplayName = L.Connector_FileSystem_IncludeSubdirs,
            Description = L.Connector_FileSystem_IncludeSubdirs_Desc,
            ParameterType = "bool",
            IsRequired = false,
            DefaultValue = "true"
        }
    ];

    public Task<bool> InitializeAsync(Dictionary<string, string> configuration)
    {
        if (!configuration.TryGetValue("BasePath", out var basePath) || string.IsNullOrEmpty(basePath))
        {
            return Task.FromResult(false);
        }

        _basePath = basePath;

        if (configuration.TryGetValue("SearchPattern", out var pattern))
        {
            _searchPattern = pattern;
        }

        if (configuration.TryGetValue("IncludeSubdirectories", out var includeSubDirs))
        {
            _includeSubdirectories = bool.TryParse(includeSubDirs, out var result) && result;
        }

        return Task.FromResult(Directory.Exists(_basePath));
    }

    public async Task<IEnumerable<SearchResult>> SearchAsync(
        string searchTerm,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SearchResult>();

        if (string.IsNullOrEmpty(_basePath))
        {
            Debug.WriteLine($"[FileSystemConnector] BasePath is empty");
            return results;
        }

        if (!Directory.Exists(_basePath))
        {
            Debug.WriteLine($"[FileSystemConnector] BasePath does not exist: {_basePath}");
            return results;
        }

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            Debug.WriteLine($"[FileSystemConnector] SearchTerm is empty");
            return results;
        }

        Debug.WriteLine($"[FileSystemConnector] Searching for '{searchTerm}' in '{_basePath}' with pattern '{_searchPattern}'");

        await Task.Run(() =>
        {
            try
            {
                var files = Directory.EnumerateFiles(_basePath, _searchPattern, new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = _includeSubdirectories
                });

                int filesScanned = 0;
                foreach (var file in files)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (results.Count >= maxResults)
                        break;

                    filesScanned++;
                    var fileName = Path.GetFileName(file);
                    
                    // Search in filename
                    if (fileName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    {
                        var fileInfo = new FileInfo(file);
                        results.Add(new SearchResult
                        {
                            Title = fileName,
                            Description = $"{L.Connector_FileSystem_Path}: {file}",
                            SourceName = L.Connector_FileSystem_Name,
                            ConnectorId = Id,
                            OriginalReference = file,
                            RelevanceScore = 100,
                            Metadata = new Dictionary<string, object>
                            {
                                ["Size"] = fileInfo.Length,
                                ["Created"] = fileInfo.CreationTime,
                                ["Modified"] = fileInfo.LastWriteTime,
                                ["Extension"] = fileInfo.Extension
                            }
                        });
                    }
                }

                Debug.WriteLine($"[FileSystemConnector] Scanned {filesScanned} files, found {results.Count} matches");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"[FileSystemConnector] Access denied: {ex.Message}");
            }
            catch (DirectoryNotFoundException ex)
            {
                Debug.WriteLine($"[FileSystemConnector] Directory not found: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileSystemConnector] Error: {ex.Message}");
            }
        }, cancellationToken);

        return results;
    }

    public bool SupportsLiveRag => true;

    public async Task<LiveRagRetrievalResult> RetrieveLiveRagContextAsync(
        LiveRagQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new LiveRagRetrievalResult
        {
            IsNativeLiveRag = true,
            SourceName = Name,
            ConnectorId = Id,
            ExecutedQueries = LiveRagConnectorHelpers.GetSearchTerms(request).ToList()
        };

        if (string.IsNullOrEmpty(_basePath) || !Directory.Exists(_basePath))
            return result;

        await Task.Run(() =>
        {
            var files = Directory.EnumerateFiles(_basePath, _searchPattern, new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = _includeSubdirectories
            });

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (result.ContextItems.Count >= Math.Max(1, request.MaxContextItems))
                    break;

                try
                {
                    var fileInfo = new FileInfo(file);
                    var fileName = Path.GetFileName(file);
                    var extension = fileInfo.Extension.ToLowerInvariant();
                    var nameScore = CalculateFileNameRagScore(fileName, result.ExecutedQueries);

                    if (!IsTextLikeFile(extension))
                    {
                        if (nameScore <= 0)
                            continue;

                        result.ContextItems.Add(CreateFileMetadataContextItem(
                            file,
                            fileInfo,
                            fileName,
                            nameScore,
                            request));
                        continue;
                    }

                    var text = ReadTextFilePreview(file, request.MaxCharactersPerItem * 3);
                    var contentScore = CalculateTextRagScore(text, result.ExecutedQueries);
                    var score = Math.Max(nameScore, contentScore);

                    if (score <= 0)
                        continue;

                    var snippet = ExtractBestSnippet(text, result.ExecutedQueries, request.MaxCharactersPerItem);
                    if (string.IsNullOrWhiteSpace(snippet))
                        snippet = text;

                    result.ContextItems.Add(new LiveRagContextItem
                    {
                        Title = fileName,
                        Content = snippet,
                        SourceName = Name,
                        ConnectorId = Id,
                        OriginalReference = file,
                        RelevanceScore = score,
                        FromNativeLiveRag = true,
                        Metadata = request.IncludeMetadata
                            ? new Dictionary<string, object>
                            {
                                ["Path"] = file,
                                ["Directory"] = Path.GetDirectoryName(file) ?? "",
                                ["Size"] = fileInfo.Length,
                                ["Created"] = fileInfo.CreationTime,
                                ["Modified"] = fileInfo.LastWriteTime,
                                ["Extension"] = extension
                            }
                            : new Dictionary<string, object>()
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FileSystemConnector] Live RAG read error for {file}: {ex.Message}");
                }
            }
        }, cancellationToken);

        result.ContextItems = result.ContextItems
            .OrderByDescending(item => item.RelevanceScore)
            .ToList();

        return result;
    }

    public Task<LiveRagSourceProfile> DescribeLiveRagCapabilitiesAsync(
        DataSource dataSource,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new LiveRagSourceProfile
        {
            DataSourceId = dataSource.Id,
            SourceName = dataSource.Name,
            ConnectorId = Id,
            Description = dataSource.Description,
            SupportsNativeLiveRag = true,
            SupportedOperations =
            [
                LiveRagOperationType.ContentScan,
                LiveRagOperationType.KeywordSearch,
                LiveRagOperationType.FetchById
            ],
            Fields = ["fileName", "path", "directory", "extension", "content", "modified", "created", "size"],
            Targets = [_basePath, _searchPattern],
            MaxOperations = 3,
            MaxResults = 50,
            Metadata =
            {
                ["includeSubdirectories"] = _includeSubdirectories,
                ["searchPattern"] = _searchPattern
            }
        });
    }

    public Task<bool> TestConnectionAsync()
    {
        return Task.FromResult(
            !string.IsNullOrEmpty(_basePath) && Directory.Exists(_basePath));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public DetailViewConfiguration GetDetailViewConfiguration(SearchResult result)
    {
        var extension = result.Metadata.GetValueOrDefault("Extension")?.ToString()?.ToLowerInvariant() ?? "";
        
        // Media files
        if (IsImageFile(extension))
        {
            return new DetailViewConfiguration
            {
                ViewType = DetailViewType.Media,
                MediaPathProperty = "OriginalReference",
                DisplayProperties = ["Size", "Created", "Modified"],
                Actions = GetFileActions()
            };
        }
        
        // Default: Table view with file properties
        return new DetailViewConfiguration
        {
            ViewType = DetailViewType.Table,
            TableColumns =
            [
                new() { PropertyName = "Extension", Header = L.Connector_FileSystem_Type, Width = "60" },
                new() { PropertyName = "Size", Header = L.Connector_FileSystem_Size, Width = "100", Format = "{0:N0} Bytes" },
                new() { PropertyName = "Created", Header = L.Connector_FileSystem_Created, Width = "150", Format = "{0:g}" },
                new() { PropertyName = "Modified", Header = L.Connector_FileSystem_Modified, Width = "150", Format = "{0:g}" }
            ],
            Actions = GetFileActions()
        };
    }

    private List<ResultAction> GetFileActions()
    {
        return
        [
            new() { Id = "open", Name = L.Connector_FileSystem_Open, Icon = "\uD83D\uDCC4", Description = L.Connector_FileSystem_Open_Desc },
            new() { Id = "open-folder", Name = L.Connector_FileSystem_OpenFolder, Icon = "\uD83D\uDCC1", Description = L.Connector_FileSystem_OpenFolder_Desc },
            new() { Id = "copy-path", Name = L.Connector_FileSystem_CopyPath, Icon = "\uD83D\uDCCB", Description = L.Connector_FileSystem_CopyPath_Desc }
        ];
    }

    private static bool IsImageFile(string extension)
    {
        return extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg";
    }

    private static bool IsTextLikeFile(string extension)
    {
        return extension is ".txt" or ".md" or ".markdown" or ".csv" or ".tsv" or ".json" or ".xml"
            or ".html" or ".htm" or ".css" or ".js" or ".ts" or ".cs" or ".xaml" or ".sql"
            or ".log" or ".ini" or ".yaml" or ".yml" or ".ps1" or ".bat" or ".cmd" or ".py"
            or ".java" or ".cpp" or ".c" or ".h" or ".hpp" or ".php" or ".rb" or ".go" or ".rs";
    }

    private static LiveRagContextItem CreateFileMetadataContextItem(
        string file,
        FileInfo fileInfo,
        string fileName,
        int score,
        LiveRagQueryRequest request)
    {
        var content = $"{L.Connector_FileSystem_Path}: {file}\n{L.Connector_FileSystem_Type}: {fileInfo.Extension}\n{L.Connector_FileSystem_Size}: {fileInfo.Length:N0} Bytes";
        return new LiveRagContextItem
        {
            Title = fileName,
            Content = content,
            SourceName = L.Connector_FileSystem_Name,
            ConnectorId = "filesystem-connector",
            OriginalReference = file,
            RelevanceScore = score,
            FromNativeLiveRag = true,
            Metadata = request.IncludeMetadata
                ? new Dictionary<string, object>
                {
                    ["Path"] = file,
                    ["Directory"] = Path.GetDirectoryName(file) ?? "",
                    ["Size"] = fileInfo.Length,
                    ["Created"] = fileInfo.CreationTime,
                    ["Modified"] = fileInfo.LastWriteTime,
                    ["Extension"] = fileInfo.Extension
                }
                : new Dictionary<string, object>()
        };
    }

    private static int CalculateFileNameRagScore(string fileName, IEnumerable<string> terms)
    {
        var score = 0;
        foreach (var term in terms)
        {
            if (fileName.Equals(term, StringComparison.OrdinalIgnoreCase))
                score = Math.Max(score, 100);
            else if (fileName.Contains(term, StringComparison.OrdinalIgnoreCase))
                score = Math.Max(score, 80);
        }

        return score;
    }

    private static int CalculateTextRagScore(string text, IEnumerable<string> terms)
    {
        var score = 0;
        foreach (var term in terms)
        {
            if (string.IsNullOrWhiteSpace(term))
                continue;

            var count = CountOccurrences(text, term);
            if (count > 0)
                score = Math.Max(score, Math.Min(100, 50 + count * 10));
        }

        return score;
    }

    private static int CountOccurrences(string text, string term)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += Math.Max(1, term.Length);
        }

        return count;
    }

    private static string ReadTextFilePreview(string file, int maxCharacters)
    {
        maxCharacters = Math.Max(1000, maxCharacters);
        using var reader = new StreamReader(file, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[maxCharacters];
        var read = reader.Read(buffer, 0, buffer.Length);
        return new string(buffer, 0, read);
    }

    private static string ExtractBestSnippet(string text, IEnumerable<string> terms, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        maxCharacters = Math.Max(250, maxCharacters);
        var bestIndex = -1;
        foreach (var term in terms.Where(term => !string.IsNullOrWhiteSpace(term)))
        {
            bestIndex = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (bestIndex >= 0)
                break;
        }

        if (bestIndex < 0 || text.Length <= maxCharacters)
            return LiveRagConnectorHelpers.NormalizeWhitespace(text.Length > maxCharacters ? text[..maxCharacters] : text);

        var start = Math.Max(0, bestIndex - maxCharacters / 3);
        var length = Math.Min(maxCharacters, text.Length - start);
        var snippet = text.Substring(start, length);
        if (start > 0)
            snippet = "..." + snippet;
        if (start + length < text.Length)
            snippet += "...";

        return LiveRagConnectorHelpers.NormalizeWhitespace(snippet);
    }

    public FrameworkElement? CreateCustomDetailView(SearchResult result)
    {
        var extension = result.Metadata.GetValueOrDefault("Extension")?.ToString()?.ToLowerInvariant() ?? "";
        
        if (IsImageFile(extension) && File.Exists(result.OriginalReference))
        {
            try
            {
                var image = new System.Windows.Controls.Image
                {
                    Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(result.OriginalReference)),
                    MaxHeight = 300,
                    MaxWidth = 400,
                    Stretch = System.Windows.Media.Stretch.Uniform
                };
                
                var border = new Border
                {
                    Child = image,
                    BorderBrush = System.Windows.Media.Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(8)
                };
                
                return border;
            }
            catch
            {
                return null;
            }
        }
        
        return null;
    }

    public async Task<bool> ExecuteActionAsync(SearchResult result, string actionId)
    {
        return await Task.Run(() =>
        {
            try
            {
                switch (actionId)
                {
                    case "open":
                        if (File.Exists(result.OriginalReference))
                        {
                            Process.Start(new ProcessStartInfo(result.OriginalReference) { UseShellExecute = true });
                            return true;
                        }
                        break;
                        
                    case "open-folder":
                        var folder = Path.GetDirectoryName(result.OriginalReference);
                        if (folder != null && Directory.Exists(folder))
                        {
                            Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
                            return true;
                        }
                        break;
                        
                    case "copy-path":
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Clipboard.SetText(result.OriginalReference);
                        });
                        return true;
                }
            }
            catch
            {
                return false;
            }
            
            return false;
        });
    }
}
