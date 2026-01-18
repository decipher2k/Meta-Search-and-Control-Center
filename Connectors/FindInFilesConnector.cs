// Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using MSCC.Localization;
using MSCC.Models;

namespace MSCC.Connectors;

/// <summary>
/// Connector for searching text content within files.
/// Supports both plain text search and regular expressions.
/// </summary>
public class FindInFilesConnector : IDataSourceConnector, IDisposable
{
    private string _basePath = string.Empty;
    private string _filePattern = "*.*";
    private bool _includeSubdirectories = true;
    private string _searchString = string.Empty;
    private bool _useRegex;
    private bool _caseSensitive;
    private Regex? _compiledRegex;
    private static Strings L => Strings.Instance;

    public string Id => "find-in-files-connector";
    public string Name => L.Connector_FindInFiles_Name;
    public string Description => L.Connector_FindInFiles_Description;
    public string Version => "1.0.0";

    public IEnumerable<ConnectorParameter> ConfigurationParameters =>
    [
        new ConnectorParameter
        {
            Name = "BasePath",
            DisplayName = L.Connector_FindInFiles_BasePath,
            Description = L.Connector_FindInFiles_BasePath_Desc,
            ParameterType = "path",
            IsRequired = true
        },
        new ConnectorParameter
        {
            Name = "FilePattern",
            DisplayName = L.Connector_FindInFiles_FilePattern,
            Description = L.Connector_FindInFiles_FilePattern_Desc,
            ParameterType = "string",
            IsRequired = false,
            DefaultValue = "*.*"
        },
        new ConnectorParameter
        {
            Name = "IncludeSubdirectories",
            DisplayName = L.Connector_FindInFiles_IncludeSubdirs,
            Description = L.Connector_FindInFiles_IncludeSubdirs_Desc,
            ParameterType = "bool",
            IsRequired = false,
            DefaultValue = "true"
        },
        new ConnectorParameter
        {
            Name = "UseRegex",
            DisplayName = L.Connector_FindInFiles_UseRegex,
            Description = L.Connector_FindInFiles_UseRegex_Desc,
            ParameterType = "bool",
            IsRequired = false,
            DefaultValue = "false"
        },
        new ConnectorParameter
        {
            Name = "CaseSensitive",
            DisplayName = L.Connector_FindInFiles_CaseSensitive,
            Description = L.Connector_FindInFiles_CaseSensitive_Desc,
            ParameterType = "bool",
            IsRequired = false,
            DefaultValue = "false"
        }
    ];

    public Task<bool> InitializeAsync(Dictionary<string, string> configuration)
    {
        if (!configuration.TryGetValue("BasePath", out var basePath) || string.IsNullOrEmpty(basePath))
        {
            return Task.FromResult(false);
        }

        _basePath = basePath;

        if (configuration.TryGetValue("FilePattern", out var pattern) && !string.IsNullOrEmpty(pattern))
        {
            _filePattern = pattern;
        }

        if (configuration.TryGetValue("IncludeSubdirectories", out var includeSubDirs))
        {
            _includeSubdirectories = bool.TryParse(includeSubDirs, out var result) && result;
        }

        if (configuration.TryGetValue("UseRegex", out var useRegex))
        {
            _useRegex = bool.TryParse(useRegex, out var result) && result;
        }

        if (configuration.TryGetValue("CaseSensitive", out var caseSensitive))
        {
            _caseSensitive = bool.TryParse(caseSensitive, out var result) && result;
        }

        return Task.FromResult(Directory.Exists(_basePath));
    }

    public async Task<IEnumerable<SearchResult>> SearchAsync(
        string searchTerm,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SearchResult>();

        if (string.IsNullOrEmpty(_basePath) || !Directory.Exists(_basePath))
        {
            Debug.WriteLine($"[FindInFilesConnector] BasePath is empty or does not exist: {_basePath}");
            return results;
        }

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            Debug.WriteLine($"[FindInFilesConnector] SearchTerm is empty");
            return results;
        }

        Debug.WriteLine($"[FindInFilesConnector] Searching for '{searchTerm}' in '{_basePath}' (Regex: {_useRegex}, CaseSensitive: {_caseSensitive})");

        // Compile regex if needed
        Regex? regex = null;
        if (_useRegex)
        {
            try
            {
                var options = RegexOptions.Compiled;
                if (!_caseSensitive)
                {
                    options |= RegexOptions.IgnoreCase;
                }
                regex = new Regex(searchTerm, options);
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine($"[FindInFilesConnector] Invalid regex pattern: {ex.Message}");
                return results;
            }
        }

