// Codex developer note: Implements supporting services for knowledge activation, upload settings, and local rebuild.
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Enums;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Domain.Services;

namespace webapi_oyako.Application.Services;

// Rebuilds active system instructions after lightweight knowledge mutations without running heavy redownload workflows.
public sealed class KnowledgeActivationService : IKnowledgeActivationService
{
    private readonly ISystemInstructionCache _systemInstructionCache;
    private readonly IReadyQuestionService _readyQuestionService;
    private readonly IRuntimeStatusService _runtimeStatusService;

    public KnowledgeActivationService(
        ISystemInstructionCache systemInstructionCache,
        IReadyQuestionService readyQuestionService,
        IRuntimeStatusService runtimeStatusService)
    {
        _systemInstructionCache = systemInstructionCache;
        _readyQuestionService = readyQuestionService;
        _runtimeStatusService = runtimeStatusService;
    }

    // Activates the latest DB knowledge cache before lightweight mutation endpoints return success.
    public async Task ActivateCacheOnlyAsync(CancellationToken cancellationToken)
    {
        await _runtimeStatusService.PublishAsync(
            "knowledge_activation",
            "cache_building",
            "cache_refreshing",
            1,
            2,
            false,
            "bilgiler önbellekleniyor",
            "info",
            "cache",
            cancellationToken: cancellationToken);
        await _systemInstructionCache.ForceRefreshAsync(cancellationToken);
        await _runtimeStatusService.PublishAsync(
            "knowledge_activation",
            "ready_for_question",
            "completed",
            2,
            2,
            true,
            "Uygulama Hazır",
            "ready",
            "message",
            cancellationToken: cancellationToken);
    }

    // Activates the latest DB knowledge cache and schedules ready-question refresh without blocking the caller.
    public async Task ActivateCacheAndQueueReadyQuestionsAsync(CancellationToken cancellationToken)
    {
        await ActivateCacheOnlyAsync(cancellationToken);
        _readyQuestionService.QueueRefreshFromKnowledge();
    }
}

// Persists and validates file upload limit settings.
public sealed class KnowledgeUploadSettingsService : IKnowledgeUploadSettingsService
{
    private readonly IWebPageRepository _repository;

    public KnowledgeUploadSettingsService(IWebPageRepository repository)
    {
        _repository = repository;
    }

    // Reads the active upload settings.
    public Task<KnowledgeUploadSettings> GetAsync(CancellationToken cancellationToken)
    {
        return _repository.GetUploadSettingsAsync(cancellationToken);
    }

    // Validates and stores upload settings.
    public Task<KnowledgeUploadSettings> UpdateAsync(int maxFileSizeMb, int maxBatchFileCount, int maxBatchSizeMb, CancellationToken cancellationToken)
    {
        if (maxFileSizeMb <= 0 || maxBatchFileCount <= 0 || maxBatchSizeMb <= 0)
        {
            throw new InvalidOperationException("Dosya yükleme limitleri pozitif değerler olmalıdır.");
        }

        return _repository.UpdateUploadSettingsAsync(
            new KnowledgeUploadSettings
            {
                MaxFileSizeMb = maxFileSizeMb,
                MaxBatchFileCount = maxBatchFileCount,
                MaxBatchSizeMb = maxBatchSizeMb,
                UpdatedAtUtc = DateTime.UtcNow
            },
            cancellationToken);
    }
}

// Rebuilds missing local knowledge data from the raw-file manifest area.
public sealed class LocalKnowledgeRebuildService : ILocalKnowledgeRebuildService
{
    private readonly string _dataRoot;
    private readonly IWebPageRepository _repository;
    private readonly IKnowledgeFileParser _parser;

    public LocalKnowledgeRebuildService(
        IHostEnvironment environment,
        IWebPageRepository repository,
        IKnowledgeFileParser parser)
    {
        _dataRoot = KnowledgeRawFileStorage.ResolveDataRoot(environment.ContentRootPath);
        _repository = repository;
        _parser = parser;
    }

    // Rebuilds local-file database rows from manifest-backed raw files without copying or duplicating raw files.
    public async Task<int> RebuildMissingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manifestRoot = Path.Combine(_dataRoot, ".manifest");
        if (!Directory.Exists(manifestRoot))
        {
            return 0;
        }

        var rebuiltCount = 0;
        foreach (var manifestPath in Directory.EnumerateFiles(manifestRoot, "document.json", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manifest = await ReadManifestAsync(manifestPath, cancellationToken);
            if (manifest is null)
            {
                continue;
            }

            if (await RebuildManifestAsync(manifest, cancellationToken))
            {
                rebuiltCount++;
            }
        }

        return rebuiltCount;
    }

