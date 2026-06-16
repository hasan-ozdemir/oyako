// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Crawling/RenderedPage.cs for maintainers.
namespace webapi_oyako.Infrastructure.Crawling;

// Defines the immutable RenderedPage data shape exchanged between Oyako components.
public sealed record RenderedPage(
    string Url,
    string? Title,
    string? FirstHeadingTitle,
    string Text,
    IReadOnlyList<string> Links);
