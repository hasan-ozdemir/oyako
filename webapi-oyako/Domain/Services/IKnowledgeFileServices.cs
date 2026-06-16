// Codex developer note: Declares local knowledge-file workflow contracts used by the application layer.
using Microsoft.AspNetCore.Http;
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Models;
using webapi_oyako.Presentation.Api;

namespace webapi_oyako.Domain.Services;

// Parses uploaded files into normalized text and previews.
public interface IKnowledgeFileParser
{
    // Parses one file stream into clean knowledge text.
    Task<ParsedKnowledgeFile> ParseAsync(Stream stream, string fileName, CancellationToken cancellationToken);
}

// Performs immediate system instruction cache activation after knowledge mutations.
public interface IKnowledgeActivationService
{
    // Rebuilds only the active system instruction cache before a lightweight mutation endpoint returns success.
    Task ActivateCacheOnlyAsync(CancellationToken cancellationToken);

    // Rebuilds the active cache and queues ready-question regeneration without blocking the mutation endpoint.
    Task ActivateCacheAndQueueReadyQuestionsAsync(CancellationToken cancellationToken);
}

// Handles preview, import, deletion, and rebuild workflows for local knowledge files.
public interface IKnowledgeFileImportService
{
    // Parses selected files and returns card previews without persisting raw files.
    Task<KnowledgeFilePreviewResult> PreviewAsync(IReadOnlyList<IFormFile> files, IReadOnlyList<string> relativePaths, CancellationToken cancellationToken);

    // Imports files into the hierarchical raw-file archive and upserts DB documents.
    Task<KnowledgeFileImportResult> ImportAsync(int sourceId, IReadOnlyList<IFormFile> files, IReadOnlyList<string> titles, IReadOnlyList<string> relativePaths, CancellationToken cancellationToken);

    // Deletes raw files connected to one source.
    Task DeleteSourceFilesAsync(int sourceId, CancellationToken cancellationToken);

    // Deletes the raw file connected to one document.
    Task DeleteDocumentFileAsync(int documentId, CancellationToken cancellationToken);
}

// Stores and retrieves upload limit settings.
public interface IKnowledgeUploadSettingsService
{
    // Reads the active upload limit settings.
    Task<KnowledgeUploadSettings> GetAsync(CancellationToken cancellationToken);

    // Persists upload limit settings after validation.
    Task<KnowledgeUploadSettings> UpdateAsync(int maxFileSizeMb, int maxBatchFileCount, int maxBatchSizeMb, CancellationToken cancellationToken);
}

// Rebuilds missing local DB records from the raw-file manifest hierarchy.
public interface ILocalKnowledgeRebuildService
{
    // Recreates missing local knowledge rows from manifests and raw files.
    Task<int> RebuildMissingAsync(CancellationToken cancellationToken);

    // Recreates all database documents for one local-files source from the raw-file archive.
    Task<int> RedownloadSourceAsync(int sourceId, CancellationToken cancellationToken);

    // Recreates one database document from its archived raw file.
    Task<bool> RedownloadDocumentAsync(int documentId, CancellationToken cancellationToken);
}
