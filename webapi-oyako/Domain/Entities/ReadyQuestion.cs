// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Entities/ReadyQuestion.cs for maintainers.
using webapi_oyako.Domain.Models;

namespace webapi_oyako.Domain.Entities;

// Implements the ReadyQuestion component and its responsibilities in the Oyako codebase.
public sealed class ReadyQuestion
{
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int Id { get; set; }
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string Text { get; set; } = string.Empty;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string SourceFingerprint { get; set; } = string.Empty;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public IReadOnlyList<ReadyQuestionDocumentReference> DocumentReferences { get; set; } = Array.Empty<ReadyQuestionDocumentReference>();
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int ServedCount { get; set; }
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public DateTime? LastServedAtUtc { get; set; }
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public DateTime CreatedAtUtc { get; set; }
}
