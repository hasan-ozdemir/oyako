// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Entities/SystemInstructionCacheEntry.cs for maintainers.
namespace webapi_oyako.Domain.Entities;

// Implements the SystemInstructionCacheEntry component and its responsibilities in the Oyako codebase.
public sealed class SystemInstructionCacheEntry
{
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string CacheKey { get; set; } = string.Empty;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string Content { get; set; } = string.Empty;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string ContentHash { get; set; } = string.Empty;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string SourceFingerprint { get; set; } = string.Empty;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int PageCount { get; set; }
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public DateTime BuiltAtUtc { get; set; }
}
