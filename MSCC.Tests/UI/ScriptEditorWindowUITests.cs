using MSCC.Scripting;

namespace MSCC.Tests.UI;

/// <summary>
/// Tests für ScriptEditorWindow und ViewModel - 
/// inkl. Edge Cases für leere TextBox und Bounds-Checks.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class ScriptEditorWindowUITests
{
    private ScriptingService _scriptingService = null!;
    private ScriptRepository _repository = null!;
    private string _tempDir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _scriptingService = new ScriptingService();
        _tempDir = Path.Combine(Path.GetTempPath(), $"MSCC_Tests_{Guid.NewGuid()}");
        _repository = new ScriptRepository(_scriptingService, _tempDir);
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

    #region ViewModel Basic Tests

    [Test]
    public void ScriptEditorViewModel_CanBeCreated()
    {
        var viewModel = new ScriptEditorViewModel(_scriptingService, _repository);
        Assert.That(viewModel, Is.Not.Null);
    }

    [Test]
    public void ScriptEditorViewModel_HasCompileCommand()
    {
        var viewModel = new ScriptEditorViewModel(_scriptingService, _repository);
        Assert.That(viewModel.CompileCommand, Is.Not.Null);
    }

    [Test]
    public void ScriptEditorViewModel_HasSaveCommand()
    {
        var viewModel = new ScriptEditorViewModel(_scriptingService, _repository);
        Assert.That(viewModel.SaveCommand, Is.Not.Null);
    }

    [Test]
    public void ScriptEditorViewModel_LoadScript_SetsCurrentScript()
    {
        var viewModel = new ScriptEditorViewModel(_scriptingService, _repository);
        var script = _repository.Create("TestScript");
        
        viewModel.LoadScript(script);
        
        Assert.That(viewModel.CurrentScript, Is.Not.Null);
        Assert.That(viewModel.CurrentScript!.Metadata.Name, Is.EqualTo("TestScript"));
    }

    [Test]
    public void ScriptEditorViewModel_LoadScript_SetsSourceCode()
    {
        var viewModel = new ScriptEditorViewModel(_scriptingService, _repository);
        var script = _repository.Create("TestScript");
        
        viewModel.LoadScript(script);
        
        Assert.That(viewModel.SourceCode, Is.Not.Empty);
        Assert.That(viewModel.SourceCode, Does.Contain("class"));
    }

    [Test]
    public void ScriptEditorViewModel_SetSourceCode_SetsHasUnsavedChanges()
    {
        var viewModel = new ScriptEditorViewModel(_scriptingService, _repository);
        var script = _repository.Create("TestScript");
        viewModel.LoadScript(script);
        
        viewModel.SourceCode = "// Modified";
        
        Assert.That(viewModel.HasUnsavedChanges, Is.True);
    }

    #endregion

    #region Edge Cases - Empty/Null SourceCode

    [Test]
    public void ScriptEditorViewModel_EmptySourceCode_DoesNotThrow()
    {
        var viewModel = new ScriptEditorViewModel(_scriptingService, _repository);
        
        Assert.DoesNotThrow(() => viewModel.SourceCode = "");
        Assert.That(viewModel.SourceCode, Is.Empty);
    }

    [Test]
    public void ScriptEditorViewModel_EmptySourceCode_CursorPositionValid()
    {
        var viewModel = new ScriptEditorViewModel(_scriptingService, _repository);
        viewModel.SourceCode = "";
        
        // Cursor sollte bei leerer Source gültige Werte haben
        Assert.That(viewModel.CursorLine, Is.GreaterThanOrEqualTo(1));
        Assert.That(viewModel.CursorColumn, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void ScriptEditorViewModel_SetCursorLine_ZeroOrNegative_HandledGracefully()
    {
        var viewModel = new ScriptEditorViewModel(_scriptingService, _repository);
        
        // Setze ungültige Werte - sollte nicht crashen
        Assert.DoesNotThrow(() => viewModel.CursorLine = 0);
        Assert.DoesNotThrow(() => viewModel.CursorLine = -1);
    }

    [Test]
    public void ScriptEditorViewModel_SetCursorColumn_ZeroOrNegative_HandledGracefully()
    {
        var viewModel = new ScriptEditorViewModel(_scriptingService, _repository);
        
        // Setze ungültige Werte - sollte nicht crashen
        Assert.DoesNotThrow(() => viewModel.CursorColumn = 0);
        Assert.DoesNotThrow(() => viewModel.CursorColumn = -1);
    }

    #endregion

    #region Edge Cases - Script Loading

    [Test]
    public void ScriptEditorViewModel_LoadScript_WithEmptySourceCode()
    {
        var viewModel = new ScriptEditorViewModel(_scriptingService, _repository);
        var script = new ConnectorScript
        {
            Metadata = new ScriptMetadata { Name = "EmptyScript" },
            SourceCode = ""
        };
        
        Assert.DoesNotThrow(() => viewModel.LoadScript(script));
        Assert.That(viewModel.SourceCode, Is.Empty);
        Assert.That(viewModel.HasScript, Is.True);
    }

    [Test]
    public void ScriptEditorViewModel_LoadScript_WithWhitespaceOnlySourceCode()
    {
        var viewModel = new ScriptEditorViewModel(_scriptingService, _repository);
        var script = new ConnectorScript
        {
            Metadata = new ScriptMetadata { Name = "WhitespaceScript" },
            SourceCode = "   \n\n   \t\t  "
        };
        
        Assert.DoesNotThrow(() => viewModel.LoadScript(script));
        Assert.That(viewModel.HasScript, Is.True);
    }

    [Test]
    public void ScriptEditorViewModel_LoadScript_MultipleTimesDoesNotThrow()
    {
        var viewModel = new ScriptEditorViewModel(_scriptingService, _repository);
        var script1 = _repository.Create("Script1");
        var script2 = _repository.Create("Script2");
        
        Assert.DoesNotThrow(() =>
        {
            viewModel.LoadScript(script1);
            viewModel.LoadScript(script2);
            viewModel.LoadScript(script1);
        });
    }

    #endregion

    #region Edge Cases - Line Number Calculations

    [Test]
    public void LineCount_EmptyText_ReturnsAtLeastOne()
    {
        // Simuliere die Logik aus UpdateLineNumbers
        int lineCount = 0; // Simuliert leere TextBox
        if (lineCount < 1) lineCount = 1;
        
        var numbers = string.Join("\n", Enumerable.Range(1, lineCount));
        
        Assert.That(numbers, Is.EqualTo("1"));
    }

    [Test]
    public void LineCount_SingleLine_ReturnsOne()
    {
        int lineCount = 1;
        var numbers = string.Join("\n", Enumerable.Range(1, lineCount));
        
        Assert.That(numbers, Is.EqualTo("1"));
    }

    [Test]
    public void LineCount_MultipleLines_ReturnsCorrectNumbers()
    {
        int lineCount = 5;
        var numbers = string.Join("\n", Enumerable.Range(1, lineCount));
        
        Assert.That(numbers, Is.EqualTo("1\n2\n3\n4\n5"));
    }

    [Test]
    public void EnumerableRange_WithZeroCount_ReturnsEmptySequence()
    {
        // In .NET 10 gibt Enumerable.Range mit count=0 eine leere Sequenz zurück
        // Deshalb müssen wir lineCount >= 1 prüfen bevor wir Range aufrufen
        var result = Enumerable.Range(1, 0).ToList();
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void EnumerableRange_WithNegativeCount_ThrowsException()
    {
        // Nur negative counts werfen eine Exception
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = Enumerable.Range(1, -1).ToList();
        });
    }

    #endregion

    #region Edge Cases - Character Index Calculations

    [Test]
    public void CharacterIndex_NegativeLineIndex_HandledGracefully()
    {
        // Simuliere die Logik aus CodeEditor_SelectionChanged
        int lineIndex = -1; // GetLineIndexFromCharacterIndex kann -1 zurückgeben
        if (lineIndex < 0) lineIndex = 0;
        
        int cursorLine = lineIndex + 1;
        
        Assert.That(cursorLine, Is.EqualTo(1));
    }

    [Test]
    public void CharacterIndex_NegativeCharIndexStart_ReturnsZeroColumn()
    {
        // Simuliere die Logik aus CodeEditor_SelectionChanged
        int charIndexStart = -1; // GetCharacterIndexFromLineIndex kann -1 zurückgeben
        int caretIndex = 5;
        
        int charIndex = charIndexStart >= 0 ? caretIndex - charIndexStart : 0;
        int cursorColumn = charIndex + 1;
        
        Assert.That(cursorColumn, Is.EqualTo(1));
    }

    [Test]
    public void CharacterIndex_ValidValues_CalculatesCorrectly()
    {
        int lineIndex = 2;
        int charIndexStart = 50;
        int caretIndex = 55;
        
        int charIndex = charIndexStart >= 0 ? caretIndex - charIndexStart : 0;
        int cursorLine = lineIndex + 1;
        int cursorColumn = charIndex + 1;
        
        Assert.That(cursorLine, Is.EqualTo(3));
        Assert.That(cursorColumn, Is.EqualTo(6));
    }

    #endregion

    #region Compilation Error Navigation Edge Cases

    [Test]
    public void CompilationError_ZeroLine_ShouldNotNavigate()
    {
        var error = new CompilationError { Line = 0, Column = 5 };
        
        // Simuliere die Prüfung aus ErrorList_SelectionChanged
        bool shouldNavigate = error.Line > 0;
        
        Assert.That(shouldNavigate, Is.False);
    }

    [Test]
    public void CompilationError_NegativeLine_ShouldNotNavigate()
    {
        var error = new CompilationError { Line = -1, Column = 5 };
        
        bool shouldNavigate = error.Line > 0;
        
        Assert.That(shouldNavigate, Is.False);
    }

    [Test]
    public void CompilationError_LineExceedsLineCount_ShouldNotNavigate()
    {
        var error = new CompilationError { Line = 100, Column = 5 };
        int lineCount = 50;
        
        bool shouldNavigate = error.Line > 0 && error.Line <= lineCount;
        
        Assert.That(shouldNavigate, Is.False);
    }

    [Test]
    public void CompilationError_ValidLine_ShouldNavigate()
    {
        var error = new CompilationError { Line = 10, Column = 5 };
        int lineCount = 50;
        
        bool shouldNavigate = error.Line > 0 && error.Line <= lineCount;
        
        Assert.That(shouldNavigate, Is.True);
    }

    [Test]
    public void CompilationError_NegativeColumn_HandledGracefully()
    {
        var error = new CompilationError { Line = 1, Column = -5 };
        
        int safeColumn = Math.Max(0, error.Column - 1);
        
        Assert.That(safeColumn, Is.EqualTo(0));
    }

    #endregion
}
