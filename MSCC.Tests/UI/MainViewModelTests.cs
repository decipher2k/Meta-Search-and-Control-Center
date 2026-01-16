using System.Windows.Controls;
using MSCC.ViewModels;

namespace MSCC.Tests.UI;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class MainViewModelTests
{
    private MainViewModel? _viewModel;

    [SetUp]
    public void SetUp()
    {
        try
        {
            _viewModel = new MainViewModel();
        }
        catch
        {
            _viewModel = null;
        }
    }

    [Test]
    public void ViewModel_CanBeCreated()
    {
        Assert.That(_viewModel, Is.Not.Null);
    }

    [Test]
    public void ViewModel_InitialState_HasEmptySearchTerm()
    {
        Assert.That(_viewModel, Is.Not.Null);
        Assert.That(_viewModel!.SearchTerm, Is.Empty);
    }

    [Test]
    public void ViewModel_InitialState_IsNotSearching()
    {
        Assert.That(_viewModel, Is.Not.Null);
        Assert.That(_viewModel!.IsSearching, Is.False);
    }

    [Test]
    public void ViewModel_InitialState_HasNoSearchResults()
    {
        Assert.That(_viewModel, Is.Not.Null);
        Assert.That(_viewModel!.SearchResults, Is.Empty);
    }

    [Test]
    public void ViewModel_InitialState_HasNoSelectedResult()
    {
        Assert.That(_viewModel, Is.Not.Null);
        Assert.That(_viewModel!.SelectedResult, Is.Null);
    }

    [Test]
    public void ViewModel_SetSearchTerm_UpdatesProperty()
    {
        Assert.That(_viewModel, Is.Not.Null);
        _viewModel!.SearchTerm = "test";
        Assert.That(_viewModel.SearchTerm, Is.EqualTo("test"));
    }

    [Test]
    public void ViewModel_SearchCommand_Exists()
    {
        Assert.That(_viewModel, Is.Not.Null);
        Assert.That(_viewModel!.SearchCommand, Is.Not.Null);
    }

    [Test]
    public void ViewModel_CancelSearchCommand_Exists()
    {
        Assert.That(_viewModel, Is.Not.Null);
        Assert.That(_viewModel!.CancelSearchCommand, Is.Not.Null);
    }

    [Test]
    public void ViewModel_AddDataSourceCommand_Exists()
    {
        Assert.That(_viewModel, Is.Not.Null);
        Assert.That(_viewModel!.AddDataSourceCommand, Is.Not.Null);
    }

    [Test]
    public void ViewModel_EditDataSourceCommand_Exists()
    {
        Assert.That(_viewModel, Is.Not.Null);
        Assert.That(_viewModel!.EditDataSourceCommand, Is.Not.Null);
    }

    [Test]
    public void ViewModel_DeleteDataSourceCommand_Exists()
    {
        Assert.That(_viewModel, Is.Not.Null);
        Assert.That(_viewModel!.DeleteDataSourceCommand, Is.Not.Null);
    }

    [Test]
    public void ViewModel_AddGroupCommand_Exists()
    {
        Assert.That(_viewModel, Is.Not.Null);
        Assert.That(_viewModel!.AddGroupCommand, Is.Not.Null);
    }

    [Test]
    public void ViewModel_DataSourceManager_IsNotNull()
    {
        Assert.That(_viewModel, Is.Not.Null);
        Assert.That(_viewModel!.DataSourceManager, Is.Not.Null);
    }

    [Test]
    public void ViewModel_DataSources_IsNotNull()
    {
        Assert.That(_viewModel, Is.Not.Null);
        Assert.That(_viewModel!.DataSources, Is.Not.Null);
    }

    [Test]
    public void ViewModel_Groups_IsNotNull()
    {
        Assert.That(_viewModel, Is.Not.Null);
        Assert.That(_viewModel!.Groups, Is.Not.Null);
    }

    [Test]
    public void ViewModel_SavedQueries_IsNotNull()
    {
        Assert.That(_viewModel, Is.Not.Null);
        Assert.That(_viewModel!.SavedQueries, Is.Not.Null);
    }

    [Test]
    public void ViewModel_StatusMessage_HasDefaultValue()
    {
        Assert.That(_viewModel, Is.Not.Null);
        Assert.That(_viewModel!.StatusMessage, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void ViewModel_RefreshDataSources_DoesNotThrow()
    {
        Assert.That(_viewModel, Is.Not.Null);
        Assert.DoesNotThrow(() => _viewModel!.RefreshDataSources());
    }

    [Test]
    public void ViewModel_PropertyChanged_FiresOnSearchTermChange()
    {
        Assert.That(_viewModel, Is.Not.Null);
        var fired = false;
        _viewModel!.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SearchTerm))
                fired = true;
        };

        _viewModel.SearchTerm = "new value";

        Assert.That(fired, Is.True);
    }

    [Test]
    public void ViewModel_PropertyChanged_FiresOnStatusMessageChange()
    {
        Assert.That(_viewModel, Is.Not.Null);
        var fired = false;
        _viewModel!.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.StatusMessage))
                fired = true;
        };

        _viewModel.StatusMessage = "New status";

        Assert.That(fired, Is.True);
    }
}
