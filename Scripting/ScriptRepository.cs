//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.IO;
using System.Text.Json;
using MSCC.Connectors;
using MSCC.Services;

namespace MSCC.Scripting;

public class ScriptRepository
{
    private readonly string _scriptsDirectory;
    private readonly ScriptingService _scriptingService;
    private readonly Dictionary<string, ConnectorScript> _loadedScripts = new();
    private readonly Dictionary<string, IDataSourceConnector> _compiledConnectors = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ScriptRepository(ScriptingService scriptingService, string? scriptsDirectory = null)
    {
        _scriptingService = scriptingService;
        _scriptsDirectory = scriptsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MSCC", "Scripts");

        EnsureDirectoryExists();
    }

    public string ScriptsDirectory => _scriptsDirectory;

    public ConnectorScript Create(string name, string? description = null)
    {
        var script = new ConnectorScript
        {
            Metadata = new ScriptMetadata
            {
                Name = name,
                Description = description ?? string.Empty
            },
            SourceCode = ScriptingService.GetScriptTemplate(name, Guid.NewGuid().ToString())
        };

        _loadedScripts[script.Metadata.Id] = script;
        return script;
    }

    public ConnectorScript? GetById(string id) => _loadedScripts.GetValueOrDefault(id);

    public IEnumerable<ConnectorScript> GetAll() => _loadedScripts.Values.ToList();

    public bool Update(ConnectorScript script)
    {
        if (!_loadedScripts.ContainsKey(script.Metadata.Id))
            return false;

        script.Metadata.ModifiedAt = DateTime.Now;
        script.IsCompiled = false;
        _loadedScripts[script.Metadata.Id] = script;
        _compiledConnectors.Remove(script.Metadata.Id);

        return true;
    }

    public bool Delete(string id)
    {
        if (!_loadedScripts.TryGetValue(id, out var script))
            return false;

        if (!string.IsNullOrEmpty(script.FilePath) && File.Exists(script.FilePath))
        {
            try
            {
                File.Delete(script.FilePath);
                var metaPath = script.FilePath + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);
            }
            catch { }
        }

        _loadedScripts.Remove(id);
        _compiledConnectors.Remove(id);
        return true;
    }

    public bool Save(ConnectorScript script)
    {
        try
        {
            EnsureDirectoryExists();
            var fileName = SanitizeFileName(script.Metadata.Name);
            var filePath = Path.Combine(_scriptsDirectory, $"{fileName}_{script.Metadata.Id[..8]}.cs");
            var metaPath = filePath + ".meta";

            File.WriteAllText(filePath, script.SourceCode);
            File.WriteAllText(metaPath, JsonSerializer.Serialize(script.Metadata, JsonOptions));

            script.FilePath = filePath;
            script.Metadata.ModifiedAt = DateTime.Now;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<int> LoadAllAsync()
    {
        EnsureDirectoryExists();
        var count = 0;

        var files = Directory.GetFiles(_scriptsDirectory, "*.cs");
        System.Diagnostics.Debug.WriteLine($"ScriptRepository: Found {files.Length} script files in {_scriptsDirectory}");

        foreach (var filePath in files)
        {
            try
            {
                var script = await LoadFromFileAsync(filePath);
                if (script != null)
                {
                    _loadedScripts[script.Metadata.Id] = script;
                    count++;
                    System.Diagnostics.Debug.WriteLine($"ScriptRepository: Loaded script '{script.Metadata.Name}' (Id: {script.Metadata.Id}, Enabled: {script.Metadata.IsEnabled})");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ScriptRepository: Error loading script from {filePath}: {ex.Message}");
            }
        }

        System.Diagnostics.Debug.WriteLine($"ScriptRepository: Total {count} scripts loaded");
        return count;
    }

    public async Task<ConnectorScript?> LoadFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        var script = new ConnectorScript
        {
            FilePath = filePath,
            SourceCode = await File.ReadAllTextAsync(filePath)
        };

        var metaPath = filePath + ".meta";
        if (File.Exists(metaPath))
        {
            try
            {
                var metaJson = await File.ReadAllTextAsync(metaPath);
                var metadata = JsonSerializer.Deserialize<ScriptMetadata>(metaJson, JsonOptions);
                if (metadata != null) script.Metadata = metadata;
            }
            catch
            {
                script.Metadata.Name = Path.GetFileNameWithoutExtension(filePath);
            }
        }
        else
        {
            script.Metadata.Name = Path.GetFileNameWithoutExtension(filePath);
        }

        return script;
    }

    public ScriptCompilationResult CompileAndRegister(ConnectorScript script)
    {
        var result = _scriptingService.Compile(script);

        if (result.Success && result.ConnectorInstance != null)
        {
            script.IsCompiled = true;
            script.CompilationErrors.Clear();
            _compiledConnectors[script.Metadata.Id] = result.ConnectorInstance;
            GlobalState.RegisterConnector(result.ConnectorInstance);
        }
        else
        {
            script.IsCompiled = false;
            script.CompilationErrors = result.Errors.Select(e => e.Message).ToList();
        }

        return result;
    }

    public async Task<(int success, int failed)> CompileAllAsync()
    {
        int success = 0, failed = 0;

        var enabledScripts = _loadedScripts.Values.Where(s => s.Metadata.IsEnabled).ToList();
        System.Diagnostics.Debug.WriteLine($"ScriptRepository: Compiling {enabledScripts.Count} enabled scripts");

        foreach (var script in enabledScripts)
        {
            System.Diagnostics.Debug.WriteLine($"ScriptRepository: Compiling '{script.Metadata.Name}'...");
            
            // Kompilierung im Thread-Pool ausführen, aber auf das Ergebnis warten
            var result = await Task.Run(() => CompileAndRegister(script));
            
            if (result.Success)
            {
                success++;
                System.Diagnostics.Debug.WriteLine($"ScriptRepository: Successfully compiled and registered '{script.Metadata.Name}'");
            }
            else
            {
                failed++;
                System.Diagnostics.Debug.WriteLine($"ScriptRepository: Failed to compile '{script.Metadata.Name}': {string.Join(", ", result.Errors.Select(e => e.Message))}");
            }
        }

        System.Diagnostics.Debug.WriteLine($"ScriptRepository: Compilation complete - {success} success, {failed} failed");
        System.Diagnostics.Debug.WriteLine($"ScriptRepository: Total connectors in GlobalState: {GlobalState.Connectors.Count}");
        
        return (success, failed);
    }

    public async Task<ConnectorScript?> ImportAsync(string sourcePath)
    {
        var script = await LoadFromFileAsync(sourcePath);
        if (script == null) return null;

        script.Metadata.Id = Guid.NewGuid().ToString();
        script.Metadata.CreatedAt = DateTime.Now;
        script.FilePath = null;

        _loadedScripts[script.Metadata.Id] = script;
        return script;
    }

    public bool Export(string scriptId, string targetPath)
    {
        if (!_loadedScripts.TryGetValue(scriptId, out var script))
            return false;

        try
        {
            File.WriteAllText(targetPath, script.SourceCode);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_scriptsDirectory))
            Directory.CreateDirectory(_scriptsDirectory);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !invalid.Contains(c)).ToArray());
    }
}
