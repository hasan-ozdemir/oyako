// Codex developer note: Carries prebuilt prompt blocks and citation metadata for fast active-cache recomposition.
namespace webapi_oyako.Domain.Models;

// Represents a reusable document prompt block that can be switched in or out without re-scraping or LLM work.
public sealed class KnowledgeDocumentCacheBlock
{
    // Identifies the knowledge document row that owns this cached block.
    public int DocumentId { get; set; }
    // Identifies the top-level knowledge source that owns the document.
    public int SourceId { get; set; }
    // Stores the display name of the source for citation rendering.
    public string SourceName { get; set; } = string.Empty;
    // Stores whether the document came from a web site or local files.
    public string SourceType { get; set; } = string.Empty;
    // Stores the document title, web page title, or uploaded file name.
    public string DocumentTitle { get; set; } = string.Empty;
    // Stores the canonical URL or local document URI.
    public string DocumentUrl { get; set; } = string.Empty;
    // Stores the exact label the LLM is allowed to cite.
    public string DocumentCitationLabel { get; set; } = string.Empty;
    // Stores the content hash used to detect stale blocks.
    public string ContentHash { get; set; } = string.Empty;
    // Stores the full prompt block injected into the system instruction.
    public string PromptBlock { get; set; } = string.Empty;
    // Stores when this block was last generated.
    public DateTime UpdatedAtUtc { get; set; }
}
