using MSCC.Scripting;

namespace MSCC.Tests.UI;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class ScriptManagerViewModelTests
{
    private ScriptingService _scriptingService = null!;
    private ScriptRepository _repository = null!;
    private ScriptManagerViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _scriptingService = new ScriptingService();
        
        // Temporäres Verzeichnis für Tests
        var tempDir = Path.Combine(Path.GetTempPath(), $"MSCC_Tests_{Guid.NewGuid()}");
        _repository = new ScriptRepository(_scriptingService, tempDir);
        
        _viewModel = new ScriptManagerViewModel(_scriptingService, _repository);
    }

    [TearDown]
    public void TearDown()
    {
        // Aufräumen
        try
        {
            if (Directory.Exists(_repository.ScriptsDirectory))
            {
                Directory.Delete(_repository.ScriptsDirectory, true);
            }
        }
        catch { }
    }

    [Test]
    public void ViewModel_InitialState_HasEmptyScripts()
    {
        Assert.That(_viewModel.Scripts, Is.Empty);
    }

    [Test]
    public void ViewModel_InitialState_HasNoSelection()
    {
        Assert.That(_viewModel.SelectedScript, Is.Null);
    }

    [Test]
    public void ViewModel_InitialState_IsNotLoading()
    {
        Assert.That(_viewModel.IsLoading, Is.False);
    }

    [Test]
    public void ViewModel_InitialState_HasDefaultNewScriptName()
    {
        Assert.That(_viewModel.NewScriptName, Is.Not.Empty);
    }

    [Test]
    public void ViewModel_CreateCommand_Exists()
    {
        Assert.That(_viewModel.CreateCommand, Is.Not.Null);
    }

    [Test]
    public void ViewModel_EditCommand_Exists()
    {
        Assert.That(_viewModel.EditCommand, Is.Not.Null);
    }

    [Test]
    public void ViewModel_DeleteCommand_Exists()
    {
        Assert.That(_viewModel.DeleteCommand, Is.Not.Null);
    }

    [Test]
    public void ViewModel_CompileCommand_Exists()
    {
        Assert.That(_viewModel.CompileCommand, Is.Not.Null);
    }

    [Test]
    public void ViewModel_CompileAllCommand_Exists()
    {
        Assert.That(_viewModel.CompileAllCommand, Is.Not.Null);
    }

    [Test]
    public void ViewModel_RefreshCommand_Exists()
    {
        Assert.That(_viewModel.RefreshCommand, Is.Not.Null);
    }

    [Test]
    public void ViewModel_ImportCommand_Exists()
    {
        Assert.That(_viewModel.ImportCommand, Is.Not.Null);
    }

    [Test]
    public void ViewModel_ExportCommand_Exists()
    {
        Assert.That(_viewModel.ExportCommand, Is.Not.Null);
    }

    [Test]
    public void ViewModel_ScriptsDirectory_IsNotEmpty()
    {
        Assert.That(_viewModel.ScriptsDirectory, Is.Not.Empty);
    }

    [Test]
    public void ViewModel_HasSelection_IsFalseInitially()
    {
        Assert.That(_viewModel.HasSelection, Is.False);
    }

    [Test]
    public void ViewModel_StatusMessage_IsNotNull()
    {
        Assert.That(_viewModel.StatusMessage, Is.Not.Null);
    }

    [Test]
    public async Task ViewModel_LoadScriptsAsync_DoesNotThrow()
    {
        await _viewModel.LoadScriptsAsync();
        Assert.Pass();
    }

    [Test]
    public async Task ViewModel_LoadScriptsAsync_SetsLoadingState()
    {
        var wasLoading = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ScriptManagerViewModel.IsLoading))
                wasLoading = true;
        };

        await _viewModel.LoadScriptsAsync();

        Assert.That(wasLoading, Is.True);
    }

    [Test]
    public async Task ViewModel_LoadScriptsAsync_UpdatesStatusMessage()
    {
        await _viewModel.LoadScriptsAsync();
        Assert.That(_viewModel.StatusMessage, Does.Contain("geladen").Or.Contain("Scripts"));
    }
}
