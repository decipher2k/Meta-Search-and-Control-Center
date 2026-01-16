using MSCC.Scripting;

namespace MSCC.Tests.UI;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class ScriptEditorViewModelTests
{
    private ScriptingService _scriptingService = null!;
    private ScriptRepository _repository = null!;
    private ScriptEditorViewModel _viewModel = null!;
    private string _tempDir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _scriptingService = new ScriptingService();
        _tempDir = Path.Combine(Path.GetTempPath(), $"MSCC_Tests_{Guid.NewGuid()}");
        _repository = new ScriptRepository(_scriptingService, _tempDir);
        _viewModel = new ScriptEditorViewModel(_scriptingService, _repository);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch { }
    }

    [Test]
    public void ViewModel_InitialState_HasNoScript()
    {
        Assert.That(_viewModel.CurrentScript, Is.Null);
    }

    [Test]
    public void ViewModel_InitialState_HasEmptySourceCode()
    {
        Assert.That(_viewModel.SourceCode, Is.Empty);
    }

    [Test]
    public void ViewModel_InitialState_IsNotCompiling()
    {
        Assert.That(_viewModel.IsCompiling, Is.False);
    }

    [Test]
    public void ViewModel_InitialState_HasNoUnsavedChanges()
    {
        Assert.That(_viewModel.HasUnsavedChanges, Is.False);
    }

    [Test]
    public void ViewModel_InitialState_CursorLineIsOne()
    {
        Assert.That(_viewModel.CursorLine, Is.EqualTo(1));
    }

    [Test]
    public void ViewModel_InitialState_CursorColumnIsOne()
    {
        Assert.That(_viewModel.CursorColumn, Is.EqualTo(1));
    }

    [Test]
    public void ViewModel_HasScript_IsFalseInitially()
    {
        Assert.That(_viewModel.HasScript, Is.False);
    }

    [Test]
    public void ViewModel_ScriptName_ShowsNoScriptInitially()
    {
        Assert.That(_viewModel.ScriptName, Does.Contain("Kein Script"));
    }

    [Test]
    public void ViewModel_CompileCommand_Exists()
    {
        Assert.That(_viewModel.CompileCommand, Is.Not.Null);
    }

    [Test]
    public void ViewModel_SaveCommand_Exists()
    {
        Assert.That(_viewModel.SaveCommand, Is.Not.Null);
    }

    [Test]
    public void ViewModel_ValidateCommand_Exists()
    {
        Assert.That(_viewModel.ValidateCommand, Is.Not.Null);
    }

    [Test]
    public void ViewModel_InsertTemplateCommand_Exists()
    {
        Assert.That(_viewModel.InsertTemplateCommand, Is.Not.Null);
    }

    [Test]
    public void ViewModel_Errors_IsNotNull()
    {
        Assert.That(_viewModel.Errors, Is.Not.Null);
    }

    [Test]
    public void ViewModel_Warnings_IsNotNull()
    {
        Assert.That(_viewModel.Warnings, Is.Not.Null);
    }

    [Test]
    public void ViewModel_LoadScript_SetsCurrentScript()
    {
        var script = _repository.Create("TestScript");
        
        _viewModel.LoadScript(script);

        Assert.That(_viewModel.CurrentScript, Is.Not.Null);
        Assert.That(_viewModel.CurrentScript!.Metadata.Name, Is.EqualTo("TestScript"));
    }

    [Test]
    public void ViewModel_LoadScript_SetsSourceCode()
    {
        var script = _repository.Create("TestScript");
        
        _viewModel.LoadScript(script);

        Assert.That(_viewModel.SourceCode, Is.Not.Empty);
    }

    [Test]
    public void ViewModel_LoadScript_UpdatesHasScript()
    {
        var script = _repository.Create("TestScript");
        
        _viewModel.LoadScript(script);

        Assert.That(_viewModel.HasScript, Is.True);
    }

    [Test]
    public void ViewModel_LoadScript_UpdatesScriptName()
    {
        var script = _repository.Create("MyTestConnector");
        
        _viewModel.LoadScript(script);

        Assert.That(_viewModel.ScriptName, Is.EqualTo("MyTestConnector"));
    }

    [Test]
    public void ViewModel_LoadScript_ClearsErrors()
    {
        _viewModel.Errors.Add(new CompilationError { Message = "Old Error" });
        var script = _repository.Create("TestScript");
        
        _viewModel.LoadScript(script);

        Assert.That(_viewModel.Errors, Is.Empty);
    }

    [Test]
    public void ViewModel_LoadScript_ClearsWarnings()
    {
        _viewModel.Warnings.Add(new CompilationError { Message = "Old Warning" });
        var script = _repository.Create("TestScript");
        
        _viewModel.LoadScript(script);

        Assert.That(_viewModel.Warnings, Is.Empty);
    }

    [Test]
    public void ViewModel_SetSourceCode_SetsHasUnsavedChanges()
    {
        var script = _repository.Create("TestScript");
        _viewModel.LoadScript(script);
        
        _viewModel.SourceCode = "// Modified code";

        Assert.That(_viewModel.HasUnsavedChanges, Is.True);
    }

    [Test]
    public void ViewModel_PropertyChanged_FiresOnSourceCodeChange()
    {
        var fired = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ScriptEditorViewModel.SourceCode))
                fired = true;
        };

        _viewModel.SourceCode = "new code";

        Assert.That(fired, Is.True);
    }

    [Test]
    public void ViewModel_OnTemplateInsertRequested_EventExists()
    {
        var eventFired = false;
        _viewModel.OnTemplateInsertRequested += (template) => eventFired = true;
        
        // Event sollte existieren (nicht werfen)
        Assert.Pass();
    }

    [Test]
    public async Task ViewModel_GetCompletionsAsync_ReturnsResults()
    {
        var script = _repository.Create("TestScript");
        _viewModel.LoadScript(script);
        
        var completions = await _viewModel.GetCompletionsAsync(10);

        // Kann leer sein, aber sollte nicht werfen
        Assert.That(completions, Is.Not.Null);
    }
}
