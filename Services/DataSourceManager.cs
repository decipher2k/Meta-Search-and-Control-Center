//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using MSCC.Connectors;
using MSCC.Models;
using MSCC.Scripting;

namespace MSCC.Services;

/// <summary>
/// Verwaltet Datenquellen, Gruppen und Konnektoren.
/// </summary>
public class DataSourceManager
{
    private readonly Dictionary<string, IDataSourceConnector> _connectorInstances = new();

    /// <summary>
    /// Registriert einen Konnektor-Typ.
    /// </summary>
    public void RegisterConnector(IDataSourceConnector connector)
    {
        GlobalState.RegisterConnector(connector);
    }

    /// <summary>
    /// Erstellt eine neue Datenquellen-Gruppe.
    /// </summary>
    public DataSourceGroup CreateGroup(string name, string description, string color = "#3498db")
    {
        var group = new DataSourceGroup
        {
            Name = name,
            Description = description,
            Color = color
        };

        GlobalState.AddGroup(group);
        _ = SaveAsync();
        return group;
    }

    /// <summary>
    /// Aktualisiert eine bestehende Gruppe.
    /// </summary>
    public bool UpdateGroup(string groupId, string? name = null, string? description = null, string? color = null)
    {
        var group = GlobalState.Groups.FirstOrDefault(g => g.Id == groupId);
        if (group == null)
            return false;

        if (name != null)
            group.Name = name;
        if (description != null)
            group.Description = description;
        if (color != null)
            group.Color = color;

        _ = SaveAsync();
        return true;
    }

    /// <summary>
    /// Holt eine Gruppe nach ID.
    /// </summary>
    public DataSourceGroup? GetGroup(string groupId)
    {
        return GlobalState.Groups.FirstOrDefault(g => g.Id == groupId);
    }

    /// <summary>
    /// Holt alle Gruppen.
    /// </summary>
    public IEnumerable<DataSourceGroup> GetAllGroups()
    {
        return GlobalState.Groups.ToList();
    }

    /// <summary>
    /// Erstellt eine neue Datenquelle mit dem angegebenen Konnektor.
    /// </summary>
    public async Task<DataSource?> CreateDataSourceAsync(
        string name,
        string connectorId,
        Dictionary<string, string> configuration,
        string? groupId = null)
    {
        if (!GlobalState.Connectors.TryGetValue(connectorId, out var connectorTemplate))
        {
            return null;
        }

        // Erstelle eine neue Konnektor-Instanz
        var connectorInstance = CreateConnectorInstance(connectorTemplate);
        if (connectorInstance == null)
        {
            return null;
        }

        // Initialisiere den Konnektor
        var success = await connectorInstance.InitializeAsync(configuration);
        if (!success)
        {
            connectorInstance.Dispose();
            return null;
        }

        var dataSource = new DataSource
        {
            Name = name,
            ConnectorId = connectorId,
            Configuration = configuration,
            GroupId = groupId,
            Description = connectorTemplate.Description
        };

        // Speichere die Instanz
        _connectorInstances[dataSource.Id] = connectorInstance;
        GlobalState.AddDataSource(dataSource);

        await SaveAsync();
        return dataSource;
    }

    /// <summary>
    /// Holt die Konnektor-Instanz für eine Datenquelle.
    /// </summary>
    public IDataSourceConnector? GetConnectorInstance(string dataSourceId)
    {
        return _connectorInstances.TryGetValue(dataSourceId, out var connector) ? connector : null;
    }

    /// <summary>
    /// Holt alle verfügbaren Konnektor-Typen.
    /// </summary>
    public IEnumerable<IDataSourceConnector> GetAvailableConnectors()
    {
        var connectors = GlobalState.Connectors.Values.ToList();
        System.Diagnostics.Debug.WriteLine($"DataSourceManager.GetAvailableConnectors: Found {connectors.Count} connectors:");
        foreach (var c in connectors)
        {
            System.Diagnostics.Debug.WriteLine($"  - {c.Id}: {c.Name} ({c.GetType().Name})");
        }
        return connectors;
    }

    /// <summary>
    /// Holt alle Datenquellen einer Gruppe.
    /// </summary>
    public IEnumerable<DataSource> GetDataSourcesByGroup(string groupId)
    {
        return GlobalState.GetDataSourcesByGroup(groupId);
    }

    /// <summary>
    /// Holt eine Datenquelle nach ID.
    /// </summary>
    public DataSource? GetDataSource(string dataSourceId)
    {
        return GlobalState.DataSources.FirstOrDefault(ds => ds.Id == dataSourceId);
    }

    /// <summary>
    /// Holt alle Datenquellen.
    /// </summary>
    public IEnumerable<DataSource> GetAllDataSources()
    {
        return GlobalState.DataSources.ToList();
    }

    /// <summary>
    /// Entfernt eine Datenquelle.
    /// </summary>
    public void RemoveDataSource(string dataSourceId)
    {
        if (_connectorInstances.TryGetValue(dataSourceId, out var connector))
        {
            connector.Dispose();
            _connectorInstances.Remove(dataSourceId);
        }

        var dataSource = GlobalState.DataSources.FirstOrDefault(ds => ds.Id == dataSourceId);
        if (dataSource != null)
        {
            GlobalState.DataSources.Remove(dataSource);
        }

        _ = SaveAsync();
    }

