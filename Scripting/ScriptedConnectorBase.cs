//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Windows;
using MSCC.Connectors;
using MSCC.Models;

namespace MSCC.Scripting;

public abstract class ScriptedConnectorBase : IDataSourceConnector
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual string Version => "1.0.0";
    public virtual IEnumerable<ConnectorParameter> ConfigurationParameters => Enumerable.Empty<ConnectorParameter>();

    protected Dictionary<string, string> Configuration { get; private set; } = new();

    public virtual Task<bool> InitializeAsync(Dictionary<string, string> configuration)
    {
        Configuration = configuration;
        return Task.FromResult(true);
    }

    public abstract Task<IEnumerable<SearchResult>> SearchAsync(
        string searchTerm,
        int maxResults = 100,
        CancellationToken cancellationToken = default);

    public virtual Task<bool> TestConnectionAsync() => Task.FromResult(true);

    public virtual DetailViewConfiguration GetDetailViewConfiguration(SearchResult result)
    {
        return new DetailViewConfiguration
        {
            ViewType = DetailViewType.Default,
            DisplayProperties = result.Metadata.Keys.ToList()
        };
    }

    public virtual FrameworkElement? CreateCustomDetailView(SearchResult result) => null;

    public virtual Task<bool> ExecuteActionAsync(SearchResult result, string actionId)
        => Task.FromResult(false);

    public virtual void Dispose() => GC.SuppressFinalize(this);

    protected SearchResult CreateResult(string title, string description, string reference = "")
    {
        return new SearchResult
        {
            Title = title,
            Description = description,
            OriginalReference = reference,
            SourceName = Name,
            ConnectorId = Id
        };
    }

    protected string GetConfig(string key, string defaultValue = "")
        => Configuration.GetValueOrDefault(key, defaultValue);

    protected int GetConfigInt(string key, int defaultValue = 0)
    {
        var value = GetConfig(key);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    protected bool GetConfigBool(string key, bool defaultValue = false)
    {
        var value = GetConfig(key);
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    protected void Log(string message)
        => System.Diagnostics.Debug.WriteLine($"[{Name}] {message}");

    protected void LogError(string message, Exception? ex = null)
    {
        System.Diagnostics.Debug.WriteLine($"[{Name}] ERROR: {message}");
        if (ex != null)
            System.Diagnostics.Debug.WriteLine($"  {ex.Message}");
    }
}