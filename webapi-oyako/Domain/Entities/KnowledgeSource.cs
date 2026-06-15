// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Entities/KnowledgeSource.cs for maintainers.
namespace webapi_oyako.Domain.Entities;

// Represents a top-level knowledge source managed by Oyako.
public sealed class KnowledgeSource
{
    // Identifies the source row inside SQLite.
    public int Id { get; set; }
    // Stores the tenant-level GUID used by the hierarchical storage path.
    public string TenantGuid { get; set; } = string.Empty;
    // Stores the knowledge-bank GUID used by the hierarchical storage path.
    public string TenantKnowledgeGuid { get; set; } = string.Empty;
    // Stores the source GUID used by manifests and raw-file storage.
    public string KnowledgeSourceGuid { get; set; } = string.Empty;
    // Distinguishes automatic web sites, manual web links, and local file sources.
    public string SourceType { get; set; } = KnowledgeSourceTypes.WebSite;
    // Stores the human-readable source name shown in the UI.
    public string Name { get; set; } = string.Empty;
    // Stores the optional source description shown in the UI.
    public string Description { get; set; } = string.Empty;
    // Stores the canonical web address or local source URI.
    public string Address { get; set; } = string.Empty;
    // Stores the source protocol such as https or local.
    public string Protocol { get; set; } = string.Empty;
    // Controls whether the source contributes to Q&A answers.
    public bool IsEnabled { get; set; } = true;
    // Controls whether the source is hidden from active use without deleting it.
    public bool IsArchived { get; set; }
    // Stores the machine-readable source status code.
    public string StatusCode { get; set; } = "ok";
    // Stores the Turkish source status label shown to users.
    public string StatusLabel { get; set; } = "Tamam";
    // Stores the detailed source status message.
    public string StatusMessage { get; set; } = "Kaynak kullanılabilir.";
    // Stores the last time the source was checked.
    public DateTime? LastCheckedAtUtc { get; set; }
    // Stores the creation timestamp.
    public DateTime CreatedAtUtc { get; set; }
    // Stores the latest update timestamp.
    public DateTime UpdatedAtUtc { get; set; }
    // Stores the total number of documents connected to this source.
    public int DocumentCount { get; set; }
    // Stores the active document count used for Q&A.
    public int ActiveDocumentCount { get; set; }
}