    /// <summary>
    /// Entfernt eine Gruppe und optional alle zugehörigen Datenquellen.
    /// </summary>
    public void RemoveGroup(string groupId, bool removeDataSources = false)
    {
        if (removeDataSources)
        {
            var dataSources = GlobalState.GetDataSourcesByGroup(groupId).ToList();
            foreach (var ds in dataSources)
            {
                RemoveDataSource(ds.Id);
            }
        }
        else
        {
            // Setze GroupId auf null für alle Datenquellen in dieser Gruppe
            foreach (var ds in GlobalState.DataSources.Where(d => d.GroupId == groupId))
            {
                ds.GroupId = null;
            }
        }

        var group = GlobalState.Groups.FirstOrDefault(g => g.Id == groupId);
        if (group != null)
        {
            GlobalState.Groups.Remove(group);
        }

        _ = SaveAsync();
    }

    /// <summary>
    /// Registriert die Standard-Konnektoren.
    /// </summary>
    public void RegisterDefaultConnectors()
    {
        RegisterConnector(new FileSystemConnector());
        RegisterConnector(new MockDatabaseConnector());
        RegisterConnector(new MicrosoftGraphConnector());
        RegisterConnector(new DuckDuckGoConnector());
        RegisterConnector(new SqlDatabaseConnector());
        RegisterConnector(new OpenAiConnector());
    }

    /// <summary>
    /// Lädt gespeicherte Datenquellen und initialisiert ihre Konnektoren.
    /// </summary>
    public async Task LoadSavedDataSourcesAsync()
    {
        await GlobalState.LoadStateAsync();

        // Initialisiere Konnektoren für alle geladenen Datenquellen
        foreach (var dataSource in GlobalState.DataSources.ToList())
        {
            if (!GlobalState.Connectors.TryGetValue(dataSource.ConnectorId, out var connectorTemplate))
            {
                // Konnektor nicht mehr verfügbar - überspringen
                continue;
            }

            var connectorInstance = CreateConnectorInstance(connectorTemplate);
            if (connectorInstance != null)
            {
                var success = await connectorInstance.InitializeAsync(dataSource.Configuration);
                if (success)
                {
                    _connectorInstances[dataSource.Id] = connectorInstance;
                }
                else
                {
                    connectorInstance.Dispose();
                    dataSource.IsEnabled = false;
                }
            }
        }
    }

    /// <summary>
    /// Speichert den aktuellen Zustand.
    /// </summary>
    public async Task SaveAsync()
    {
        await GlobalState.SaveStateAsync();
    }

    private static IDataSourceConnector? CreateConnectorInstance(IDataSourceConnector template)
    {
        // Bekannte Konnektoren
        if (template is FileSystemConnector)
            return new FileSystemConnector();
        
        if (template is MockDatabaseConnector)
            return new MockDatabaseConnector();
        
        if (template is MicrosoftGraphConnector)
            return new MicrosoftGraphConnector();
        
        if (template is DuckDuckGoConnector)
            return new DuckDuckGoConnector();
        
        if (template is SqlDatabaseConnector)
            return new SqlDatabaseConnector();
        
        if (template is OpenAiConnector)
            return new OpenAiConnector();
        
        // Gescriptete Konnektoren - versuche neue Instanz über Reflection zu erstellen
        if (template is ScriptedConnectorBase)
        {
            try
            {
                var type = template.GetType();
                var instance = Activator.CreateInstance(type) as IDataSourceConnector;
                return instance;
            }
            catch
            {
                // Falls Instanziierung fehlschlägt, verwende das Template direkt
                // (nicht ideal, aber besser als nichts)
                return null;
            }
        }

        // Fallback: Versuche generische Instanziierung
        try
        {
            var type = template.GetType();
            if (type.GetConstructor(Type.EmptyTypes) != null)
            {
                return Activator.CreateInstance(type) as IDataSourceConnector;
            }
        }
        catch
        {
            // Ignorieren
        }

        return null;
    }

    /// <summary>
    /// Aktualisiert eine bestehende Datenquelle.
    /// </summary>
    public async Task<bool> UpdateDataSourceAsync(
        string dataSourceId,
        string? name = null,
        Dictionary<string, string>? configuration = null,
        string? groupId = null,
        bool? isEnabled = null)
    {
        var dataSource = GlobalState.DataSources.FirstOrDefault(ds => ds.Id == dataSourceId);
        if (dataSource == null)
            return false;

        if (name != null)
            dataSource.Name = name;
        
        if (groupId != null)
            dataSource.GroupId = groupId == string.Empty ? null : groupId;

        if (isEnabled.HasValue)
            dataSource.IsEnabled = isEnabled.Value;

        // Wenn Konfiguration geändert wurde, Konnektor neu initialisieren
        if (configuration != null)
        {
            dataSource.Configuration = configuration;

            if (_connectorInstances.TryGetValue(dataSourceId, out var existingConnector))
            {
                existingConnector.Dispose();
                _connectorInstances.Remove(dataSourceId);
            }

            if (GlobalState.Connectors.TryGetValue(dataSource.ConnectorId, out var connectorTemplate))
            {
                var newConnector = CreateConnectorInstance(connectorTemplate);
                if (newConnector != null)
                {
                    var success = await newConnector.InitializeAsync(configuration);
                    if (success)
                    {
                        _connectorInstances[dataSourceId] = newConnector;
                    }
                    else
                    {
                        newConnector.Dispose();
                        return false;
                    }
                }
            }
        }

        await SaveAsync();
        return true;
    }

    /// <summary>
    /// Verschiebt eine Datenquelle in eine andere Gruppe.
    /// </summary>
    public bool MoveDataSourceToGroup(string dataSourceId, string? groupId)
    {
        var dataSource = GlobalState.DataSources.FirstOrDefault(ds => ds.Id == dataSourceId);
        if (dataSource == null)
            return false;

        dataSource.GroupId = groupId;
        _ = SaveAsync();
        return true;
    }

    /// <summary>
    /// Gibt alle Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        foreach (var connector in _connectorInstances.Values)
        {
            connector.Dispose();
        }
        _connectorInstances.Clear();
    }
}
