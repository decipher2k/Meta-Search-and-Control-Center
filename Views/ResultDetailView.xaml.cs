//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MSCC.Connectors;
using MSCC.Models;
using MSCC.Services;

namespace MSCC.Views;

public partial class ResultDetailView : UserControl
{
    private SearchResult? _currentResult;
    private IDataSourceConnector? _connector;
    private readonly DataSourceManager _dataSourceManager;

    public ResultDetailView()
    {
        InitializeComponent();
        _dataSourceManager = new DataSourceManager();
    }

    public ResultDetailView(DataSourceManager dataSourceManager)
    {
        InitializeComponent();
        _dataSourceManager = dataSourceManager;
    }

    public void ShowResult(SearchResult result, IDataSourceConnector? connector)
    {
        _currentResult = result;
        _connector = connector;

        TitleBlock.Text = result.Title;
        SourceBlock.Text = $"Quelle: {result.SourceName}";
        DescriptionBlock.Text = result.Description;

        LoadContent(result, connector);
        LoadActions(result, connector);
    }

    private void LoadContent(SearchResult result, IDataSourceConnector? connector)
    {
        ContentArea.Content = null;

        if (connector == null)
        {
            ContentArea.Content = CreateDefaultView(result);
            return;
        }

        var config = connector.GetDetailViewConfiguration(result);

        switch (config.ViewType)
        {
            case DetailViewType.Custom:
                var customView = connector.CreateCustomDetailView(result);
                ContentArea.Content = customView ?? CreateDefaultView(result);
                break;

            case DetailViewType.Table:
                ContentArea.Content = CreateTableView(result, config);
                break;

            case DetailViewType.Media:
                ContentArea.Content = CreateMediaView(result, config) ?? CreateDefaultView(result);
                break;

            case DetailViewType.Chart:
                ContentArea.Content = CreateChartPlaceholder(config);
                break;

            case DetailViewType.Default:
            default:
                ContentArea.Content = CreateDefaultView(result);
                break;
        }
    }

    private static FrameworkElement CreateDefaultView(SearchResult result)
    {
        var stackPanel = new StackPanel();

        foreach (var kvp in result.Metadata.Take(10))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            
            row.Children.Add(new TextBlock
            {
                Text = $"{kvp.Key}: ",
                FontWeight = FontWeights.SemiBold,
                Width = 120
            });
            
            row.Children.Add(new TextBlock
            {
                Text = kvp.Value?.ToString() ?? "(leer)",
                TextWrapping = TextWrapping.Wrap
            });
            
            stackPanel.Children.Add(row);
        }

        return new ScrollViewer
        {
            Content = stackPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 200
        };
    }

    private static FrameworkElement CreateTableView(SearchResult result, DetailViewConfiguration config)
    {
        var grid = new Grid();
        
        foreach (var col in config.TableColumns)
        {
            var colDef = new ColumnDefinition();
            if (col.Width == "*")
                colDef.Width = new GridLength(1, GridUnitType.Star);
            else if (col.Width == "Auto")
                colDef.Width = GridLength.Auto;
            else if (double.TryParse(col.Width, out var width))
                colDef.Width = new GridLength(width);
            
            grid.ColumnDefinitions.Add(colDef);
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int i = 0; i < config.TableColumns.Count; i++)
        {
            var header = new TextBlock
            {
                Text = config.TableColumns[i].Header,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(8, 4, 8, 4),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"))
            };
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, i);
            grid.Children.Add(header);
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int i = 0; i < config.TableColumns.Count; i++)
        {
            var col = config.TableColumns[i];
            var value = result.Metadata.GetValueOrDefault(col.PropertyName);
            
            var formattedValue = value?.ToString() ?? "";
            if (!string.IsNullOrEmpty(col.Format) && value != null)
            {
                try
                {
                    formattedValue = string.Format(col.Format, value);
                }
                catch
                {
                    formattedValue = value.ToString() ?? "";
                }
            }

            var cell = new TextBlock
            {
                Text = formattedValue,
                Padding = new Thickness(8, 4, 8, 4)
            };
            Grid.SetRow(cell, 1);
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        return new Border
        {
            Child = grid,
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0")),
            BorderThickness = new Thickness(1)
        };
    }

    private static FrameworkElement? CreateMediaView(SearchResult result, DetailViewConfiguration config)
    {
        var path = result.OriginalReference;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            return null;

        try
        {
            var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
            
            if (extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp")
            {
                var image = new Image
                {
                    Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(path)),
                    MaxHeight = 300,
                    MaxWidth = 400,
                    Stretch = Stretch.Uniform
                };

                return new Border
                {
                    Child = image,
                    BorderBrush = new SolidColorBrush(Colors.LightGray),
                    BorderThickness = new Thickness(1),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static FrameworkElement CreateChartPlaceholder(DetailViewConfiguration config)
    {
        var placeholder = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5")),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(20),
            Child = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Chart",
                        FontSize = 24,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = config.ChartConfig?.Title ?? "Chart-Ansicht",
                        FontSize = 14,
                        FontWeight = FontWeights.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 8, 0, 4)
                    },
                    new TextBlock
                    {
                        Text = $"Typ: {config.ChartConfig?.ChartType ?? "Bar"}",
                        Foreground = new SolidColorBrush(Colors.Gray),
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            }
        };

        return placeholder;
    }

    private void LoadActions(SearchResult result, IDataSourceConnector? connector)
    {
        ActionsPanel.Children.Clear();

        if (connector == null)
            return;

        var config = connector.GetDetailViewConfiguration(result);

        foreach (var action in config.Actions)
        {
            var button = new Button
            {
                Content = $"{action.Icon} {action.Name}",
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 0),
                Tag = action.Id,
                ToolTip = action.Description
            };

            button.Click += async (s, e) =>
            {
                if (_currentResult != null && _connector != null)
                {
                    var success = await _connector.ExecuteActionAsync(_currentResult, action.Id);
                    if (!success)
                    {
                        MessageBox.Show($"Aktion '{action.Name}' konnte nicht ausgefuehrt werden.", 
                            "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            };

            ActionsPanel.Children.Add(button);
        }
    }
}
