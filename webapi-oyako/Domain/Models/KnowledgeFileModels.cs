// Codex developer note: Defines strongly typed models for knowledge-file parsing, storage, and imports.
using webapi_oyako.Domain.Entities;

namespace webapi_oyako.Domain.Models;

// Carries normalized parse output for a single uploaded file.
public sealed record ParsedKnowledgeFile(
    string FileName,
    string Extension,
    string Content,
    string ContentPreview,
    string ParseStatus,
    string OcrStatus,
    string FileHash,
    string ContentHash,
    long FileSizeBytes);

// Carries a local document identity resolved from DB before an upsert.
public sealed record LocalDocumentIdentity(
    int? DocumentId,
    string TenantGuid,
    string TenantKnowledgeGuid,
    string KnowledgeSourceGuid,
    string SourceFolderGuid,
    string FolderDocumentGuid,
    string StorageDirectory,
    string StoredFileName);

// Carries the data required to insert or update a local document row.
public sealed record LocalDocumentUpsert(
    int SourceId,
    string TenantGuid,
    string TenantKnowledgeGuid,
    string KnowledgeSourceGuid,
    string SourceFolderGuid,
    string FolderDocumentGuid,
    string Title,
    string WebSourceUrl,
    string Content,
    string ContentPreview,
    string OriginalFileName,
    string NormalizedFolderPath,
    string NormalizedRelativePath,
    string StorageDirectory,
    string StoredFileName,
    string FileExtension,
    long FileSizeBytes,
    string FileHash,
    string ContentHash,
    string ParseStatus,
    string OcrStatus,
    string Origin);

// Carries a preview item returned by the upload preview workflow.
public sealed record KnowledgeFilePreviewItem(
    string ClientFileId,
    string FileName,
    string RelativePath,
    string DefaultTitle,
    string Content,
    string ContentPreview,
    string ParseStatus,
    string OcrStatus,
    string? ErrorMessage);

// Carries the result of a file preview request.
public sealed record KnowledgeFilePreviewResult(
    IReadOnlyList<KnowledgeFilePreviewItem> Items,
    IReadOnlyList<string> Messages);

// Carries one failed import item without exposing stack traces.
public sealed record KnowledgeFileImportFailure(
    string FileName,
    string RelativePath,
    string Message);

// Carries the result of a local file import operation.
public sealed record KnowledgeFileImportResult(
    int ImportedCount,
    int UpdatedCount,
    int SkippedCount,
    IReadOnlyList<KnowledgeFileImportFailure> FailedItems);

// Carries manifest information for one document in the hierarchical raw-file archive.
public sealed record KnowledgeDocumentManifest(
    string TenantGuid,
    string TenantKnowledgeGuid,
    string KnowledgeSourceGuid,
    string SourceFolderGuid,
    string FolderDocumentGuid,
    string SourceName,
    string SourceDescription,
    string FolderName,
    string NormalizedFolderPath,
    string DocumentTitle,
    string OriginalFileName,
    string NormalizedRelativePath,
    string StoredFileName,
    string FileExtension,
    long FileSizeBytes,
    string FileHash,
    string ContentHash,
    string ParseStatus,
    string OcrStatus,
    string Origin,
    DateTime UpdatedAtUtc);
