// Codex developer note: Explains the purpose and flow of webapi-oyako/webapi-oyako.Tests/TextExtractorTests.cs for maintainers.
using webapi_oyako.Infrastructure.Crawling;
using Xunit;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Tests;

// Implements the TextExtractorTests component and its responsibilities in the Oyako codebase.
public class TextExtractorTests
{
    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public void ExtractText_RemovesScriptNode()
    {
        // Creates the object needed for the next step of the workflow.
        var extractor = new RenderedTextExtractor();
        var html = "<html><body><script>console.log('x')</script>Merhaba  Dünya</body></html>";

        var text = extractor.ExtractText(html);

        // Verifies the expected behavior for this test scenario.
        Assert.Equal("Merhaba Dünya", text);
    }
}
