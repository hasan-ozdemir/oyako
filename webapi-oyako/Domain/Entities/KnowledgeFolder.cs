// Codex developer note: Defines folder metadata for local-file knowledge sources.
namespace webapi_oyako.Domain.Entities;

// Represents a logical folder beneath a local knowledge source.
public sealed class KnowledgeFolder
{
    // Identifies the folder row inside SQLite.
    public int Id { get; set; }
    // Stores the tenant GUID for storage hierarchy reconstruction.
    public string TenantGuid { get; set; } = string.Empty;
    // Stores the knowledge-bank GUID for storage hierarchy reconstruction.
    public string TenantKnowledgeGuid { get; set; } = string.Empty;
    // Stores the source GUID for storage hierarchy reconstruction.
    public string KnowledgeSourceGuid { get; set; } = string.Empty;
    // Stores the folder GUID used in raw-file paths.
    public string SourceFolderGuid { get; set; } = string.Empty;
    // Stores the folder label shown in management views.
    public string FolderName { get; set; } = string.Empty;
    // Stores the normalized folder path used for duplicate prevention.
    public string NormalizedFolderPath { get; set; } = string.Empty;
    // Stores the creation timestamp.
    public DateTime CreatedAtUtc { get; set; }
    // Stores the latest update timestamp.
    public DateTime UpdatedAtUtc { get; set; }
}
