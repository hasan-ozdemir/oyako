// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Models/ChatAnswerSnapshot.cs for maintainers.
namespace webapi_oyako.Domain.Models;

// Defines the immutable ChatAnswerSnapshot data shape exchanged between Oyako components.
public sealed record ChatAnswerSnapshot(
    string AnswerContent,
    IReadOnlyList<string> SuggestedQuestions,
    IReadOnlyList<SourceAttribution> SourceAttributions);
