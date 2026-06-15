// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Entities/CrawlRun.cs for maintainers.
using webapi_oyako.Domain.Enums;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Domain.Entities;

// Implements the CrawlRun component and its responsibilities in the Oyako codebase.
public sealed class CrawlRun
{
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int Id { get; set; }
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public DateTime StartedAtUtc { get; set; }
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public DateTime? CompletedAtUtc { get; set; }
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public CrawlRunStatus Status { get; set; } = CrawlRunStatus.Running;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int PageCount { get; set; }
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int ErrorCount { get; set; }
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int WarningCount { get; set; }
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string? ErrorMessage { get; set; }
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string? WarningMessage { get; set; }
}
