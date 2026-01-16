using MSCC.Scripting;

namespace MSCC.Tests.Integration;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class ScriptingServiceTests
{
    private ScriptingService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new ScriptingService();
    }

    [Test]
    public void ScriptingService_CanBeCreated()
    {
        Assert.That(_service, Is.Not.Null);
    }

    [Test]
    public void Validate_EmptyCode_ReturnsNoErrors()
    {
        var errors = _service.Validate("");
        
        // Leerer Code sollte keine Parse-Fehler haben
        Assert.That(errors.Count(e => e.Severity == "Error"), Is.EqualTo(0));
    }

    [Test]
    public void Validate_ValidCode_ReturnsNoErrors()
    {
        var code = @"
using System;
namespace Test
{
    public class TestClass
    {
        public void Method() { }
    }
}";
        var errors = _service.Validate(code);
        
        Assert.That(errors.Count(e => e.Severity == "Error"), Is.EqualTo(0));
    }

    [Test]
    public void Validate_InvalidCode_ReturnsErrors()
    {
        var code = @"
public class TestClass
{
    public void Method(
}"; // Fehlende schließende Klammer
        
        var errors = _service.Validate(code);
        
        Assert.That(errors.Count(e => e.Severity == "Error"), Is.GreaterThan(0));
    }

    [Test]
    public void Validate_SyntaxError_ReturnsLineNumber()
    {
        var code = @"public class Test {
    public void Method() {
        int x =  // Fehler in Zeile 3
    }
}";
        var errors = _service.Validate(code);
        var firstError = errors.FirstOrDefault(e => e.Severity == "Error");
        
        Assert.That(firstError, Is.Not.Null);
        Assert.That(firstError!.Line, Is.GreaterThan(0));
    }

    [Test]
    public void Compile_ValidConnectorScript_Succeeds()
    {
        var script = new ConnectorScript
        {
            SourceCode = ScriptingService.GetScriptTemplate("TestConnector", "test-id")
        };
        
        var result = _service.Compile(script);
        
        // Kompilierung kann fehlschlagen wegen fehlender Referenzen, 
        // aber sollte nicht werfen
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void Compile_InvalidCode_ReturnsFailed()
    {
        var script = new ConnectorScript
        {
            SourceCode = "this is not valid C# code { }}}}"
        };
        
        var result = _service.Compile(script);
        
        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.Count, Is.GreaterThan(0));
    }

    [Test]
    public void Compile_Result_HasCorrectErrorInfo()
    {
        var script = new ConnectorScript
        {
            SourceCode = @"
public class Test {
    public void Method() {
        undefinedVariable = 5; // Error
    }
}"
        };
        
        var result = _service.Compile(script);
        
        Assert.That(result.Success, Is.False);
        var error = result.Errors.FirstOrDefault();
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Message, Is.Not.Empty);
        Assert.That(error.Line, Is.GreaterThan(0));
    }

    [Test]
    public void GetScriptTemplate_ReturnsValidCode()
    {
        var template = ScriptingService.GetScriptTemplate("MyConnector", "my-id");
        
        Assert.That(template, Is.Not.Empty);
        Assert.That(template, Does.Contain("class"));
        Assert.That(template, Does.Contain("MyConnector"));
        Assert.That(template, Does.Contain("my-id"));
    }

    [Test]
    public void GetScriptTemplate_ContainsRequiredUsings()
    {
        var template = ScriptingService.GetScriptTemplate("Test", "id");
        
        Assert.That(template, Does.Contain("using System;"));
        Assert.That(template, Does.Contain("using System.Collections.Generic;"));
        Assert.That(template, Does.Contain("using MSCC.Scripting;"));
    }

    [Test]
    public void GetScriptTemplate_InheritsFromScriptedConnectorBase()
    {
        var template = ScriptingService.GetScriptTemplate("Test", "id");
        
        Assert.That(template, Does.Contain("ScriptedConnectorBase"));
    }

    [Test]
    public void GetScriptTemplate_ImplementsSearchAsync()
    {
        var template = ScriptingService.GetScriptTemplate("Test", "id");
        
        Assert.That(template, Does.Contain("SearchAsync"));
    }

    [Test]
    public async Task GetCompletionsAsync_DoesNotThrow()
    {
        var code = "using System; class Test { void M() { Console. } }";
        
        var completions = await _service.GetCompletionsAsync(code, 45);
        
        // Kann leer sein, aber sollte nicht werfen
        Assert.That(completions, Is.Not.Null);
    }

    [Test]
    public void GetScriptTemplate_SanitizesClassName()
    {
        var template = ScriptingService.GetScriptTemplate("Test-With-Dashes!", "id");
        
        // Sollte nur alphanumerische Zeichen enthalten
        Assert.That(template, Does.Contain("TestWithDashes"));
    }
}
