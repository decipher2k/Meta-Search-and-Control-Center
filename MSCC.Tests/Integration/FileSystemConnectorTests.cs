using MSCC.Connectors;
using MSCC.Models;

namespace MSCC.Tests.Integration;

[TestFixture]
public class FileSystemConnectorTests
{
    private FileSystemConnector _connector = null!;
    private string _testDir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _connector = new FileSystemConnector();
        _testDir = Path.Combine(Path.GetTempPath(), $"MSCC_FSTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        
        // Erstelle Testdateien
        File.WriteAllText(Path.Combine(_testDir, "testfile.txt"), "Test content");
        File.WriteAllText(Path.Combine(_testDir, "document.pdf"), "PDF content");
        File.WriteAllText(Path.Combine(_testDir, "image.png"), "PNG content");
        File.WriteAllText(Path.Combine(_testDir, "report_2024.docx"), "Report content");
    }

    [TearDown]
    public void TearDown()
    {
        _connector.Dispose();
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }
        catch { }
    }

    [Test]
    public void Connector_HasCorrectId()
    {
        Assert.That(_connector.Id, Is.EqualTo("filesystem-connector"));
    }

    [Test]
    public void Connector_HasCorrectName()
    {
        Assert.That(_connector.Name, Is.EqualTo("Dateisystem"));
    }

    [Test]
    public void Connector_HasConfigurationParameters()
    {
        var parameters = _connector.ConfigurationParameters.ToList();
        
        Assert.That(parameters.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(parameters.Any(p => p.Name == "BasePath"), Is.True);
    }

    [Test]
    public async Task Initialize_WithValidPath_ReturnsTrue()
    {
        var config = new Dictionary<string, string>
        {
            ["BasePath"] = _testDir
        };
        
        var result = await _connector.InitializeAsync(config);
        
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task Initialize_WithInvalidPath_ReturnsFalse()
    {
        var config = new Dictionary<string, string>
        {
            ["BasePath"] = "/nonexistent/path/that/does/not/exist"
        };
        
        var result = await _connector.InitializeAsync(config);
        
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task Initialize_WithEmptyBasePath_ReturnsFalse()
    {
        var config = new Dictionary<string, string>
        {
            ["BasePath"] = ""
        };
        
        var result = await _connector.InitializeAsync(config);
        
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task Initialize_WithMissingBasePath_ReturnsFalse()
    {
        var config = new Dictionary<string, string>();
        
        var result = await _connector.InitializeAsync(config);
        
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task Search_FindsMatchingFiles()
    {
        await _connector.InitializeAsync(new Dictionary<string, string>
        {
            ["BasePath"] = _testDir
        });
        
        var results = await _connector.SearchAsync("test");
        var resultList = results.ToList();
        
        Assert.That(resultList.Count, Is.EqualTo(1));
        Assert.That(resultList[0].Title, Is.EqualTo("testfile.txt"));
    }

    [Test]
    public async Task Search_CaseInsensitive()
    {
        await _connector.InitializeAsync(new Dictionary<string, string>
        {
            ["BasePath"] = _testDir
        });
        
        var results = await _connector.SearchAsync("TEST");
        var resultList = results.ToList();
        
        Assert.That(resultList.Count, Is.EqualTo(1));
        Assert.That(resultList[0].Title, Is.EqualTo("testfile.txt"));
    }

    [Test]
    public async Task Search_FindsMultipleMatches()
    {
        // Erstelle weitere Testdateien
        File.WriteAllText(Path.Combine(_testDir, "test2.txt"), "Content");
        File.WriteAllText(Path.Combine(_testDir, "mytest.doc"), "Content");
        
        await _connector.InitializeAsync(new Dictionary<string, string>
        {
            ["BasePath"] = _testDir
        });
        
        var results = await _connector.SearchAsync("test");
        var resultList = results.ToList();
        
        Assert.That(resultList.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task Search_ReturnsEmptyForNoMatch()
    {
        await _connector.InitializeAsync(new Dictionary<string, string>
        {
            ["BasePath"] = _testDir
        });
        
        var results = await _connector.SearchAsync("xyznomatch");
        var resultList = results.ToList();
        
        Assert.That(resultList.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task Search_ReturnsEmptyForEmptySearchTerm()
    {
        await _connector.InitializeAsync(new Dictionary<string, string>
        {
            ["BasePath"] = _testDir
        });
        
        var results = await _connector.SearchAsync("");
        var resultList = results.ToList();
        
        Assert.That(resultList.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task Search_ReturnsEmptyForWhitespaceSearchTerm()
    {
        await _connector.InitializeAsync(new Dictionary<string, string>
        {
            ["BasePath"] = _testDir
        });
        
        var results = await _connector.SearchAsync("   ");
        var resultList = results.ToList();
        
        Assert.That(resultList.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task Search_RespectsMaxResults()
    {
        // Erstelle viele Testdateien
        for (int i = 0; i < 20; i++)
        {
            File.WriteAllText(Path.Combine(_testDir, $"file{i}.txt"), "Content");
        }
        
        await _connector.InitializeAsync(new Dictionary<string, string>
        {
            ["BasePath"] = _testDir
        });
        
        var results = await _connector.SearchAsync("file", maxResults: 5);
        var resultList = results.ToList();
        
        Assert.That(resultList.Count, Is.EqualTo(5));
    }

    [Test]
    public async Task Search_ReturnsCorrectMetadata()
    {
        await _connector.InitializeAsync(new Dictionary<string, string>
        {
            ["BasePath"] = _testDir
        });
        
        var results = await _connector.SearchAsync("testfile");
        var result = results.FirstOrDefault();
        
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Metadata.ContainsKey("Size"), Is.True);
        Assert.That(result.Metadata.ContainsKey("Extension"), Is.True);
        Assert.That(result.Metadata["Extension"], Is.EqualTo(".txt"));
    }

    [Test]
    public async Task Search_SetsCorrectSourceName()
    {
        await _connector.InitializeAsync(new Dictionary<string, string>
        {
            ["BasePath"] = _testDir
        });
        
        var results = await _connector.SearchAsync("test");
        var result = results.FirstOrDefault();
        
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.SourceName, Is.EqualTo("Dateisystem"));
    }

    [Test]
    public async Task Search_SetsCorrectConnectorId()
    {
        await _connector.InitializeAsync(new Dictionary<string, string>
        {
            ["BasePath"] = _testDir
        });
        
        var results = await _connector.SearchAsync("test");
        var result = results.FirstOrDefault();
        
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ConnectorId, Is.EqualTo("filesystem-connector"));
    }

    [Test]
    public async Task Search_WithSearchPattern_FiltersByExtension()
    {
        await _connector.InitializeAsync(new Dictionary<string, string>
        {
            ["BasePath"] = _testDir,
            ["SearchPattern"] = "*.txt"
        });
        
        var results = await _connector.SearchAsync("file");
        var resultList = results.ToList();
        
        // Sollte nur .txt Dateien finden
        Assert.That(resultList.All(r => r.Title.EndsWith(".txt")), Is.True);
    }

    [Test]
    public async Task Search_IncludesSubdirectories()
    {
        // Erstelle Unterverzeichnis mit Datei
        var subDir = Path.Combine(_testDir, "subdir");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "subtest.txt"), "Content");
        
        await _connector.InitializeAsync(new Dictionary<string, string>
        {
            ["BasePath"] = _testDir,
            ["IncludeSubdirectories"] = "true"
        });
        
        var results = await _connector.SearchAsync("subtest");
        var resultList = results.ToList();
        
        Assert.That(resultList.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Search_ExcludesSubdirectoriesWhenConfigured()
    {
        // Erstelle Unterverzeichnis mit Datei
        var subDir = Path.Combine(_testDir, "subdir2");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "hidden.txt"), "Content");
        
        await _connector.InitializeAsync(new Dictionary<string, string>
        {
            ["BasePath"] = _testDir,
            ["IncludeSubdirectories"] = "false"
        });
        
        var results = await _connector.SearchAsync("hidden");
        var resultList = results.ToList();
        
        Assert.That(resultList.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task TestConnection_AfterInitialize_ReturnsTrue()
    {
        await _connector.InitializeAsync(new Dictionary<string, string>
        {
            ["BasePath"] = _testDir
        });
        
        var result = await _connector.TestConnectionAsync();
        
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task TestConnection_BeforeInitialize_ReturnsFalse()
    {
        var result = await _connector.TestConnectionAsync();
        
        Assert.That(result, Is.False);
    }
}
