//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using MSCC.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MSCC.Connectors;

/// <summary>
/// Mock-Datenbank-Konnektor für Demonstrationszwecke.
/// Simuliert eine Datenbanksuche mit Beispieldaten.
/// </summary>
public class MockDatabaseConnector : IDataSourceConnector, IDisposable
{
    private string _connectionString = string.Empty;
    private string _tableName = string.Empty;
    private List<Dictionary<string, object>> _mockData = new();

    public string Id => "mock-database-connector";
    public string Name => "Datenbank (Mock)";
    public string Description => "Simuliert eine Datenbankabfrage für Demonstrationszwecke.";
    public string Version => "1.0.0";

    public IEnumerable<ConnectorParameter> ConfigurationParameters => new[]
    {
        new ConnectorParameter
        {
            Name = "ConnectionString",
            DisplayName = "Verbindungsstring",
            Description = "Datenbankverbindungsstring.",
            ParameterType = "string",
            IsRequired = true
        },
        new ConnectorParameter
        {
            Name = "TableName",
            DisplayName = "Tabellenname",
            Description = "Name der zu durchsuchenden Tabelle.",
            ParameterType = "string",
            IsRequired = true
        }
    };

    public Task<bool> InitializeAsync(Dictionary<string, string> configuration)
    {
        if (!configuration.TryGetValue("ConnectionString", out var connStr) || string.IsNullOrEmpty(connStr))
        {
            return Task.FromResult(false);
        }

        if (!configuration.TryGetValue("TableName", out var tableName) || string.IsNullOrEmpty(tableName))
        {
            return Task.FromResult(false);
        }

        _connectionString = connStr;
        _tableName = tableName;

        // Generiere Mock-Daten
        GenerateMockData();

        return Task.FromResult(true);
    }

    private void GenerateMockData()
    {
        _mockData = new List<Dictionary<string, object>>
        {
            new() { ["Id"] = 1, ["Name"] = "Projektbericht Q1 2024", ["Category"] = "Berichte", ["Author"] = "Max Mustermann" },
            new() { ["Id"] = 2, ["Name"] = "Kundendatenanalyse", ["Category"] = "Analysen", ["Author"] = "Anna Schmidt" },
            new() { ["Id"] = 3, ["Name"] = "Technische Dokumentation API", ["Category"] = "Dokumentation", ["Author"] = "Peter Weber" },
            new() { ["Id"] = 4, ["Name"] = "Marketingstrategie 2024", ["Category"] = "Strategien", ["Author"] = "Lisa Müller" },
            new() { ["Id"] = 5, ["Name"] = "Budgetplanung Entwicklung", ["Category"] = "Finanzen", ["Author"] = "Thomas Fischer" },
            new() { ["Id"] = 6, ["Name"] = "Serverarchitektur Diagramm", ["Category"] = "Technisch", ["Author"] = "Julia Klein" },
            new() { ["Id"] = 7, ["Name"] = "Mitarbeiterschulung Datenschutz", ["Category"] = "HR", ["Author"] = "Michael Braun" },
            new() { ["Id"] = 8, ["Name"] = "Produktspezifikation v2.0", ["Category"] = "Produkt", ["Author"] = "Sandra Hoffmann" },
            new() { ["Id"] = 9, ["Name"] = "Kundenumfrage Ergebnisse", ["Category"] = "Marketing", ["Author"] = "Daniel Wagner" },
            new() { ["Id"] = 10, ["Name"] = "Sicherheitsaudit Bericht", ["Category"] = "Sicherheit", ["Author"] = "Christine Becker" }
        };
    }

