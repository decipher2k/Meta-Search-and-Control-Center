//Meta Search and Control Center (c) 2026 Dennis Michael Heine
namespace MSCC.Scripting;

/// <summary>
/// Metadaten eines Konnektor-Scripts.
/// </summary>
public class ScriptMetadata
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;
    public bool IsEnabled { get; set; } = true;
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Gespeichertes Konnektor-Script.
/// </summary>
public class ConnectorScript
{
    public ScriptMetadata Metadata { get; set; } = new();
    public string SourceCode { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public bool IsCompiled { get; set; }
    public List<string> CompilationErrors { get; set; } = new();
}

/// <summary>
/// Ergebnis einer Script-Kompilierung.
/// </summary>
public class ScriptCompilationResult
{
    public bool Success { get; set; }
    public List<CompilationError> Errors { get; set; } = new();
    public List<CompilationError> Warnings { get; set; } = new();
    public System.Reflection.Assembly? CompiledAssembly { get; set; }
    public Connectors.IDataSourceConnector? ConnectorInstance { get; set; }
}

/// <summary>
/// Ein einzelner Kompilierungsfehler oder Warnung.
/// </summary>
public class CompilationError
{
    public string ErrorId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public string Severity { get; set; } = "Error";
}

/// <summary>
/// Code-Completion-Vorschlag.
/// </summary>
public class ScriptCompletionItem
{
    public string DisplayText { get; set; } = string.Empty;
    public string InsertText { get; set; } = string.Empty;
    public string Kind { get; set; } = "Method";
    public string? Description { get; set; }
    public string? Signature { get; set; }
}