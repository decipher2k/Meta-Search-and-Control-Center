using System.Windows.Controls;
using MSCC.Services;
using MSCC.Views;

namespace MSCC.Tests.UI;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class DataSourceDialogUITests
{
    private DataSourceManager? _dataSourceManager;

    [SetUp]
    public void SetUp()
    {
        try
        {
            _dataSourceManager = new DataSourceManager();
        }
        catch
        {
            _dataSourceManager = null;
        }
    }

    [Test]
    public void DataSourceManager_CanBeCreated()
    {
        // Test nur den Manager, nicht das UI
        Assert.That(_dataSourceManager, Is.Not.Null);
    }

    [Test]
    public void DataSourceManager_HasConnectors()
    {
        Assert.That(_dataSourceManager, Is.Not.Null);
        var connectors = _dataSourceManager!.GetAvailableConnectors().ToList();
        Assert.That(connectors.Count, Is.GreaterThan(0), "Es sollten Konnektoren verfügbar sein");
    }

    [Test]
    public void DataSourceManager_HasGroups()
    {
        Assert.That(_dataSourceManager, Is.Not.Null);
        var groups = _dataSourceManager!.GetAllGroups().ToList();
        Assert.That(groups, Is.Not.Null);
    }

    [Test]
    public void DataSourceManager_CanGetDataSources()
    {
        Assert.That(_dataSourceManager, Is.Not.Null);
        var dataSources = _dataSourceManager!.GetAllDataSources().ToList();
        Assert.That(dataSources, Is.Not.Null);
    }
}
