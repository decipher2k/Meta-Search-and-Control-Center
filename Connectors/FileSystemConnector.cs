// Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Diagnostics;
using System.IO;
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
            new() { Id = "open", Name = L.Connector_FileSystem_Open, Icon = "[File]", Description = L.Connector_FileSystem_Open_Desc },
            new() { Id = "open-folder", Name = L.Connector_FileSystem_OpenFolder, Icon = "[Folder]", Description = L.Connector_FileSystem_OpenFolder_Desc },
            new() { Id = "copy-path", Name = L.Connector_FileSystem_CopyPath, Icon = "[Copy]", Description = L.Connector_FileSystem_CopyPath_Desc }
        ];
    }

    private static bool IsImageFile(string extension)
    {
        return extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg";
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
