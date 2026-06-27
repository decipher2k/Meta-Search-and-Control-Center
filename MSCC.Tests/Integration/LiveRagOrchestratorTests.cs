using MSCC.Connectors;
using MSCC.Models;
using MSCC.Services;
using System.Windows;

namespace MSCC.Tests.Integration;

[TestFixture]
[NonParallelizable]
public class LiveRagOrchestratorTests
{
    private string _tempDirectory = string.Empty;
    private DataSourceManager _dataSourceManager = null!;
    private List<DataSource> _originalDataSources = new();
    private List<DataSourceGroup> _originalGroups = new();
    private List<SearchQuery> _originalQueries = new();
    private Dictionary<string, IDataSourceConnector> _originalConnectors = new();

    [SetUp]
    public async Task SetUp()
    {
        _originalDataSources = GlobalState.DataSources.ToList();
        _originalGroups = GlobalState.Groups.ToList();
        _originalQueries = GlobalState.Queries.ToList();
        _originalConnectors = GlobalState.Connectors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        GlobalState.DataSources.Clear();
        GlobalState.Groups.Clear();
        GlobalState.Connectors.Clear();
        GlobalState.Queries.Clear();

        _tempDirectory = Path.Combine(Path.GetTempPath(), "mscc-live-rag-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirectory, "neptune-plan.txt"),
            "Neptune migration requires a staged rollout and a budget review.");

        _dataSourceManager = new DataSourceManager();
        _dataSourceManager.RegisterDefaultConnectors();

        await _dataSourceManager.CreateDataSourceAsync(
            "Live RAG Test Files",
            "filesystem-connector",
            new Dictionary<string, string>
            {
                ["BasePath"] = _tempDirectory,
                ["SearchPattern"] = "*.txt",
                ["IncludeSubdirectories"] = "true"
            });
    }

    [TearDown]
    public void TearDown()
    {
        _dataSourceManager.Dispose();

        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);

        GlobalState.DataSources.Clear();
        foreach (var item in _originalDataSources)
            GlobalState.DataSources.Add(item);

        GlobalState.Groups.Clear();
        foreach (var item in _originalGroups)
            GlobalState.Groups.Add(item);

        GlobalState.Connectors.Clear();
        foreach (var item in _originalConnectors)
            GlobalState.Connectors[item.Key] = item.Value;

