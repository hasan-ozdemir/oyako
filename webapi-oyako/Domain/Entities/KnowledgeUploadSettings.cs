// Codex developer note: Defines configurable upload limits for local knowledge files.
namespace webapi_oyako.Domain.Entities;

// Represents the persistent file upload limits managed from the Settings UI.
public sealed class KnowledgeUploadSettings
{
    // Stores the maximum allowed size for a single file in megabytes.
    public int MaxFileSizeMb { get; set; } = 25;
    // Stores the maximum allowed file count for one batch.
    public int MaxBatchFileCount { get; set; } = 100;
    // Stores the maximum allowed total batch size in megabytes.
    public int MaxBatchSizeMb { get; set; } = 250;
    // Stores the latest update timestamp.
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
