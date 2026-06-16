// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Models/ReadyQuestionSet.cs for maintainers.
namespace webapi_oyako.Domain.Models;

// Defines the immutable ReadyQuestionSet data shape exchanged between Oyako components.
public sealed record ReadyQuestionSet(
    IReadOnlyList<string> Questions,
    string Source,
    DateTime? GeneratedAtUtc,
    int TotalAvailable,
    string? SourceFingerprint,
    bool IsRefreshing);
