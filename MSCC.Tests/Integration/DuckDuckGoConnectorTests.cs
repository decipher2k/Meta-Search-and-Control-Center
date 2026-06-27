using System.Reflection;
using MSCC.Connectors;
using MSCC.Models;

namespace MSCC.Tests.Integration;

[TestFixture]
public class DuckDuckGoConnectorTests
{
    [Test]
    public void ParseSearchResults_ParsesDuckDuckGoHtmlAnchors()
    {
        const string html = """
            <div class="result results_links">
                <h2 class="result__title">
                    <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Fbill-gates">Bill Gates - Microsoft</a>
                </h2>
                <a class="result__snippet" href="/l/?kh=-1">Bill Gates co-founded Microsoft and works on philanthropy.</a>
            </div>
            """;

        var results = Parse(html);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Title, Is.EqualTo("Bill Gates - Microsoft"));
        Assert.That(results[0].Description, Does.Contain("philanthropy"));
        Assert.That(results[0].OriginalReference, Is.EqualTo("https://example.com/bill-gates"));
    }

    [Test]
    public void ParseSearchResults_ParsesDuckDuckGoLiteAnchors()
    {
        const string html = """
            <table>
                <tr>
                    <td>
                        <a rel="nofollow" class="result-link" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.org%2Fbill">Bill Gates Biography</a>
                    </td>
                </tr>
            </table>
            """;

        var results = Parse(html);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Title, Is.EqualTo("Bill Gates Biography"));
        Assert.That(results[0].OriginalReference, Is.EqualTo("https://example.org/bill"));
    }

    private static List<SearchResult> Parse(string html)
    {
        var connector = new DuckDuckGoConnector();
        var method = typeof(DuckDuckGoConnector).GetMethod(
            "ParseSearchResults",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        return (List<SearchResult>)method!.Invoke(connector, [html, 10])!;
    }
}
