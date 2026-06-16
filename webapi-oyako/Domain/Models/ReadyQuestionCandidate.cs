// Codex developer note: Represents a generated ready question tied to one or more source documents.
namespace webapi_oyako.Domain.Models;

// Carries a ready question and the exact document identities it was generated from.
public sealed record ReadyQuestionCandidate(
    string Text,
    IReadOnlyList<ReadyQuestionDocumentReference> DocumentReferences);

// Carries the source document identity behind one generated ready question.
public sealed record ReadyQuestionDocumentReference(
    int SourceId,
    int DocumentId,
    string DocumentContentHash);