        await Task.Run(() =>
        {
            try
            {
                var files = Directory.EnumerateFiles(_basePath, _filePattern, new EnumerationOptions
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

                    try
                    {
                        var matches = SearchInFile(file, searchTerm, regex, cancellationToken);
                        if (matches.Count > 0 && results.Count < maxResults)
                        {
                            var fileInfo = new FileInfo(file);
                            var matchSummary = FormatMatchSummary(matches);

                            results.Add(new SearchResult
                            {
                                Title = Path.GetFileName(file),
                                Description = $"{matches.Count} {L.Connector_FindInFiles_MatchesFound}: {matchSummary}",
                                SourceName = L.Connector_FindInFiles_Name,
                                ConnectorId = Id,
                                OriginalReference = file,
                                RelevanceScore = Math.Min(100, matches.Count * 10),
                                Metadata = new Dictionary<string, object>
                                {
                                    [L.Connector_FindInFiles_Path] = file,
                                    [L.Connector_FindInFiles_Directory] = Path.GetDirectoryName(file) ?? "",
                                    [L.Connector_FindInFiles_FileName] = Path.GetFileName(file),
                                    [L.Connector_FindInFiles_Size] = fileInfo.Length,
                                    [L.Connector_FindInFiles_Modified] = fileInfo.LastWriteTime,
                                    [L.Connector_FindInFiles_MatchCount] = matches.Count,
                                    [L.Connector_FindInFiles_Matches] = matches,
                                    [L.Connector_FindInFiles_SearchTerm] = searchTerm
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[FindInFilesConnector] Error reading file {file}: {ex.Message}");
                    }
                }

                Debug.WriteLine($"[FindInFilesConnector] Scanned {filesScanned} files, found {results.Count} files with matches");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FindInFilesConnector] Error: {ex.Message}");
            }
        }, cancellationToken);

        return results;
    }

    private List<FileMatch> SearchInFile(string filePath, string searchTerm, Regex? regex, CancellationToken cancellationToken)
    {
        var matches = new List<FileMatch>();

        try
        {
            using var reader = new StreamReader(filePath);
            int lineNumber = 0;

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = reader.ReadLine();
                if (line == null)
                    break;

                lineNumber++;

                if (_useRegex && regex != null)
                {
                    var regexMatches = regex.Matches(line);
                    foreach (Match match in regexMatches)
                    {
                        matches.Add(new FileMatch
                        {
                            Line = lineNumber,
                            Column = match.Index + 1,
                            MatchedText = match.Value,
                            LineContent = TruncateLine(line, match.Index)
                        });
                    }
                }
                else
                {
                    var comparison = _caseSensitive 
                        ? StringComparison.Ordinal 
                        : StringComparison.OrdinalIgnoreCase;
                    
                    int startIndex = 0;
                    int index;
                    while ((index = line.IndexOf(searchTerm, startIndex, comparison)) >= 0)
                    {
                        matches.Add(new FileMatch
                        {
                            Line = lineNumber,
                            Column = index + 1,
                            MatchedText = line.Substring(index, searchTerm.Length),
                            LineContent = TruncateLine(line, index)
                        });
                        startIndex = index + 1;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FindInFilesConnector] Error searching in file {filePath}: {ex.Message}");
        }

        return matches;
    }

    private static string TruncateLine(string line, int matchIndex, int contextLength = 80)
    {
        if (line.Length <= contextLength)
            return line.Trim();

        int start = Math.Max(0, matchIndex - contextLength / 2);
        int end = Math.Min(line.Length, start + contextLength);
        
        if (end - start < contextLength && start > 0)
        {
            start = Math.Max(0, end - contextLength);
        }

        var result = line.Substring(start, end - start).Trim();
        
        if (start > 0)
            result = "..." + result;
        if (end < line.Length)
            result = result + "...";

        return result;
    }

    private static string FormatMatchSummary(List<FileMatch> matches)
    {
        if (matches.Count == 0)
            return "";

        if (matches.Count == 1)
        {
            return $"{matches[0].LineContent}";
        }

        var first = matches[0];
        return $"{first.LineContent}";
    }

    public Task<bool> TestConnectionAsync()
    {
        return Task.FromResult(
            !string.IsNullOrEmpty(_basePath) && Directory.Exists(_basePath));
    }

    public void Dispose()
    {
        _compiledRegex = null;
        GC.SuppressFinalize(this);
    }

    public DetailViewConfiguration GetDetailViewConfiguration(SearchResult result)
    {
        return new DetailViewConfiguration
        {
            ViewType = DetailViewType.Custom,
            DisplayProperties = [
                L.Connector_FindInFiles_Path,
                L.Connector_FindInFiles_FileName,
                L.Connector_FindInFiles_Size,
                L.Connector_FindInFiles_Modified,
                L.Connector_FindInFiles_MatchCount
            ],
            Actions = GetFileActions()
        };
    }

    private List<ResultAction> GetFileActions()
    {
        return
        [
            new() { Id = "open", Name = L.Connector_FindInFiles_Open, Icon = "[File]", Description = L.Connector_FindInFiles_Open_Desc },
            new() { Id = "open-folder", Name = L.Connector_FindInFiles_OpenFolder, Icon = "[Folder]", Description = L.Connector_FindInFiles_OpenFolder_Desc },
            new() { Id = "copy-path", Name = L.Connector_FindInFiles_CopyPath, Icon = "[Copy]", Description = L.Connector_FindInFiles_CopyPath_Desc },
            new() { Id = "copy-matches", Name = L.Connector_FindInFiles_CopyMatches, Icon = "[List]", Description = L.Connector_FindInFiles_CopyMatches_Desc }
        ];
    }

    public FrameworkElement? CreateCustomDetailView(SearchResult result)
    {
        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(10) };

        // File info section
        var fileInfoHeader = new System.Windows.Controls.TextBlock
        {
            Text = L.Connector_FindInFiles_FileInfo,
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 8)
        };
        panel.Children.Add(fileInfoHeader);

        // File path
        if (result.Metadata.TryGetValue(L.Connector_FindInFiles_Path, out var path))
        {
            var pathPanel = CreatePropertyRow(L.Connector_FindInFiles_Path, path?.ToString() ?? "");
            panel.Children.Add(pathPanel);
        }

        // File size
        if (result.Metadata.TryGetValue(L.Connector_FindInFiles_Size, out var size))
        {
            var sizeText = size is long sizeVal ? $"{sizeVal:N0} Bytes" : size?.ToString() ?? "";
            var sizePanel = CreatePropertyRow(L.Connector_FindInFiles_Size, sizeText);
            panel.Children.Add(sizePanel);
        }

        // Modified date
        if (result.Metadata.TryGetValue(L.Connector_FindInFiles_Modified, out var modified))
        {
            var modifiedText = modified is DateTime dt ? dt.ToString("g") : modified?.ToString() ?? "";
            var modifiedPanel = CreatePropertyRow(L.Connector_FindInFiles_Modified, modifiedText);
            panel.Children.Add(modifiedPanel);
        }

        // Matches section
        if (result.Metadata.TryGetValue(L.Connector_FindInFiles_Matches, out var matchesObj) && matchesObj is List<FileMatch> matches)
        {
            var matchesHeader = new System.Windows.Controls.TextBlock
            {
                Text = $"{L.Connector_FindInFiles_MatchesHeader} ({matches.Count})",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 16, 0, 8)
            };
            panel.Children.Add(matchesHeader);

            // Create a scrollable list of matches
            var matchesList = new System.Windows.Controls.ListBox
            {
                MaxHeight = 300,
                Margin = new Thickness(0, 0, 0, 8),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(1),
                BorderBrush = System.Windows.Media.Brushes.LightGray
            };

            foreach (var match in matches.Take(100)) // Limit to 100 matches for performance
            {
                var matchItem = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                
                var locationText = new System.Windows.Controls.TextBlock
                {
                    Text = $"{L.Connector_FindInFiles_Line} {match.Line}, {L.Connector_FindInFiles_Column} {match.Column}: ",
                    FontWeight = FontWeights.SemiBold,
                    Foreground = System.Windows.Media.Brushes.DarkBlue,
                    Margin = new Thickness(4, 2, 0, 2)
                };
                matchItem.Children.Add(locationText);

                var contentText = new System.Windows.Controls.TextBlock
                {
                    Text = match.LineContent,
                    Margin = new Thickness(0, 2, 4, 2),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 500
                };
                matchItem.Children.Add(contentText);

                matchesList.Items.Add(matchItem);
            }

            if (matches.Count > 100)
            {
                var moreText = new System.Windows.Controls.TextBlock
                {
                    Text = $"... {L.Connector_FindInFiles_AndMore} {matches.Count - 100} {L.Connector_FindInFiles_MoreMatches}",
                    FontStyle = FontStyles.Italic,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    Margin = new Thickness(4, 2, 4, 2)
                };
                matchesList.Items.Add(moreText);
            }

            panel.Children.Add(matchesList);
        }

        return panel;
    }

