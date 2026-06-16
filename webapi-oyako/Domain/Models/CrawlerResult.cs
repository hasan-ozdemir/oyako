// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Models/CrawlerResult.cs for maintainers.
using webapi_oyako.Domain.Entities;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Domain.Models;

// Defines the immutable CrawlerResult data shape exchanged between Oyako components.
public sealed record CrawlerResult(
    bool IsSuccessful,
    IReadOnlyCollection<WebPage> Pages,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    DateTimeOffset CompletedAtUtc);
