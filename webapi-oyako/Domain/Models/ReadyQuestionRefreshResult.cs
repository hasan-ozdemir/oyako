// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Models/ReadyQuestionRefreshResult.cs for maintainers.
namespace webapi_oyako.Domain.Models;

// Defines the immutable ReadyQuestionRefreshResult data shape exchanged between Oyako components.
public sealed record ReadyQuestionRefreshResult(
    bool Succeeded,
    int QuestionCount,
    string? SourceFingerprint);