    public Task<IEnumerable<SearchResult>> SearchAsync(
        string searchTerm,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SearchResult>();

        foreach (var record in _mockData)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (results.Count >= maxResults)
                break;

            var name = record["Name"]?.ToString() ?? string.Empty;
            var category = record["Category"]?.ToString() ?? string.Empty;
            var author = record["Author"]?.ToString() ?? string.Empty;

            if (name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                category.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                author.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new SearchResult
                {
                    Id = record["Id"]?.ToString() ?? Guid.NewGuid().ToString(),
                    Title = name,
                    Description = $"Kategorie: {category} | Autor: {author}",
                    SourceName = $"Datenbank: {_tableName}",
                    ConnectorId = Id,
                    OriginalReference = $"db://{_tableName}/{record["Id"]}",
                    RelevanceScore = CalculateRelevance(searchTerm, name, category, author),
                    Metadata = new Dictionary<string, object>(record)
                });
            }
        }

        return Task.FromResult<IEnumerable<SearchResult>>(results);
    }

    private static int CalculateRelevance(string searchTerm, string name, string category, string author)
    {
        if (name.Equals(searchTerm, StringComparison.OrdinalIgnoreCase))
            return 100;
        if (name.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
            return 90;
        if (name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            return 70;
        if (category.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            return 50;
        if (author.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            return 40;
        return 30;
    }

    public Task<bool> TestConnectionAsync()
    {
        return Task.FromResult(!string.IsNullOrEmpty(_connectionString));
    }

    public void Dispose()
    {
        _mockData.Clear();
        GC.SuppressFinalize(this);
    }

    public DetailViewConfiguration GetDetailViewConfiguration(SearchResult result)
    {
        return new DetailViewConfiguration
        {
            ViewType = DetailViewType.Table,
            TableColumns = new List<TableColumnDefinition>
            {
                new() { PropertyName = "Id", Header = "ID", Width = "50" },
                new() { PropertyName = "Name", Header = "Name", Width = "*" },
                new() { PropertyName = "Category", Header = "Kategorie", Width = "120" },
                new() { PropertyName = "Author", Header = "Autor", Width = "150" }
            },
            Actions = new List<ResultAction>
            {
                new() { Id = "view-details", Name = "Details anzeigen", Icon = "??", Description = "Vollständige Datensatz-Details" },
                new() { Id = "export", Name = "Exportieren", Icon = "??", Description = "Datensatz als JSON exportieren" },
                new() { Id = "show-chart", Name = "Als Chart", Icon = "??", Description = "Daten als Chart visualisieren" }
            },
            ChartConfig = new ChartDefinition
            {
                ChartType = "Bar",
                CategoryProperty = "Category",
                ValueProperty = "Id",
                Title = "Datensatz-Verteilung"
            }
        };
    }

    public FrameworkElement? CreateCustomDetailView(SearchResult result)
    {
        // Erstelle eine benutzerdefinierte Detailansicht als Tabelle
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var row = 0;
        foreach (var kvp in result.Metadata)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var labelBlock = new TextBlock
            {
                Text = $"{kvp.Key}:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(4),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C3E50"))
            };
            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            var valueBlock = new TextBlock
            {
                Text = kvp.Value?.ToString() ?? "(leer)",
                Margin = new Thickness(4),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(valueBlock, row);
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(valueBlock);

            row++;
        }

        var border = new Border
        {
            Child = grid,
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Margin = new Thickness(8),
            Background = Brushes.White
        };

        return border;
    }

    public async Task<bool> ExecuteActionAsync(SearchResult result, string actionId)
    {
        return await Task.Run(() =>
        {
            try
            {
                switch (actionId)
                {
                    case "view-details":
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            var details = string.Join("\n", result.Metadata.Select(m => $"{m.Key}: {m.Value}"));
                            MessageBox.Show(details, $"Details: {result.Title}", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                        return true;

                    case "export":
                        var json = System.Text.Json.JsonSerializer.Serialize(result.Metadata, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            Clipboard.SetText(json);
                            MessageBox.Show("JSON wurde in die Zwischenablage kopiert.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                        return true;

                    case "show-chart":
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show("Chart-Ansicht wird in einer späteren Version implementiert.", "Chart", MessageBoxButton.OK, MessageBoxImage.Information);
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
