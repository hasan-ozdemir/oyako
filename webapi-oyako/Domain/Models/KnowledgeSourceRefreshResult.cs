// Codex developer note: Defines source-refresh result contracts for background web knowledge updates.
namespace webapi_oyako.Domain.Models;

// Describes the refresh outcome for one web-site knowledge source.
public sealed record KnowledgeSourceRefreshResult(
    int SourceId,
    string SourceName,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    string Status,
    int AddedCount,
    int UpdatedCount,
    int DeletedCount,
    int UnchangedCount,
    int FailedDocumentCount,
    bool Changed,
    string Message);

// Describes one full background source-refresh run.
public sealed record KnowledgeSourceRefreshRunResult(
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    string Status,
    int SourceCount,
    int AddedCount,
    int UpdatedCount,
    int DeletedCount,
    int UnchangedCount,
    int FailedDocumentCount,
    bool CacheActivated,
    IReadOnlyList<KnowledgeSourceRefreshResult> Sources,
    string Message);
