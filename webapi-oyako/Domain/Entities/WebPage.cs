// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Entities/WebPage.cs for maintainers.
namespace webapi_oyako.Domain.Entities;

// Represents a knowledge document, whether it came from a crawled web page or a local file.
public sealed class WebPage
{
    // Identifies the document row inside SQLite.
    public int Id { get; set; }
    // References the owning source row.
    public int? SourceId { get; set; }
    // Stores the source name projected for UI display.
    public string? SourceName { get; set; }
    // Stores the source type projected for UI display.
    public string SourceType { get; set; } = KnowledgeSourceTypes.WebSite;
    // Stores the tenant-level GUID used by the hierarchical storage path.
    public string TenantGuid { get; set; } = string.Empty;
    // Stores the knowledge-bank GUID used by the hierarchical storage path.
    public string TenantKnowledgeGuid { get; set; } = string.Empty;
    // Stores the source GUID used by manifests and raw-file storage.
    public string KnowledgeSourceGuid { get; set; } = string.Empty;
    // Stores the source folder GUID for local file collections.
    public string SourceFolderGuid { get; set; } = string.Empty;
    // Stores the folder document GUID for local file documents.
    public string FolderDocumentGuid { get; set; } = string.Empty;
    // Stores the canonical URL or local document URI.
    public string WebSourceUrl { get; set; } = string.Empty;
    // Stores the document title.
    public string? WebTitle { get; set; }
    // Stores the normalized text used by the LLM.
    public string WebContent { get; set; } = string.Empty;
    // Stores the user-friendly preview text.
    public string ContentPreview { get; set; } = string.Empty;
    // Controls whether the document contributes to Q&A answers.
    public bool IsEnabled { get; set; } = true;
    // Controls whether the document is hidden from active use without deleting it.
    public bool IsArchived { get; set; }
    // Stores the machine-readable document status code.
    public string StatusCode { get; set; } = "ok";
    // Stores the Turkish document status label shown to users.
    public string StatusLabel { get; set; } = "Tamam";
    // Stores the detailed document status message.
    public string StatusMessage { get; set; } = "Belge kullanılabilir.";
    // Stores the HTTP status code for web documents.
    public int? HttpStatusCode { get; set; }
    // Stores the preview generation strategy or status.
    public string PreviewStatus { get; set; } = "deterministic";
    // Stores when the preview was generated.
    public DateTime? PreviewGeneratedAtUtc { get; set; }
    // Stores when the document was last checked.
    public DateTime? LastCheckedAtUtc { get; set; }
    // Stores the normalized content hash used for cache fingerprinting.
    public string ContentHash { get; set; } = string.Empty;
    // Stores the first-seen timestamp.
    public DateTime FirstSeenAtUtc { get; set; }
    // Stores the last-seen timestamp.
    public DateTime LastSeenAtUtc { get; set; }
    // Stores the latest crawl/import timestamp.
    public DateTime LastCrawledAtUtc { get; set; }
    // Stores the original uploaded file name for local documents.
    public string OriginalFileName { get; set; } = string.Empty;
    // Stores the normalized relative path used for local duplicate prevention.
    public string NormalizedRelativePath { get; set; } = string.Empty;
    // Stores the normalized folder path used for local folder grouping.
    public string NormalizedFolderPath { get; set; } = string.Empty;
    // Stores the raw file storage directory.
    public string StorageDirectory { get; set; } = string.Empty;
    // Stores the safe stored file name.
    public string StoredFileName { get; set; } = string.Empty;
    // Stores the uploaded file extension.
    public string FileExtension { get; set; } = string.Empty;
    // Stores the uploaded file size in bytes.
    public long FileSizeBytes { get; set; }
    // Stores the raw file hash.
    public string FileHash { get; set; } = string.Empty;
    // Stores the parse status for local documents.
    public string ParseStatus { get; set; } = string.Empty;
    // Stores the OCR status for local documents.
    public string OcrStatus { get; set; } = string.Empty;
    // Stores the document origin such as web_crawl, manual_web_link, or local_file_upload.
    public string Origin { get; set; } = "web_crawl";
}
