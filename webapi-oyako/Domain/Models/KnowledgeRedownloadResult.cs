// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Models/KnowledgeRedownloadResult.cs for maintainers.
namespace webapi_oyako.Domain.Models;

// Defines the immutable KnowledgeRedownloadResult data shape exchanged between Oyako components.
public sealed record KnowledgeRedownloadResult(
    string Status,
    string BackupSetId,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    int PageCount,
    int WarningCount,
    int ErrorCount,
    int ReadyQuestionsCount,
    string? SourceFingerprint,
    DateTime? CacheBuiltAtUtc,
    bool RestoredFromBackup,
    string Message);
