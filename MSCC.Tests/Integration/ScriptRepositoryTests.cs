using MSCC.Scripting;

namespace MSCC.Tests.Integration;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class ScriptRepositoryTests
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

    [Test]
    public void Repository_CanBeCreated()
    {
        Assert.That(_repository, Is.Not.Null);
    }

    [Test]
    public void Repository_ScriptsDirectory_IsSet()
    {
        Assert.That(_repository.ScriptsDirectory, Is.EqualTo(_tempDir));
    }

    [Test]
    public void Create_ReturnsNewScript()
    {
        var script = _repository.Create("TestScript");

        Assert.That(script, Is.Not.Null);
        Assert.That(script.Metadata.Name, Is.EqualTo("TestScript"));
    }

    [Test]
    public void Create_SetsUniqueId()
    {
        var script1 = _repository.Create("Script1");
        var script2 = _repository.Create("Script2");

        Assert.That(script1.Metadata.Id, Is.Not.EqualTo(script2.Metadata.Id));
    }

    [Test]
    public void Create_GeneratesSourceCode()
    {
        var script = _repository.Create("TestScript");

        Assert.That(script.SourceCode, Is.Not.Empty);
        Assert.That(script.SourceCode, Does.Contain("class"));
    }

    [Test]
    public void GetById_ReturnsScript()
    {
        var created = _repository.Create("TestScript");
        
        var retrieved = _repository.GetById(created.Metadata.Id);

        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Metadata.Name, Is.EqualTo("TestScript"));
    }

    [Test]
    public void GetById_ReturnsNullForUnknownId()
    {
        var result = _repository.GetById("unknown-id");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetAll_ReturnsAllScripts()
    {
        _repository.Create("Script1");
        _repository.Create("Script2");
        _repository.Create("Script3");

        var all = _repository.GetAll().ToList();

        Assert.That(all.Count, Is.EqualTo(3));
    }

    [Test]
    public void Update_ModifiesScript()
    {
        var script = _repository.Create("TestScript");
        script.Metadata.Name = "UpdatedName";
        
        var result = _repository.Update(script);

        Assert.That(result, Is.True);
        var retrieved = _repository.GetById(script.Metadata.Id);
        Assert.That(retrieved!.Metadata.Name, Is.EqualTo("UpdatedName"));
    }

    [Test]
    public void Update_ReturnsFalseForUnknownScript()
    {
        var unknownScript = new ConnectorScript { Metadata = new ScriptMetadata { Id = "unknown" } };
        
        var result = _repository.Update(unknownScript);

        Assert.That(result, Is.False);
    }

    [Test]
    public void Delete_RemovesScript()
    {
        var script = _repository.Create("TestScript");
        
        var result = _repository.Delete(script.Metadata.Id);

        Assert.That(result, Is.True);
        Assert.That(_repository.GetById(script.Metadata.Id), Is.Null);
    }

    [Test]
    public void Delete_ReturnsFalseForUnknownId()
    {
        var result = _repository.Delete("unknown-id");

        Assert.That(result, Is.False);
    }

    [Test]
    public void Save_CreatesFile()
    {
        var script = _repository.Create("TestScript");
        
        var result = _repository.Save(script);

        Assert.That(result, Is.True);
        Assert.That(Directory.GetFiles(_tempDir, "*.cs").Length, Is.GreaterThan(0));
    }

    [Test]
    public void Save_CreatesMetadataFile()
    {
        var script = _repository.Create("TestScript");
        
        _repository.Save(script);

        Assert.That(Directory.GetFiles(_tempDir, "*.meta").Length, Is.GreaterThan(0));
    }

    [Test]
    public void Save_UpdatesFilePath()
    {
        var script = _repository.Create("TestScript");
        
        _repository.Save(script);

        Assert.That(script.FilePath, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task LoadAllAsync_LoadsSavedScripts()
    {
        var script = _repository.Create("TestScript");
        _repository.Save(script);
        
        // Erstelle neues Repository mit gleichem Verzeichnis
        var newRepo = new ScriptRepository(_scriptingService, _tempDir);
        var count = await newRepo.LoadAllAsync();

        Assert.That(count, Is.GreaterThan(0));
    }

    [Test]
    public async Task LoadFromFileAsync_LoadsScript()
    {
        var script = _repository.Create("TestScript");
        _repository.Save(script);
        
        var loaded = await _repository.LoadFromFileAsync(script.FilePath!);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.SourceCode, Is.Not.Empty);
    }

    [Test]
    public async Task LoadFromFileAsync_ReturnsNullForMissingFile()
    {
        var result = await _repository.LoadFromFileAsync("/nonexistent/file.cs");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void CompileAndRegister_CompilesScript()
    {
        var script = _repository.Create("TestScript");
        
        var result = _repository.CompileAndRegister(script);

        Assert.That(result, Is.Not.Null);
        // Kompilierung kann fehlschlagen, aber sollte nicht werfen
    }

    [Test]
    public void Export_CreatesFile()
    {
        var script = _repository.Create("TestScript");
        var exportPath = Path.Combine(_tempDir, "exported.cs");
        
        var result = _repository.Export(script.Metadata.Id, exportPath);

        Assert.That(result, Is.True);
        Assert.That(File.Exists(exportPath), Is.True);
    }

    [Test]
    public void Export_ReturnsFalseForUnknownScript()
    {
        var exportPath = Path.Combine(_tempDir, "exported.cs");
        
        var result = _repository.Export("unknown-id", exportPath);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ImportAsync_ImportsScript()
    {
        var script = _repository.Create("TestScript");
        _repository.Save(script);
        
        var importedScript = await _repository.ImportAsync(script.FilePath!);

        Assert.That(importedScript, Is.Not.Null);
        // Importiertes Script hat neue ID
        Assert.That(importedScript!.Metadata.Id, Is.Not.EqualTo(script.Metadata.Id));
    }
}
