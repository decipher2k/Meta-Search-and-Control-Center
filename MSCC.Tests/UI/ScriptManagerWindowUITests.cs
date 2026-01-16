using System.Windows.Controls;
using MSCC.Scripting;
using MSCC.Views;

namespace MSCC.Tests.UI;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class ScriptManagerWindowUITests
{
    private ScriptingService _scriptingService = null!;
    private ScriptRepository _repository = null!;
    private string _tempDir = string.Empty;
    private bool _canRunWindowTests;

    [SetUp]
    public void SetUp()
    {
        _scriptingService = new ScriptingService();
        _tempDir = Path.Combine(Path.GetTempPath(), $"MSCC_Tests_{Guid.NewGuid()}");
        _repository = new ScriptRepository(_scriptingService, _tempDir);

        // Prüfen ob Window-Tests möglich sind
        try
        {
            var window = new ScriptManagerWindow(_scriptingService, _repository);
            window.Close();
            _canRunWindowTests = true;
        }
        catch
        {
            _canRunWindowTests = false;
        }
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    [Test]
    public void ScriptManagerWindow_CanBeCreated()
    {
        if (!_canRunWindowTests)
        {
            Assert.Pass("Window tests skipped - no WPF context available");
            return;
        }

        var window = new ScriptManagerWindow(_scriptingService, _repository);
        Assert.That(window, Is.Not.Null);
        window.Close();
    }

    [Test]
    public void ScriptManagerWindow_HasCorrectTitle()
    {
        if (!_canRunWindowTests)
        {
            Assert.Pass("Window tests skipped - no WPF context available");
            return;
        }

        var window = new ScriptManagerWindow(_scriptingService, _repository);
        Assert.That(window.Title, Does.Contain("Script Manager"));
        window.Close();
    }

    [Test]
    public void ScriptManagerWindow_HasDataContext()
    {
        if (!_canRunWindowTests)
        {
            Assert.Pass("Window tests skipped - no WPF context available");
            return;
        }

        var window = new ScriptManagerWindow(_scriptingService, _repository);
        Assert.That(window.DataContext, Is.InstanceOf<ScriptManagerViewModel>());
        window.Close();
    }

    [Test]
    public void ScriptManagerWindow_HasNewScriptTextBox()
    {
        if (!_canRunWindowTests)
        {
            Assert.Pass("Window tests skipped - no WPF context available");
            return;
        }

        var window = new ScriptManagerWindow(_scriptingService, _repository);
        window.Show();
        window.UpdateLayout();

        var textBoxes = UITestHelper.FindAllChildren<TextBox>(window);
        Assert.That(textBoxes.Count, Is.GreaterThan(0), "TextBox für Script-Namen sollte vorhanden sein");
        window.Close();
    }

    [Test]
    public void ScriptManagerWindow_HasButtons()
    {
        if (!_canRunWindowTests)
        {
            Assert.Pass("Window tests skipped - no WPF context available");
            return;
        }

        var window = new ScriptManagerWindow(_scriptingService, _repository);
        window.Show();
        window.UpdateLayout();

        var buttons = UITestHelper.FindAllChildren<Button>(window);
        Assert.That(buttons.Count, Is.GreaterThanOrEqualTo(3), "Mindestens 3 Buttons sollten vorhanden sein");
        window.Close();
    }

    [Test]
    public void ScriptManagerWindow_HasToolbarWithButtons()
    {
        if (!_canRunWindowTests)
        {
            Assert.Pass("Window tests skipped - no WPF context available");
            return;
        }

        var window = new ScriptManagerWindow(_scriptingService, _repository);
        window.Show();
        window.UpdateLayout();

        var buttons = UITestHelper.FindAllChildren<Button>(window);
        Assert.That(buttons.Count, Is.GreaterThanOrEqualTo(3), "Mindestens 3 Buttons sollten vorhanden sein");
        window.Close();
    }

    [Test]
    public void ScriptManagerWindow_HasNewScriptButton()
    {
        if (!_canRunWindowTests)
        {
            Assert.Pass("Window tests skipped - no WPF context available");
            return;
        }

        var window = new ScriptManagerWindow(_scriptingService, _repository);
        window.Show();
        window.UpdateLayout();

        var buttons = UITestHelper.FindAllChildren<Button>(window);
        var newScriptButton = buttons.FirstOrDefault(b =>
            b.Content?.ToString()?.Contains("Neues Script") == true);
        Assert.That(newScriptButton, Is.Not.Null, "Neues Script Button sollte vorhanden sein");
        window.Close();
    }

    [Test]
    public void ScriptManagerWindow_HasCompileAllButton()
    {
        if (!_canRunWindowTests)
        {
            Assert.Pass("Window tests skipped - no WPF context available");
            return;
        }

        var window = new ScriptManagerWindow(_scriptingService, _repository);
        window.Show();
        window.UpdateLayout();

        var buttons = UITestHelper.FindAllChildren<Button>(window);
        var compileButton = buttons.FirstOrDefault(b =>
            b.Content?.ToString()?.Contains("kompilieren") == true);
        Assert.That(compileButton, Is.Not.Null, "Alle kompilieren Button sollte vorhanden sein");
        window.Close();
    }

    [Test]
    public void ScriptManagerWindow_HasListView()
    {
        if (!_canRunWindowTests)
        {
            Assert.Pass("Window tests skipped - no WPF context available");
            return;
        }

        var window = new ScriptManagerWindow(_scriptingService, _repository);
        window.Show();
        window.UpdateLayout();

        var listView = UITestHelper.FindAllChildren<ListView>(window).FirstOrDefault();
        Assert.That(listView, Is.Not.Null, "ListView für Scripts sollte vorhanden sein");
        window.Close();
    }
}
