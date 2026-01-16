//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MSCC.Models;

namespace MSCC.Connectors;

/// <summary>
/// Dateisystem-Konnektor zum Durchsuchen von Dateien.
/// </summary>
public class FileSystemConnector : IDataSourceConnector, IDisposable
{
    private string _basePath = string.Empty;
    private string _searchPattern = "*.*";
    private bool _includeSubdirectories = true;

    public string Id => "filesystem-connector";
    public string Name => "Dateisystem";
    public string Description => "Durchsucht Dateien und Ordner im lokalen Dateisystem.";
    public string Version => "1.0.0";

    public IEnumerable<ConnectorParameter> ConfigurationParameters => new[]
    {
        new ConnectorParameter
        {
            Name = "BasePath",
            DisplayName = "Basispfad",
            Description = "Der Ordnerpfad, der durchsucht werden soll.",
            ParameterType = "path",
            IsRequired = true
        },
        new ConnectorParameter
        {
            Name = "SearchPattern",
            DisplayName = "Suchmuster",
            Description = "Dateimuster (z.B. *.txt, *.pdf)",
            ParameterType = "string",
            IsRequired = false,
            DefaultValue = "*.*"
        },
        new ConnectorParameter
        {
            Name = "IncludeSubdirectories",
            DisplayName = "Unterordner einschließen",
            Description = "Gibt an, ob Unterordner durchsucht werden sollen.",
            ParameterType = "bool",
            IsRequired = false,
            DefaultValue = "true"
        }
    };

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
            System.Diagnostics.Debug.WriteLine($"[FileSystemConnector] BasePath is empty");
            return results;
        }

        if (!Directory.Exists(_basePath))
        {
            System.Diagnostics.Debug.WriteLine($"[FileSystemConnector] BasePath does not exist: {_basePath}");
            return results;
        }

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            System.Diagnostics.Debug.WriteLine($"[FileSystemConnector] SearchTerm is empty");
            return results;
        }

        System.Diagnostics.Debug.WriteLine($"[FileSystemConnector] Searching for '{searchTerm}' in '{_basePath}' with pattern '{_searchPattern}'");

        await Task.Run(() =>
        {
            try
            {
                var searchOption = _includeSubdirectories
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

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
                    
                    // Suche im Dateinamen
                    if (fileName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    {
                        var fileInfo = new FileInfo(file);
                        results.Add(new SearchResult
                        {
                            Title = fileName,
                            Description = $"Pfad: {file}",
                            SourceName = "Dateisystem",
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

                System.Diagnostics.Debug.WriteLine($"[FileSystemConnector] Scanned {filesScanned} files, found {results.Count} matches");
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileSystemConnector] Access denied: {ex.Message}");
            }
            catch (DirectoryNotFoundException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileSystemConnector] Directory not found: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileSystemConnector] Error: {ex.Message}");
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
        // Keine Ressourcen freizugeben
        GC.SuppressFinalize(this);
    }

    public DetailViewConfiguration GetDetailViewConfiguration(SearchResult result)
    {
        var extension = result.Metadata.GetValueOrDefault("Extension")?.ToString()?.ToLowerInvariant() ?? "";
        
        // Medien-Dateien
        if (IsImageFile(extension))
        {
            return new DetailViewConfiguration
            {
                ViewType = DetailViewType.Media,
                MediaPathProperty = "OriginalReference",
                DisplayProperties = new List<string> { "Size", "Created", "Modified" },
                Actions = GetFileActions()
            };
        }
        
        // Standard: Tabellen-Ansicht mit Datei-Eigenschaften
        return new DetailViewConfiguration
        {
            ViewType = DetailViewType.Table,
            TableColumns = new List<TableColumnDefinition>
            {
                new() { PropertyName = "Extension", Header = "Typ", Width = "60" },
                new() { PropertyName = "Size", Header = "Größe", Width = "100", Format = "{0:N0} Bytes" },
                new() { PropertyName = "Created", Header = "Erstellt", Width = "150", Format = "{0:g}" },
                new() { PropertyName = "Modified", Header = "Geändert", Width = "150", Format = "{0:g}" }
            },
            Actions = GetFileActions()
        };
    }

    private static List<ResultAction> GetFileActions()
    {
        return new List<ResultAction>
        {
            new() { Id = "open", Name = "Öffnen", Icon = "??", Description = "Datei mit Standardprogramm öffnen" },
            new() { Id = "open-folder", Name = "Ordner öffnen", Icon = "??", Description = "Übergeordneten Ordner öffnen" },
            new() { Id = "copy-path", Name = "Pfad kopieren", Icon = "??", Description = "Dateipfad in Zwischenablage kopieren" }
        };
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
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            System.Windows.Clipboard.SetText(result.OriginalReference);
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