    private static System.Windows.Controls.StackPanel CreatePropertyRow(string label, string value)
    {
        var row = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness(0, 2, 0, 2)
        };

        var labelText = new System.Windows.Controls.TextBlock
        {
            Text = label + ": ",
            FontWeight = FontWeights.SemiBold,
            Width = 100
        };
        row.Children.Add(labelText);

        var valueText = new System.Windows.Controls.TextBlock
        {
            Text = value,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 400
        };
        row.Children.Add(valueText);

        return row;
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
                            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{result.OriginalReference}\"") { UseShellExecute = true });
                            return true;
                        }
                        break;

                    case "copy-path":
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Clipboard.SetText(result.OriginalReference);
                        });
                        return true;

                    case "copy-matches":
                        if (result.Metadata.TryGetValue(L.Connector_FindInFiles_Matches, out var matchesObj) && matchesObj is List<FileMatch> matches)
                        {
                            var text = FormatMatchesForClipboard(result.OriginalReference, matches);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Clipboard.SetText(text);
                            });
                            return true;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FindInFilesConnector] Action error: {ex.Message}");
                return false;
            }

            return false;
        });
    }

    private static string FormatMatchesForClipboard(string filePath, List<FileMatch> matches)
    {
        var lines = new List<string>
        {
            $"File: {filePath}",
            $"Matches: {matches.Count}",
            ""
        };

        foreach (var match in matches)
        {
            lines.Add($"Line {match.Line}, Column {match.Column}: {match.LineContent}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// Represents a match found in a file.
/// </summary>
public class FileMatch
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string MatchedText { get; set; } = string.Empty;
    public string LineContent { get; set; } = string.Empty;
}
