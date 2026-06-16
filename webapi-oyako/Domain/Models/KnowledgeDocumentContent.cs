// Codex developer note: Returns normalized document text for the in-app DocumentViewer.
namespace webapi_oyako.Domain.Models;

// Carries display metadata and pure text content for a Knowledge Bank document.
public sealed record KnowledgeDocumentContent(
    int Id,
    int? SourceId,
    string SourceName,
    string SourceType,
    string Title,
    string Url,
    string Content,
    string OriginalFileName,
    DateTime? LastCheckedAtUtc,
    DateTime LastCrawledAtUtc);