        GlobalState.Queries.Clear();
        foreach (var item in _originalQueries)
            GlobalState.Queries.Add(item);
    }

    [Test]
    public void ValidateOperations_RejectsDangerousStructuredQuery()
    {
        var profile = new LiveRagSourceProfile
        {
            DataSourceId = "source-1",
            SourceName = "SQL",
            ConnectorId = "sql-database-connector",
            SupportsNativeLiveRag = true,
            SupportedOperations = [LiveRagOperationType.StructuredQuery],
            MaxResults = 10
        };

        var operation = new LiveRagOperation
        {
            DataSourceId = "source-1",
            Type = LiveRagOperationType.StructuredQuery,
            Query = "DROP TABLE cities",
            Limit = 10
        };

        var result = LiveRagOrchestrator.ValidateOperations(
            "drop cities",
            [profile],
            [operation],
            maxOperationsPerSource: 3,
            maxCandidateItems: 20);

        Assert.That(result.AcceptedOperations, Is.Empty);
        Assert.That(result.RejectedOperations, Has.Count.EqualTo(1));
        Assert.That(result.Traces.Single().Accepted, Is.False);
    }

    [Test]
    public void ValidateOperations_ClampsLimitAndAcceptsSupportedOperation()
    {
        var profile = new LiveRagSourceProfile
        {
            DataSourceId = "source-1",
            SourceName = "Files",
            ConnectorId = "filesystem-connector",
            SupportsNativeLiveRag = true,
            SupportedOperations = [LiveRagOperationType.ContentScan],
            MaxResults = 10
        };

        var operation = new LiveRagOperation
        {
            DataSourceId = "source-1",
            Type = LiveRagOperationType.ContentScan,
            Query = "Neptune rollout",
            Limit = 500
        };

        var result = LiveRagOrchestrator.ValidateOperations(
            "Neptune rollout",
            [profile],
            [operation],
            maxOperationsPerSource: 3,
            maxCandidateItems: 25);

        Assert.That(result.AcceptedOperations, Has.Count.EqualTo(1));
        Assert.That(result.AcceptedOperations[0].Limit, Is.EqualTo(25));
        Assert.That(result.RejectedOperations, Is.Empty);
    }

    [Test]
    public async Task GetLiveRagContextAsync_WhenAiPlanningDisabled_UsesProfileAwareLiveFallback()
    {
        var source = GlobalState.DataSources.Single();
        var orchestrator = new LiveRagOrchestrator(_dataSourceManager);

        var context = await orchestrator.GetLiveRagContextAsync(
            "Welche Notizen gibt es zur Neptune Migration?",
            [source.Id],
            maxResultsPerOperation: 10,
            maxContextItemsPerSource: 5,
            maxContextItemsTotal: 5,
            maxCharactersPerItem: 1000,
            includeMetadata: true,
            useAiPlanning: false);

        Assert.That(context.Success, Is.True);
        Assert.That(context.IsDegradedFallback, Is.False);
        Assert.That(context.PlanOperations, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(context.PlanOperations[0].Type, Is.EqualTo(LiveRagOperationType.ContentScan));
        Assert.That(context.ExecutionTrace.Any(trace => trace.Accepted), Is.True);
        Assert.That(context.ContextItems.Any(item => item.Content.Contains("Neptune", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public async Task GetLiveRagContextAsync_ProfileAwareSqlFallback_MapsGermanTopCitiesQuestion()
    {
        var source = new DataSource
        {
            Id = Guid.NewGuid().ToString(),
            Name = "SQL Profile Test",
            ConnectorId = ProfileOnlySqlConnector.ConnectorId,
            IsEnabled = true
        };
        GlobalState.DataSources.Add(source);
        AddConnectorInstance(source.Id, new ProfileOnlySqlConnector());

        var orchestrator = new LiveRagOrchestrator(_dataSourceManager);

        var context = await orchestrator.GetLiveRagContextAsync(
            "Liste die Top-Staedte nach Einwohnerzahl auf.",
            [source.Id],
            maxResultsPerOperation: 10,
            maxContextItemsPerSource: 5,
            maxContextItemsTotal: 5,
            maxCharactersPerItem: 1000,
            includeMetadata: true,
            useAiPlanning: false);

        Assert.That(context.Success, Is.True);
        Assert.That(context.IsDegradedFallback, Is.False);
        Assert.That(context.PlanOperations.Any(operation =>
            operation.Type == LiveRagOperationType.TopN &&
            string.Equals(operation.Target, "cities", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(operation.SortField, "population", StringComparison.OrdinalIgnoreCase)), Is.True);
        Assert.That(context.ContextItems.Single().Content, Does.Contain("Tokyo"));
    }

    private void AddConnectorInstance(string dataSourceId, IDataSourceConnector connector)
    {
        var field = typeof(DataSourceManager).GetField(
            "_connectorInstances",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);

        var instances = (Dictionary<string, IDataSourceConnector>)field!.GetValue(_dataSourceManager)!;
        instances[dataSourceId] = connector;
    }

    public sealed class ProfileOnlySqlConnector : IDataSourceConnector
    {
        public const string ConnectorId = "profile-only-sql-test-connector";

        public string Id => ConnectorId;
        public string Name => "Profile SQL";
        public string Description => "Profile-only SQL test connector.";
        public string Version => "1.0";
        public IEnumerable<ConnectorParameter> ConfigurationParameters => [];
        public bool SupportsLiveRag => true;

        public Task<bool> InitializeAsync(Dictionary<string, string> configuration) => Task.FromResult(true);

        public Task<IEnumerable<SearchResult>> SearchAsync(
            string searchTerm,
            int maxResults = 100,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Enumerable.Empty<SearchResult>());
        }

        public Task<LiveRagSourceProfile> DescribeLiveRagCapabilitiesAsync(
            DataSource dataSource,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LiveRagSourceProfile
            {
                DataSourceId = dataSource.Id,
                SourceName = dataSource.Name,
                ConnectorId = Id,
                SupportsNativeLiveRag = true,
                SupportedOperations = [LiveRagOperationType.TopN, LiveRagOperationType.Aggregate, LiveRagOperationType.KeywordSearch],
                Targets = ["cities"],
                Fields = ["cities.name", "cities.population"],
                MaxResults = 50
            });
        }

        public Task<LiveRagRetrievalResult> RetrieveLiveRagContextByOperationsAsync(
            LiveRagQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            var result = new LiveRagRetrievalResult
            {
                IsNativeLiveRag = true,
                SourceName = Name,
                ConnectorId = Id
            };

            foreach (var operation in request.Operations)
            {
                if (operation.Type == LiveRagOperationType.TopN &&
                    string.Equals(operation.Target, "cities", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(operation.SortField, "population", StringComparison.OrdinalIgnoreCase))
                {
                    result.ExecutedOperations.Add(operation);
                    result.ContextItems.Add(new LiveRagContextItem
                    {
                        Title = "Tokyo",
                        Content = "name: Tokyo | population: 37400068",
                        SourceName = Name,
                        ConnectorId = Id,
                        OriginalReference = "cities:1",
                        RelevanceScore = 100,
                        FromNativeLiveRag = true,
                        OperationId = operation.Id,
                        OperationType = operation.Type
                    });
                }
                else
                {
                    result.RejectedOperations.Add(operation);
                }
            }

            return Task.FromResult(result);
        }

        public Task<bool> TestConnectionAsync() => Task.FromResult(true);
        public DetailViewConfiguration GetDetailViewConfiguration(SearchResult result) => new();
        public FrameworkElement? CreateCustomDetailView(SearchResult result) => null;
        public Task<bool> ExecuteActionAsync(SearchResult result, string actionId) => Task.FromResult(false);
        public void Dispose() { }
    }
}
