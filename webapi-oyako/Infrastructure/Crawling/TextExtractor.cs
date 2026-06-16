// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Crawling/TextExtractor.cs for maintainers.
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Infrastructure.Crawling;

// Declares the ITextExtractor contract used to decouple Oyako layers.
public interface ITextExtractor
{
    string ExtractText(string html);
    IReadOnlyList<string> ExtractLinks(string html);
}

// Implements the RenderedTextExtractor component and its responsibilities in the Oyako codebase.
public sealed class RenderedTextExtractor : ITextExtractor
{
    // Executes this component behavior as part of the Oyako application flow.
    private static readonly Regex WhitespacePattern = new(@"[\r\n\t]+", RegexOptions.Compiled);

    // Executes this component behavior as part of the Oyako application flow.
    public string ExtractText(string html)
    {
        // Creates the object needed for the next step of the workflow.
        var document = new HtmlDocument();
        document.LoadHtml(html);

        // Iterates through the collection to process each item consistently.
        foreach (var node in document.DocumentNode.Descendants("script").ToList())
        {
            node.Remove();
        }

        // Iterates through the collection to process each item consistently.
        foreach (var node in document.DocumentNode.Descendants("style").ToList())
        {
            node.Remove();
        }

        var text = document.DocumentNode.InnerText ?? string.Empty;
        text = HtmlEntity.DeEntitize(text);
        text = WhitespacePattern.Replace(text, " ");
        text = Regex.Replace(text, @"\s{2,}", " ");
        // Returns the computed result to the caller and completes this branch of the workflow.
        return text.Trim();
    }

    // Executes this component behavior as part of the Oyako application flow.
    public IReadOnlyList<string> ExtractLinks(string html)
    {
        // Creates the object needed for the next step of the workflow.
        var list = new List<string>();
        // Creates the object needed for the next step of the workflow.
        var document = new HtmlDocument();
        document.LoadHtml(html);
        // Creates the object needed for the next step of the workflow.
        var links = document.DocumentNode.SelectNodes("//a[@href]")?.ToList() ?? new List<HtmlNode>();

        // Iterates through the collection to process each item consistently.
        foreach (var link in links)
        {
            var href = link.GetAttributeValue("href", string.Empty);
            // Guards the following branch so the workflow handles this condition deliberately.
            if (!string.IsNullOrWhiteSpace(href))
            {
                // Registers or maps application behavior into the runtime pipeline.
                list.Add(href);
            }
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return list;
    }
}
