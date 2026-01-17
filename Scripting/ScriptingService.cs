//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using MSCC.Connectors;

namespace MSCC.Scripting;

public class ScriptingService
{
    private readonly List<MetadataReference> _references;
    private readonly CSharpCompilationOptions _compilationOptions;
    private AdhocWorkspace? _workspace;

    public ScriptingService()
    {
        _references = GetDefaultReferences();
        _compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: OptimizationLevel.Debug,
            allowUnsafe: false);
    }

    /// <summary>
    /// Kompiliert ein Konnektor-Script.
    /// </summary>
    public ScriptCompilationResult Compile(ConnectorScript script)
    {
        var result = new ScriptCompilationResult();

        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(
                script.SourceCode,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp12));

            var compilation = CSharpCompilation.Create(
                $"Script_{script.Metadata.Id}",
                new[] { syntaxTree },
                _references,
                _compilationOptions);

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                foreach (var diagnostic in emitResult.Diagnostics)
                {
                    var error = new CompilationError
                    {
                        ErrorId = diagnostic.Id,
                        Message = diagnostic.GetMessage(),
                        Severity = diagnostic.Severity.ToString()
                    };

                    var lineSpan = diagnostic.Location.GetLineSpan();
                    error.Line = lineSpan.StartLinePosition.Line + 1;
                    error.Column = lineSpan.StartLinePosition.Character + 1;

                    if (diagnostic.Severity == DiagnosticSeverity.Error)
                        result.Errors.Add(error);
                    else if (diagnostic.Severity == DiagnosticSeverity.Warning)
                        result.Warnings.Add(error);
                }

                result.Success = false;
                return result;
            }

            ms.Seek(0, SeekOrigin.Begin);
            result.CompiledAssembly = Assembly.Load(ms.ToArray());
            result.Success = true;
            result.ConnectorInstance = CreateConnectorInstance(result.CompiledAssembly);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(new CompilationError
            {
                ErrorId = "SCRIPT001",
                Message = $"Kompilierungsfehler: {ex.Message}",
                Severity = "Error"
            });
        }

        return result;
    }

    /// <summary>
    /// Validiert ein Script ohne vollständige Kompilierung.
    /// </summary>
    public List<CompilationError> Validate(string sourceCode)
    {
        var errors = new List<CompilationError>();

        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(
                sourceCode,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp12));

            foreach (var diagnostic in syntaxTree.GetDiagnostics())
            {
                var lineSpan = diagnostic.Location.GetLineSpan();
                errors.Add(new CompilationError
                {
                    ErrorId = diagnostic.Id,
                    Message = diagnostic.GetMessage(),
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1,
                    Severity = diagnostic.Severity.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            errors.Add(new CompilationError
            {
                ErrorId = "PARSE001",
                Message = $"Parse-Fehler: {ex.Message}",
                Severity = "Error"
            });
        }

        return errors;
    }

    /// <summary>
    /// Holt Code-Completion-Vorschläge.
    /// </summary>
    public async Task<List<ScriptCompletionItem>> GetCompletionsAsync(string sourceCode, int position)
    {
        var completions = new List<ScriptCompletionItem>();

        try
        {
            _workspace ??= new AdhocWorkspace(MefHostServices.DefaultHost);

            var projectInfo = ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Create(),
                "ScriptProject",
                "ScriptAssembly",
                LanguageNames.CSharp,
                compilationOptions: _compilationOptions,
                metadataReferences: _references);

            var project = _workspace.AddProject(projectInfo);
            var document = _workspace.AddDocument(project.Id, "Script.cs", SourceText.From(sourceCode));

            var completionService = CompletionService.GetService(document);
            if (completionService != null)
            {
                var results = await completionService.GetCompletionsAsync(document, position);
                if (results != null)
                {
                    foreach (var item in results.ItemsList.Take(50))
                    {
                        completions.Add(new ScriptCompletionItem
                        {
                            DisplayText = item.DisplayText,
                            InsertText = item.DisplayText,
                            Kind = item.Tags.FirstOrDefault() ?? "Unknown",
                            Description = item.InlineDescription
                        });
                    }
                }
            }

            _workspace.ClearSolution();
        }
        catch
        {
            // Completion-Fehler ignorieren
        }

        return completions;
    }

    private IDataSourceConnector? CreateConnectorInstance(Assembly assembly)
    {
        try
        {
            var connectorType = assembly.GetTypes()
                .FirstOrDefault(t =>
                    typeof(IDataSourceConnector).IsAssignableFrom(t) &&
                    !t.IsAbstract &&
                    t.IsClass);

            if (connectorType == null)
                return null;

            return Activator.CreateInstance(connectorType) as IDataSourceConnector;
        }
        catch
        {
            return null;
        }
    }

    private static List<MetadataReference> GetDefaultReferences()
    {
        var references = new List<MetadataReference>();
        var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        var essentialAssemblies = new[]
        {
            "System.Runtime.dll",
            "System.Collections.dll",
            "System.Linq.dll",
            "System.Threading.Tasks.dll",
            "System.Net.Http.dll",
            "System.Text.Json.dll",
            "System.ComponentModel.dll",
            "System.ObjectModel.dll",
            "netstandard.dll",
            "System.Private.CoreLib.dll"
        };

        foreach (var assembly in essentialAssemblies)
        {
            var path = Path.Combine(runtimePath, assembly);
            if (File.Exists(path))
                references.Add(MetadataReference.CreateFromFile(path));
        }

        var wpfAssemblies = new[] { "PresentationCore.dll", "PresentationFramework.dll", "WindowsBase.dll" };
        foreach (var assembly in wpfAssemblies)
        {
            var path = Path.Combine(runtimePath, assembly);
            if (File.Exists(path))
                references.Add(MetadataReference.CreateFromFile(path));
        }

        references.Add(MetadataReference.CreateFromFile(typeof(ScriptingService).Assembly.Location));
        return references;
    }

    /// <summary>
    /// Generiert ein Template für ein neues Konnektor-Script.
    /// </summary>
    public static string GetScriptTemplate(string connectorName, string connectorId)
    {
        var safeName = new string(connectorName.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrEmpty(safeName)) safeName = "Custom";

        return $@"using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MSCC.Connectors;
using MSCC.Models;
using MSCC.Scripting;

namespace MSCC.Scripts
{{
    public class {safeName}Connector : ScriptedConnectorBase
    {{
        public override string Id => ""{connectorId}"";
        public override string Name => ""{connectorName}"";
        public override string Description => ""Beschreibung"";

        public override IEnumerable<ConnectorParameter> ConfigurationParameters => new[]
        {{
            new ConnectorParameter
            {{
                Name = ""ApiUrl"",
                DisplayName = ""API URL"",
                Description = ""Die URL der API"",
                IsRequired = true
            }}
        }};

        public override async Task<IEnumerable<SearchResult>> SearchAsync(
            string searchTerm, int maxResults = 100, CancellationToken cancellationToken = default)
        {{
            var results = new List<SearchResult>();

            for (int i = 1; i <= Math.Min(10, maxResults); i++)
            {{
                if (cancellationToken.IsCancellationRequested) break;
                
                results.Add(new SearchResult
                {{
                    Title = $""Ergebnis {{i}}: {{searchTerm}}"",
                    Description = $""Beschreibung für Ergebnis {{i}}"",
                    SourceName = Name,
                    ConnectorId = Id,
                    OriginalReference = $""ref-{{i}}"",
                    RelevanceScore = 100 - (i * 5),
                    Metadata = new Dictionary<string, object>
                    {{
                        [""Author""] = ""Beispiel-Autor"",
                        [""CreatedDate""] = DateTime.Now.ToString(""yyyy-MM-dd""),
                        [""Category""] = ""Beispiel""
                    }}
                }});
            }}

            await Task.Delay(100, cancellationToken);
            return results;
        }}

        // Konfiguriert die Detailansicht für Suchergebnisse
        public override DetailViewConfiguration GetDetailViewConfiguration(SearchResult result)
        {{
            return new DetailViewConfiguration
            {{
                ViewType = DetailViewType.Default,
                
                // Welche Metadaten-Eigenschaften angezeigt werden
                DisplayProperties = new List<string> {{ ""Author"", ""CreatedDate"", ""Category"" }},
                
                // Verfügbare Aktionen
                Actions = new List<ResultAction>
                {{
                    new ResultAction {{ Id = ""open"", Name = ""Öffnen"", Icon = ""??"" }},
                    new ResultAction {{ Id = ""copy"", Name = ""Kopieren"", Icon = ""??"" }}
                }}
            }};
        }}

        // Führt eine Aktion auf einem Suchergebnis aus
        public override Task<bool> ExecuteActionAsync(SearchResult result, string actionId)
        {{
            switch (actionId)
            {{
                case ""open"":
                    // TODO: Öffnen-Logik implementieren
                    Log($""Öffne: {{result.OriginalReference}}"");
                    return Task.FromResult(true);
                    
                case ""copy"":
                    // In Zwischenablage kopieren
                    Clipboard.SetText(result.OriginalReference);
                    return Task.FromResult(true);
                    
                default:
                    return Task.FromResult(false);
            }}
        }}
    }}
}}
";
    }
}