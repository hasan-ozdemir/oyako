// Codex developer note: Explains the purpose and flow of webapi-oyako/Application/Services/AnswerHtmlSanitizer.cs for maintainers.
using System.Text.RegularExpressions;
using Ganss.Xss;
using Markdig;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Services;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Application.Services;

public sealed partial class AnswerHtmlSanitizer : IAnswerHtmlSanitizer
{
    // Executes this component behavior as part of the Oyako application flow.
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder().Build();

    // Stores state or a dependency required by the surrounding component.
    private readonly HtmlSanitizer _sanitizer;
    // Stores state or a dependency required by the surrounding component.
    private readonly IAnswerActionLinkifier _answerActionLinkifier;

    // Creates a new instance and captures the dependencies needed by this component.
    public AnswerHtmlSanitizer(IAnswerActionLinkifier answerActionLinkifier)
    {
        _answerActionLinkifier = answerActionLinkifier;
        _sanitizer = CreateSanitizer();
    }

    // Executes this component behavior as part of the Oyako application flow.
    public ChatAnswerSnapshot RenderAssistantMarkdown(string markdown, int suggestedQuestionLimit = 5, bool enableActionLinks = true)
    {
        var parsed = ExtractSuggestions(StripCodeFence(markdown).Trim(), suggestedQuestionLimit);
        var html = Markdown.ToHtml(parsed.BodyMarkdown, MarkdownPipeline);
        var linkified = enableActionLinks ? _answerActionLinkifier.Linkify(html) : html;
        var sanitized = _sanitizer.Sanitize(linkified).Trim();

        // Guards the following branch so the workflow handles this condition deliberately.
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "<p>Yanıt hazırlanıyor...</p>";
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return new ChatAnswerSnapshot(sanitized, parsed.Questions, Array.Empty<SourceAttribution>());
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static HtmlSanitizer CreateSanitizer()
    {
        // Creates the object needed for the next step of the workflow.
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        // Iterates through the collection to process each item consistently.
        foreach (var tag in new[]
        {
            "article", "section", "header", "div", "span", "p", "br",
            "strong", "b", "em", "i", "u", "small",
            "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "blockquote",
            "table", "thead", "tbody", "tr", "th", "td",
            "a", "pre", "code"
        })
        {
            // Registers or maps application behavior into the runtime pipeline.
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        // Iterates through the collection to process each item consistently.
        foreach (var attribute in new[]
        {
            "class", "role", "aria-label", "aria-live",
            "href", "target", "rel"
        })
        {
            // Registers or maps application behavior into the runtime pipeline.
            sanitizer.AllowedAttributes.Add(attribute);
        }

        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedSchemes.Clear();
        // Registers or maps application behavior into the runtime pipeline.
        sanitizer.AllowedSchemes.Add("http");
        // Registers or maps application behavior into the runtime pipeline.
        sanitizer.AllowedSchemes.Add("https");
        // Registers or maps application behavior into the runtime pipeline.
        sanitizer.AllowedSchemes.Add("mailto");
        // Registers or maps application behavior into the runtime pipeline.
        sanitizer.AllowedSchemes.Add("tel");
        // Registers or maps application behavior into the runtime pipeline.
        sanitizer.AllowedSchemes.Add("sms");

        // Returns the computed result to the caller and completes this branch of the workflow.
        return sanitizer;
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static ParsedMarkdown ExtractSuggestions(string markdown, int suggestedQuestionLimit)
    {
        // Guards the following branch so the workflow handles this condition deliberately.
        if (string.IsNullOrWhiteSpace(markdown))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return new ParsedMarkdown(string.Empty, Array.Empty<string>());
        }

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var headingIndex = Array.FindIndex(lines, line => SuggestionsHeadingRegex().IsMatch(line.Trim()));
        // Guards the following branch so the workflow handles this condition deliberately.
        if (headingIndex < 0)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return new ParsedMarkdown(RemoveSourceLine(markdown), Array.Empty<string>());
        }

        var bodyMarkdown = RemoveSourceLine(string.Join('\n', lines.Take(headingIndex)).Trim());
        var questions = lines
            .Skip(headingIndex + 1)
            .Select(ParseSuggestionLine)
            .Where(question => !string.IsNullOrWhiteSpace(question))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(suggestedQuestionLimit, 1, 10))
            .ToArray();

        // Returns the computed result to the caller and completes this branch of the workflow.
        return new ParsedMarkdown(bodyMarkdown, questions);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string ParseSuggestionLine(string line)
    {
        var match = SuggestionLineRegex().Match(line);
        // Guards the following branch so the workflow handles this condition deliberately.
        if (!match.Success)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return string.Empty;
        }

        var question = match.Groups["question"].Value.Trim();
        question = MarkdownLinkRegex().Replace(question, "$1");
        question = MarkdownEmphasisRegex().Replace(question, string.Empty);
        question = question.Trim(' ', '"', '\'', '`', '-', '*');
        // Returns the computed result to the caller and completes this branch of the workflow.
        return question;
    }

    private static string RemoveSourceLine(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n').ToList();
        for (var index = lines.Count - 1; index >= 0; index--)
        {
            if (SourceLineRegex().IsMatch(lines[index].Trim()))
            {
                lines.RemoveAt(index);
                break;
            }
        }

        return string.Join('\n', lines).Trim();
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string StripCodeFence(string rawHtml)
    {
        var value = rawHtml.Trim();
        // Guards the following branch so the workflow handles this condition deliberately.
        if (!value.StartsWith("```", StringComparison.Ordinal))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return rawHtml;
        }

        var firstLineEnd = value.IndexOf('\n');
        // Guards the following branch so the workflow handles this condition deliberately.
        if (firstLineEnd < 0)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return string.Empty;
        }

        value = value[(firstLineEnd + 1)..];
        var fenceStart = value.LastIndexOf("```", StringComparison.Ordinal);
        // Returns the computed result to the caller and completes this branch of the workflow.
        return fenceStart >= 0 ? value[..fenceStart] : value;
    }

    [GeneratedRegex(@"^#{1,6}\s*(?:[Öö]nerilen|[Oo]nerilen)\s+sorular\s*:?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    // Executes this component behavior as part of the Oyako application flow.
    private static partial Regex SuggestionsHeadingRegex();

    [GeneratedRegex(@"^\s*(?:\*\*|__)?\s*[Kk]aynak\s*(?:\*\*|__)?\s*:\s*(?:\*\*|__)?\s*.+$", RegexOptions.CultureInvariant)]
    private static partial Regex SourceLineRegex();

    [GeneratedRegex(@"^\s*(?:[-*+]\s+|\d+[\.)]\s+)(?<question>.+?)\s*$")]
    // Executes this component behavior as part of the Oyako application flow.
    private static partial Regex SuggestionLineRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)")]
    // Executes this component behavior as part of the Oyako application flow.
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"[*_~]")]
    // Executes this component behavior as part of the Oyako application flow.
    private static partial Regex MarkdownEmphasisRegex();

    // Executes this component behavior as part of the Oyako application flow.
    private sealed record ParsedMarkdown(string BodyMarkdown, IReadOnlyList<string> Questions);
}