    // Redownloads one local-files source by replaying its manifest-backed raw-file archive.
    public async Task<int> RedownloadSourceAsync(int sourceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = await _repository.GetSourceByIdAsync(sourceId, cancellationToken);
        if (source is null || source.SourceType != KnowledgeSourceTypes.LocalFiles)
        {
            return 0;
        }

        var manifestRoot = Path.Combine(_dataRoot, ".manifest");
        if (!Directory.Exists(manifestRoot))
        {
            await InvalidateMissingRawFilesAsync(sourceId, cancellationToken);
            return 0;
        }

        var rebuiltCount = 0;
        foreach (var manifestPath in Directory.EnumerateFiles(manifestRoot, "document.json", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manifest = await ReadManifestAsync(manifestPath, cancellationToken);
            if (manifest is null || !manifest.KnowledgeSourceGuid.Equals(source.KnowledgeSourceGuid, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (await RebuildManifestAsync(manifest, cancellationToken))
            {
                rebuiltCount++;
            }
        }

        await InvalidateMissingRawFilesAsync(sourceId, cancellationToken);
        return rebuiltCount;
    }

    // Redownloads one local document by reparsing the raw file already stored under the backend data archive.
    public async Task<bool> RedownloadDocumentAsync(int documentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var document = await _repository.GetDocumentByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            return false;
        }

        var rawFilePath = BuildDocumentRawFilePath(document);
        if (string.IsNullOrWhiteSpace(rawFilePath) || !File.Exists(rawFilePath))
        {
            await _repository.MarkDocumentInvalidAsync(
                documentId,
                "raw_file_missing",
                "Geçersiz",
                "Belgenin arşivdeki ham dosyası bulunamadı.",
                null,
                cancellationToken);
            return false;
        }

        if (document.SourceId is null)
        {
            await _repository.MarkDocumentInvalidAsync(
                documentId,
                "invalid_source",
                "Geçersiz",
                "Belge bağlı olduğu yerel dosya kaynağını kaybetmiş.",
                null,
                cancellationToken);
            return false;
        }

        var source = await _repository.GetSourceByIdAsync(document.SourceId.Value, cancellationToken);
        if (source is null)
        {
            await _repository.MarkDocumentInvalidAsync(
                documentId,
                "invalid_source",
                "Geçersiz",
                "Belgenin bağlı olduğu kaynak bulunamadı.",
                null,
                cancellationToken);
            return false;
        }

        var folder = await _repository.EnsureFolderAsync(
            source,
            document.SourceFolderGuid,
            BuildFolderName(document.NormalizedFolderPath),
            string.IsNullOrWhiteSpace(document.NormalizedFolderPath) ? "/" : document.NormalizedFolderPath,
            cancellationToken);
        await using var fileStream = File.OpenRead(rawFilePath);
        var parsed = await _parser.ParseAsync(fileStream, string.IsNullOrWhiteSpace(document.OriginalFileName) ? document.StoredFileName : document.OriginalFileName, cancellationToken);
        var folderDocumentGuid = string.IsNullOrWhiteSpace(document.FolderDocumentGuid) ? Guid.NewGuid().ToString("D") : document.FolderDocumentGuid;

        await _repository.UpsertLocalDocumentAsync(
            new LocalDocumentUpsert(
                source.Id,
                source.TenantGuid,
                source.TenantKnowledgeGuid,
                source.KnowledgeSourceGuid,
                folder.SourceFolderGuid,
                folderDocumentGuid,
                string.IsNullOrWhiteSpace(document.WebTitle) ? parsed.FileName : document.WebTitle,
                document.WebSourceUrl,
                parsed.Content,
                parsed.ContentPreview,
                string.IsNullOrWhiteSpace(document.OriginalFileName) ? parsed.FileName : document.OriginalFileName,
                string.IsNullOrWhiteSpace(document.NormalizedFolderPath) ? "/" : document.NormalizedFolderPath,
                string.IsNullOrWhiteSpace(document.NormalizedRelativePath) ? parsed.FileName : document.NormalizedRelativePath,
                Path.GetDirectoryName(rawFilePath) ?? string.Empty,
                Path.GetFileName(rawFilePath),
                parsed.Extension,
                parsed.FileSizeBytes,
                parsed.FileHash,
                parsed.ContentHash,
                parsed.ParseStatus,
                parsed.OcrStatus,
                string.IsNullOrWhiteSpace(document.Origin) ? "local_file" : document.Origin),
            cancellationToken);
        return true;
    }

    // Rebuilds one manifest-backed local document into the database.
    private async Task<bool> RebuildManifestAsync(KnowledgeDocumentManifest manifest, CancellationToken cancellationToken)
    {
        var rawFilePath = Path.Combine(
            _dataRoot,
            manifest.TenantGuid,
            manifest.TenantKnowledgeGuid,
            manifest.KnowledgeSourceGuid,
            manifest.SourceFolderGuid,
            manifest.FolderDocumentGuid,
            manifest.StoredFileName);
        if (!File.Exists(rawFilePath))
        {
            return false;
        }

        var source = await _repository.EnsureLocalSourceAsync(
            manifest.TenantGuid,
            manifest.TenantKnowledgeGuid,
            manifest.KnowledgeSourceGuid,
            manifest.SourceName,
            manifest.SourceDescription,
            cancellationToken);

        var folder = await _repository.EnsureFolderAsync(
            source,
            manifest.SourceFolderGuid,
            manifest.FolderName,
            manifest.NormalizedFolderPath,
            cancellationToken);
        var existingIdentity = await _repository.FindLocalDocumentIdentityAsync(
            source.Id,
            manifest.NormalizedFolderPath,
            manifest.NormalizedRelativePath,
            cancellationToken);

        await using var fileStream = File.OpenRead(rawFilePath);
        var parsed = await _parser.ParseAsync(fileStream, manifest.OriginalFileName, cancellationToken);
        var folderDocumentGuid = existingIdentity?.FolderDocumentGuid ?? manifest.FolderDocumentGuid;
        await _repository.UpsertLocalDocumentAsync(
            new LocalDocumentUpsert(
                source.Id,
                source.TenantGuid,
                source.TenantKnowledgeGuid,
                source.KnowledgeSourceGuid,
                folder.SourceFolderGuid,
                folderDocumentGuid,
                string.IsNullOrWhiteSpace(manifest.DocumentTitle) ? parsed.FileName : manifest.DocumentTitle,
                $"local-file://{source.KnowledgeSourceGuid}/{folderDocumentGuid}/{manifest.NormalizedRelativePath}",
                parsed.Content,
                parsed.ContentPreview,
                manifest.OriginalFileName,
                manifest.NormalizedFolderPath,
                manifest.NormalizedRelativePath,
                Path.GetDirectoryName(rawFilePath) ?? string.Empty,
                manifest.StoredFileName,
                parsed.Extension,
                parsed.FileSizeBytes,
                parsed.FileHash,
                parsed.ContentHash,
                parsed.ParseStatus,
                parsed.OcrStatus,
                string.IsNullOrWhiteSpace(manifest.Origin) ? "local_file" : manifest.Origin),
            cancellationToken);
        return true;
    }

    // Marks local documents invalid when their archived raw file is missing.
    private async Task InvalidateMissingRawFilesAsync(int sourceId, CancellationToken cancellationToken)
    {
        var documents = await _repository.GetDocumentsBySourceAsync(sourceId, cancellationToken);
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rawFilePath = BuildDocumentRawFilePath(document);
            if (!string.IsNullOrWhiteSpace(rawFilePath) && File.Exists(rawFilePath))
            {
                continue;
            }

            await _repository.MarkDocumentInvalidAsync(
                document.Id,
                "raw_file_missing",
                "Geçersiz",
                "Belgenin arşivdeki ham dosyası bulunamadı.",
                null,
                cancellationToken);
        }
    }

    // Resolves the raw-file path currently linked to a Knowledge Bank document row.
    private static string BuildDocumentRawFilePath(WebPage document)
    {
        return string.IsNullOrWhiteSpace(document.StorageDirectory) || string.IsNullOrWhiteSpace(document.StoredFileName)
            ? string.Empty
            : Path.Combine(document.StorageDirectory, document.StoredFileName);
    }

    // Creates a readable folder name from the normalized local source folder path.
    private static string BuildFolderName(string normalizedFolderPath)
    {
        var trimmed = normalizedFolderPath.Trim().Trim('/');
        return string.IsNullOrWhiteSpace(trimmed) ? "Kök Klasör" : trimmed;
    }

    // Reads a manifest file and shields startup from malformed manifest documents.
    private static async Task<KnowledgeDocumentManifest?> ReadManifestAsync(string manifestPath, CancellationToken cancellationToken)
    {
        try
        {
            await using var manifestStream = File.OpenRead(manifestPath);
            return await JsonSerializer.DeserializeAsync<KnowledgeDocumentManifest>(manifestStream, cancellationToken: cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}
