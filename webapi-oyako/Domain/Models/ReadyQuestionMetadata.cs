// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Models/ReadyQuestionMetadata.cs for maintainers.
namespace webapi_oyako.Domain.Models;

// Implements the ReadyQuestionMetadata component and its responsibilities in the Oyako codebase.
public sealed class ReadyQuestionMetadata
{
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int TotalAvailable { get; set; }
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string? SourceFingerprint { get; set; }
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public DateTime? GeneratedAtUtc { get; set; }
}
