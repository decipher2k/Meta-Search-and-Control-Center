# MSCC - Meta Search Command Center

Eine erweiterbare Metasuchmaschine für Windows, die mehrere Datenquellen gleichzeitig durchsuchen kann. Die Anwendung unterstützt sowohl eingebaute Konnektoren als auch benutzerdefinierte Plugins.

## Inhaltsverzeichnis

- [Funktionsübersicht](#funktionsübersicht)
- [Installation](#installation)
- [Benutzeroberfläche](#benutzeroberfläche)
- [Konnektoren und Datenquellen](#konnektoren-und-datenquellen)
- [Plugin-Entwicklung](#plugin-entwicklung)
  - [Option 1: Script-basierte Plugins](#option-1-script-basierte-plugins)
  - [Option 2: Kompilierte Plugins (Visual Studio)](#option-2-kompilierte-plugins-visual-studio)
- [API-Referenz](#api-referenz)
- [Beispiele](#beispiele)

---

## Funktionsübersicht

### Kernfunktionen

- **Multi-Source-Suche**: Durchsuchen Sie mehrere Datenquellen gleichzeitig
- **Datenquellen-Gruppen**: Organisieren Sie Ihre Datenquellen in logischen Gruppen
- **Label-System**: Versehen Sie Suchergebnisse mit Keywords für spätere Referenz
- **Gespeicherte Abfragen**: Speichern und laden Sie häufig verwendete Suchen
- **Erweiterbar**: Fügen Sie eigene Konnektoren per Script oder als kompiliertes Plugin hinzu

### Eingebaute Konnektoren

| Konnektor | Beschreibung |
|-----------|--------------|
| **Dateisystem** | Durchsucht lokale Dateien und Ordner nach Dateinamen |
| **Mock-Datenbank** | Demo-Konnektor für Testzwecke |

---

## Installation

### Voraussetzungen

- Windows 10/11
- .NET 10 Runtime

### Build aus Quellcode

```bash
git clone <repository-url>
cd MSCC
dotnet build
dotnet run
```

---

## Benutzeroberfläche

### Hauptfenster

Das Hauptfenster ist in drei Bereiche unterteilt:

1. **Linke Seitenleiste**: Gruppen und Datenquellen verwalten
2. **Mitte**: Suchergebnisse und Detailansicht
3. **Rechte Seitenleiste**: Labels und gespeicherte Abfragen

### Datenquellen verwalten

1. Klicken Sie auf **+** bei "Datenquellen"
2. Wählen Sie einen Konnektor-Typ
3. Geben Sie einen Namen und die Konfiguration ein
4. Klicken Sie auf **Speichern**

### Suche durchführen

1. Aktivieren Sie die gewünschten Datenquellen (Checkbox)
2. Geben Sie einen Suchbegriff ein
3. Drücken Sie **Enter** oder klicken Sie auf **Suchen**

---

## Konnektoren und Datenquellen

### Konzept

- **Konnektor**: Ein Plugin, das eine bestimmte Datenquelle anbinden kann (z.B. Dateisystem, REST-API, Datenbank)
- **Datenquelle**: Eine konkrete Instanz eines Konnektors mit spezifischer Konfiguration

Beispiel: Der Dateisystem-Konnektor kann mehrere Datenquellen erstellen - eine für "Dokumente", eine für "Downloads", etc.

---

## Plugin-Entwicklung

Es gibt zwei Möglichkeiten, eigene Konnektoren zu erstellen:

### Option 1: Script-basierte Plugins

Script-Plugins werden zur Laufzeit kompiliert und erfordern keinen separaten Build-Prozess. Ideal für schnelle Prototypen und einfache Konnektoren.

#### Script erstellen

1. Öffnen Sie **Plugins ? Script Manager**
2. Geben Sie einen Namen ein und klicken Sie auf **+ Neues Script**
3. Bearbeiten Sie das generierte Template
4. Klicken Sie auf **Kompilieren**

#### Script-Template

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MSCC.Connectors;
using MSCC.Models;
using MSCC.Scripting;

namespace MSCC.Scripts
{
    public class MeinKonnektor : ScriptedConnectorBase
    {
        // Eindeutige ID für diesen Konnektor
        public override string Id => "mein-konnektor";
        
        // Anzeigename in der UI
        public override string Name => "Mein Konnektor";
        
        // Beschreibung
        public override string Description => "Beschreibung meines Konnektors";
        
        // Version
        public override string Version => "1.0.0";

        // Konfigurationsparameter definieren
        public override IEnumerable<ConnectorParameter> ConfigurationParameters => new[]
        {
            new ConnectorParameter
            {
                Name = "ApiUrl",
                DisplayName = "API URL",
                Description = "Die URL der API",
                ParameterType = "string",
                IsRequired = true
            },
            new ConnectorParameter
            {
                Name = "ApiKey",
                DisplayName = "API Key",
                Description = "Optionaler API-Schlüssel",
                ParameterType = "string",
                IsRequired = false
            }
        };

        private string _apiUrl = string.Empty;
        private string _apiKey = string.Empty;

        // Initialisierung mit Konfiguration
        public override Task<bool> InitializeAsync(Dictionary<string, string> configuration)
        {
            if (!configuration.TryGetValue("ApiUrl", out var url) || string.IsNullOrEmpty(url))
                return Task.FromResult(false);

            _apiUrl = url;
            configuration.TryGetValue("ApiKey", out var key);
            _apiKey = key ?? string.Empty;

            return Task.FromResult(true);
        }

        // Verbindungstest
        public override Task<bool> TestConnectionAsync()
        {
            return Task.FromResult(!string.IsNullOrEmpty(_apiUrl));
        }

        // Suchlogik implementieren
        public override async Task<IEnumerable<SearchResult>> SearchAsync(
            string searchTerm,
            int maxResults = 100,
            CancellationToken cancellationToken = default)
        {
            var results = new List<SearchResult>();

            // Hier Ihre Suchlogik implementieren
            // Beispiel: API aufrufen, Datenbank abfragen, etc.

            // Ergebnis hinzufügen
            results.Add(new SearchResult
            {
                Title = "Beispiel-Ergebnis",
                Description = $"Gefunden für: {searchTerm}",
                SourceName = Name,
                ConnectorId = Id,
                OriginalReference = "ref-123",
                RelevanceScore = 100,
                Metadata = new Dictionary<string, object>
                {
                    ["Eigenschaft1"] = "Wert1",
                    ["Eigenschaft2"] = 42
                }
            });

            return results;
        }
    }
}
```

#### Script-Speicherort

Scripts werden gespeichert unter:
```
%APPDATA%\MSCC\Scripts\
```

Jedes Script besteht aus zwei Dateien:
- `ScriptName_<id>.cs` - Der Quellcode
- `ScriptName_<id>.cs.meta` - Metadaten (Name, Version, etc.)

---

### Option 2: Kompilierte Plugins (Visual Studio)

Für komplexere Konnektoren empfiehlt sich die Entwicklung als kompiliertes Plugin.

#### Projekt erstellen

1. Erstellen Sie ein neues **Class Library**-Projekt in Visual Studio
2. Ziel-Framework: **.NET 10**
3. Fügen Sie eine Projekt-Referenz zu `MSCC.csproj` hinzu

#### IDataSourceConnector implementieren

```csharp
using System.Windows;
using MSCC.Connectors;
using MSCC.Models;

namespace MeinPlugin
{
    public class MeinKonnektor : IDataSourceConnector
    {
        public string Id => "mein-kompilierter-konnektor";
        public string Name => "Mein Kompilierter Konnektor";
        public string Description => "Ein kompilierter Konnektor";
        public string Version => "1.0.0";

        public IEnumerable<ConnectorParameter> ConfigurationParameters => new[]
        {
            new ConnectorParameter
            {
                Name = "ConnectionString",
                DisplayName = "Verbindungszeichenfolge",
                ParameterType = "string",
                IsRequired = true
            }
        };

        private string _connectionString = string.Empty;

        public Task<bool> InitializeAsync(Dictionary<string, string> configuration)
        {
            if (!configuration.TryGetValue("ConnectionString", out var cs))
                return Task.FromResult(false);
            
            _connectionString = cs;
            return Task.FromResult(true);
        }

        public Task<bool> TestConnectionAsync()
        {
            // Verbindungstest implementieren
            return Task.FromResult(!string.IsNullOrEmpty(_connectionString));
        }

        public async Task<IEnumerable<SearchResult>> SearchAsync(
            string searchTerm,
            int maxResults = 100,
            CancellationToken cancellationToken = default)
        {
            var results = new List<SearchResult>();
            
            // Suchlogik hier implementieren
            
            return results;
        }

        public DetailViewConfiguration GetDetailViewConfiguration(SearchResult result)
        {
            return new DetailViewConfiguration
            {
                ViewType = DetailViewType.Default,
                Actions = new List<ResultAction>
                {
                    new() { Id = "open", Name = "Öffnen", Icon = "??" }
                }
            };
        }

        public FrameworkElement? CreateCustomDetailView(SearchResult result)
        {
            return null; // Oder eigene WPF-Ansicht zurückgeben
        }

        public Task<bool> ExecuteActionAsync(SearchResult result, string actionId)
        {
            // Aktionen behandeln
            return Task.FromResult(true);
        }

        public void Dispose()
        {
            // Ressourcen freigeben
        }
    }
}
```

#### Konnektor registrieren

In `DataSourceManager.cs` den Konnektor hinzufügen:

```csharp
public void RegisterDefaultConnectors()
{
    RegisterConnector(new FileSystemConnector());
    RegisterConnector(new MockDatabaseConnector());
    RegisterConnector(new MeinKonnektor()); // Hier hinzufügen
}
```

Und in `CreateConnectorInstance`:

```csharp
private static IDataSourceConnector? CreateConnectorInstance(IDataSourceConnector template)
{
    if (template is FileSystemConnector)
        return new FileSystemConnector();
    
    if (template is MockDatabaseConnector)
        return new MockDatabaseConnector();
    
    if (template is MeinKonnektor)
        return new MeinKonnektor();
    
    // ... rest
}
```

---

## API-Referenz

### IDataSourceConnector

Das Haupt-Interface für alle Konnektoren.

| Methode/Property | Beschreibung |
|------------------|--------------|
| `Id` | Eindeutige Kennung des Konnektors |
| `Name` | Anzeigename |
| `Description` | Beschreibung |
| `Version` | Versionsnummer |
| `ConfigurationParameters` | Liste der Konfigurationsparameter |
| `InitializeAsync()` | Initialisiert den Konnektor mit Konfiguration |
| `TestConnectionAsync()` | Prüft die Verbindung |
| `SearchAsync()` | Führt eine Suche durch |
| `GetDetailViewConfiguration()` | Definiert die Detailansicht |
| `CreateCustomDetailView()` | Erstellt eine benutzerdefinierte WPF-Ansicht |
| `ExecuteActionAsync()` | Führt eine Aktion auf einem Ergebnis aus |

### SearchResult

Repräsentiert ein Suchergebnis.

| Property | Typ | Beschreibung |
|----------|-----|--------------|
| `Title` | string | Titel des Ergebnisses |
| `Description` | string | Beschreibung |
| `SourceName` | string | Name der Datenquelle |
| `ConnectorId` | string | ID des Konnektors |
| `OriginalReference` | string | Referenz zum Original (z.B. Dateipfad, URL) |
| `RelevanceScore` | int | Relevanz (0-100) |
| `Metadata` | Dictionary | Zusätzliche Eigenschaften |

### ConnectorParameter

Definiert einen Konfigurationsparameter.

| Property | Beschreibung |
|----------|--------------|
| `Name` | Technischer Name |
| `DisplayName` | Anzeigename |
| `Description` | Beschreibung/Hilfetext |
| `ParameterType` | Typ: "string", "bool", "int", "path" |
| `IsRequired` | Pflichtfeld? |
| `DefaultValue` | Standardwert |

### DetailViewConfiguration

Konfiguriert die Detailansicht für Suchergebnisse.

| Property | Beschreibung |
|----------|--------------|
| `ViewType` | Art der Ansicht: Default, Table, Media, Chart, Custom |
| `TableColumns` | Spaltendefinitionen für Tabellenansicht |
| `Actions` | Verfügbare Aktionen |
| `ChartConfig` | Konfiguration für Diagramme |

---

## Beispiele

### Web-API Konnektor

Ein Beispiel für einen Konnektor, der eine REST-API abfragt:

```csharp
public override async Task<IEnumerable<SearchResult>> SearchAsync(
    string searchTerm,
    int maxResults = 100,
    CancellationToken cancellationToken = default)
{
    var results = new List<SearchResult>();
    
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    
    var response = await client.GetAsync(
        $"{_apiUrl}/search?q={Uri.EscapeDataString(searchTerm)}&limit={maxResults}",
        cancellationToken);
    
    if (!response.IsSuccessStatusCode)
        return results;
    
    var json = await response.Content.ReadAsStringAsync(cancellationToken);
    var data = JsonSerializer.Deserialize<ApiResponse>(json);
    
    foreach (var item in data?.Items ?? [])
    {
        results.Add(new SearchResult
        {
            Title = item.Title,
            Description = item.Summary,
            SourceName = Name,
            ConnectorId = Id,
            OriginalReference = item.Url,
            RelevanceScore = (int)(item.Score * 100),
            Metadata = new Dictionary<string, object>
            {
                ["Author"] = item.Author,
                ["Date"] = item.CreatedAt
            }
        });
    }
    
    return results;
}
```

### Datenbank-Konnektor

```csharp
public override async Task<IEnumerable<SearchResult>> SearchAsync(
    string searchTerm,
    int maxResults = 100,
    CancellationToken cancellationToken = default)
{
    var results = new List<SearchResult>();
    
    using var connection = new SqlConnection(_connectionString);
    await connection.OpenAsync(cancellationToken);
    
    var command = new SqlCommand(
        "SELECT TOP (@max) Id, Title, Description FROM Documents WHERE Title LIKE @search",
        connection);
    command.Parameters.AddWithValue("@max", maxResults);
    command.Parameters.AddWithValue("@search", $"%{searchTerm}%");
    
    using var reader = await command.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
    {
        results.Add(new SearchResult
        {
            Title = reader.GetString(1),
            Description = reader.GetString(2),
            SourceName = Name,
            ConnectorId = Id,
            OriginalReference = reader.GetInt32(0).ToString(),
            RelevanceScore = 100
        });
    }
    
    return results;
}
```

---

## Tests ausführen

```bash
cd MSCC.Tests
dotnet test
```

Aktuelle Testabdeckung: **163 Tests**

---

## Lizenz

MIT License

---

## Mitwirken

Pull Requests sind willkommen! Bitte erstellen Sie zunächst ein Issue, um größere Änderungen zu besprechen.
