// Codex developer note: Implements preview, import, raw storage, manifest, and delete workflows for local knowledge files.
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Domain.Services;

namespace webapi_oyako.Application.Services;

// Coordinates local-file import workflows for the knowledge bank.
public sealed class KnowledgeFileImportService : IKnowledgeFileImportService
{
    private readonly IWebPageRepository _repository;
    private readonly IKnowledgeFileParser _parser;
    private readonly IKnowledgeUploadSettingsService _settingsService;
    private readonly string _dataRoot;

    public KnowledgeFileImportService(
        IWebPageRepository repository,
        IKnowledgeFileParser parser,
        IKnowledgeUploadSettingsService settingsService,
        IHostEnvironment environment)
    {
        _repository = repository;
        _parser = parser;
        _settingsService = settingsService;
        _dataRoot = Path.Combine(environment.ContentRootPath, "data");
    }

    // Parses selected files without persisting raw bytes.
    public async Task<KnowledgeFilePreviewResult> PreviewAsync(IReadOnlyList<IFormFile> files, IReadOnlyList<string> relativePaths, CancellationToken cancellationToken)
    {
        await ValidateBatchAsync(files, cancellationToken);
        var items = new List<KnowledgeFilePreviewItem>();
        var messages = new List<string>();

        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            var relativePath = NormalizeRelativePath(GetIndexed(relativePaths, index) ?? file.FileName);
            try
            {
                await using var stream = file.OpenReadStream();
                var parsed = await _parser.ParseAsync(stream, file.FileName, cancellationToken);
                items.Add(new KnowledgeFilePreviewItem(
                    Guid.NewGuid().ToString("N"),
                    parsed.FileName,
                    relativePath,
                    Path.GetFileNameWithoutExtension(parsed.FileName),
                    parsed.Content,
                    parsed.ContentPreview,
                    parsed.ParseStatus,
                    parsed.OcrStatus,
                    null));
            }
            catch (Exception ex)
            {
                items.Add(new KnowledgeFilePreviewItem(
                    Guid.NewGuid().ToString("N"),
                    Path.GetFileName(file.FileName),
                    relativePath,
                    Path.GetFileNameWithoutExtension(file.FileName),
                    string.Empty,
                    "Bu dosya için önizleme oluşturulamadı.",
                    "parse_failed",
                    "not_started",
                    ex.Message));
                messages.Add($"{file.FileName}: {ex.Message}");
            }
        }

        return new KnowledgeFilePreviewResult(items, messages);
    }

    // Imports selected files into raw storage and upserts local documents.
    public async Task<KnowledgeFileImportResult> ImportAsync(int sourceId, IReadOnlyList<IFormFile> files, IReadOnlyList<string> titles, IReadOnlyList<string> relativePaths, CancellationToken cancellationToken)
    {
        await ValidateBatchAsync(files, cancellationToken);
        var source = await _repository.GetSourceByIdAsync(sourceId, cancellationToken)
            ?? throw new InvalidOperationException("Kaynak bulunamadı.");
        if (!source.SourceType.Equals(KnowledgeSourceTypes.LocalFiles, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Yeni belge yalnızca Yerel Dosyalar kaynaklarına eklenebilir.");
        }

        var imported = 0;
        var updated = 0;
        var skipped = 0;
        var failures = new List<KnowledgeFileImportFailure>();

        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            var normalizedRelativePath = NormalizeRelativePath(GetIndexed(relativePaths, index) ?? file.FileName);
            var normalizedFolderPath = NormalizeFolderPath(normalizedRelativePath);
            try
            {
                await using var parseStream = file.OpenReadStream();
                var parsed = await _parser.ParseAsync(parseStream, file.FileName, cancellationToken);
                if (string.IsNullOrWhiteSpace(parsed.Content))
                {
                    skipped++;
                    failures.Add(new KnowledgeFileImportFailure(file.FileName, normalizedRelativePath, "Dosyadan kullanılabilir metin çıkarılamadı."));
                    continue;
                }

                var folderName = normalizedFolderPath == "/" ? "Kök Klasör" : normalizedFolderPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
                var folder = await _repository.GetOrCreateFolderAsync(source, folderName, normalizedFolderPath, cancellationToken);
                var existing = await _repository.FindLocalDocumentIdentityAsync(source.Id, normalizedFolderPath, normalizedRelativePath, cancellationToken);
                var folderDocumentGuid = existing?.FolderDocumentGuid ?? Guid.NewGuid().ToString("D");
                var storageDirectory = existing?.StorageDirectory;
                if (string.IsNullOrWhiteSpace(storageDirectory))
                {
                    storageDirectory = KnowledgeRawFileStorage.BuildStorageDirectory(_dataRoot, source, folder.SourceFolderGuid, folderDocumentGuid);
                }

                await using var rawMemory = new MemoryStream();
                await using (var rawStream = file.OpenReadStream())
                {
                    await rawStream.CopyToAsync(rawMemory, cancellationToken);
                }

                var storedFileName = await KnowledgeRawFileStorage.ReplaceRawFileAsync(
                    storageDirectory,
                    parsed.FileName,
                    rawMemory.ToArray(),
                    cancellationToken);

                var title = string.IsNullOrWhiteSpace(GetIndexed(titles, index))
                    ? Path.GetFileNameWithoutExtension(parsed.FileName)
                    : GetIndexed(titles, index)!.Trim();
                var webSourceUrl = $"local://source/{source.KnowledgeSourceGuid}/folder/{folder.SourceFolderGuid}/document/{folderDocumentGuid}";
                await _repository.UpsertLocalDocumentAsync(
                    new LocalDocumentUpsert(
                        source.Id,
                        source.TenantGuid,
                        source.TenantKnowledgeGuid,
                        source.KnowledgeSourceGuid,
                        folder.SourceFolderGuid,
                        folderDocumentGuid,
                        title,
                        webSourceUrl,
                        parsed.Content,
                        parsed.ContentPreview,
                        parsed.FileName,
                        normalizedFolderPath,
                        normalizedRelativePath,
                        storageDirectory,
                        storedFileName,
                        parsed.Extension,
                        parsed.FileSizeBytes,
                        parsed.FileHash,
                        parsed.ContentHash,
                        parsed.ParseStatus,
                        parsed.OcrStatus,
                        "local_import"),
                    cancellationToken);

                await WriteDocumentManifestAsync(source, folder, folderDocumentGuid, title, parsed, normalizedFolderPath, normalizedRelativePath, storedFileName, cancellationToken);
                if (existing is null)
                {
                    imported++;
                }
                else
                {
                    updated++;
                }
            }
            catch (Exception ex)
            {
                skipped++;
                failures.Add(new KnowledgeFileImportFailure(file.FileName, normalizedRelativePath, ex.Message));
            }
        }

        return new KnowledgeFileImportResult(imported, updated, skipped, failures);
    }

    // Deletes all raw files beneath one source storage path.
    public async Task DeleteSourceFilesAsync(int sourceId, CancellationToken cancellationToken)
    {
        var source = await _repository.GetSourceByIdAsync(sourceId, cancellationToken);
        if (source is null || !source.SourceType.Equals(KnowledgeSourceTypes.LocalFiles, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var sourcePath = Path.Combine(_dataRoot, source.TenantGuid, source.TenantKnowledgeGuid, source.KnowledgeSourceGuid);
        KnowledgeRawFileStorage.DeleteDirectoryIfSafe(_dataRoot, sourcePath);
        var sourceManifestPath = Path.Combine(_dataRoot, ".manifest", "tenants", source.TenantGuid, "knowledge", source.TenantKnowledgeGuid, "sources", source.KnowledgeSourceGuid);
        KnowledgeRawFileStorage.DeleteDirectoryIfSafe(_dataRoot, sourceManifestPath);
    }

    // Deletes the raw file storage directory for one document.
    public async Task DeleteDocumentFileAsync(int documentId, CancellationToken cancellationToken)
    {
        var document = await _repository.GetDocumentByIdAsync(documentId, cancellationToken);
        if (document is null || string.IsNullOrWhiteSpace(document.StorageDirectory))
        {
            return;
        }

        KnowledgeRawFileStorage.DeleteDirectoryIfSafe(_dataRoot, document.StorageDirectory);
        var manifestPath = Path.Combine(
            _dataRoot,
            ".manifest",
            "tenants",
            document.TenantGuid,
            "knowledge",
            document.TenantKnowledgeGuid,
            "sources",
            document.KnowledgeSourceGuid,
            "folders",
            document.SourceFolderGuid,
            "documents",
            document.FolderDocumentGuid);
        KnowledgeRawFileStorage.DeleteDirectoryIfSafe(_dataRoot, manifestPath);
    }

    // Validates batch limits before parsing starts.
    private async Task ValidateBatchAsync(IReadOnlyList<IFormFile> files, CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetAsync(cancellationToken);
        if (files.Count == 0)
        {
            throw new InvalidOperationException("Yüklenecek dosya seçilmedi.");
        }

        if (files.Count > settings.MaxBatchFileCount)
        {
            throw new InvalidOperationException($"Tek seferde en fazla {settings.MaxBatchFileCount} dosya yüklenebilir.");
        }

        var maxFileBytes = settings.MaxFileSizeMb * 1024L * 1024L;
        var maxBatchBytes = settings.MaxBatchSizeMb * 1024L * 1024L;
        if (files.Any(file => file.Length > maxFileBytes))
        {
            throw new InvalidOperationException($"Dosya başına limit {settings.MaxFileSizeMb} MB.");
        }

        if (files.Sum(file => file.Length) > maxBatchBytes)
        {
            throw new InvalidOperationException($"Toplam yükleme limiti {settings.MaxBatchSizeMb} MB.");
        }
    }

    // Writes a document manifest under the deterministic manifest hierarchy.
    private async Task WriteDocumentManifestAsync(
        KnowledgeSource source,
        KnowledgeFolder folder,
        string folderDocumentGuid,
        string title,
        ParsedKnowledgeFile parsed,
        string normalizedFolderPath,
        string normalizedRelativePath,
        string storedFileName,
        CancellationToken cancellationToken)
    {
        var sourceManifestDirectory = Path.Combine(_dataRoot, ".manifest", "tenants", source.TenantGuid, "knowledge", source.TenantKnowledgeGuid, "sources", source.KnowledgeSourceGuid);
        var folderManifestDirectory = Path.Combine(sourceManifestDirectory, "folders", folder.SourceFolderGuid);
        var documentManifestDirectory = Path.Combine(folderManifestDirectory, "documents", folderDocumentGuid);
        Directory.CreateDirectory(documentManifestDirectory);
        Directory.CreateDirectory(sourceManifestDirectory);
        Directory.CreateDirectory(folderManifestDirectory);

        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(
            Path.Combine(sourceManifestDirectory, "source.json"),
            JsonSerializer.Serialize(source, options),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(folderManifestDirectory, "folder.json"),
            JsonSerializer.Serialize(folder, options),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(documentManifestDirectory, "document.json"),
            JsonSerializer.Serialize(new KnowledgeDocumentManifest(
                source.TenantGuid,
                source.TenantKnowledgeGuid,
                source.KnowledgeSourceGuid,
                folder.SourceFolderGuid,
                folderDocumentGuid,
                source.Name,
                source.Description,
                folder.FolderName,
                normalizedFolderPath,
                title,
                parsed.FileName,
                normalizedRelativePath,
                storedFileName,
                parsed.Extension,
                parsed.FileSizeBytes,
                parsed.FileHash,
                parsed.ContentHash,
                parsed.ParseStatus,
                parsed.OcrStatus,
                "local_import",
                DateTime.UtcNow),
                options),
            cancellationToken);
    }

    // Normalizes a browser-provided relative path into a safe identity path.
    private static string NormalizeRelativePath(string value)
    {
        var parts = value.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => part.Length > 0 && part != "." && part != "..")
            .ToArray();
        return parts.Length == 0 ? "belge.txt" : string.Join("/", parts);
    }

    // Extracts the folder part of a normalized relative path.
    private static string NormalizeFolderPath(string normalizedRelativePath)
    {
        var parts = normalizedRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length <= 1 ? "/" : $"/{string.Join("/", parts.Take(parts.Length - 1))}";
    }

    // Reads an indexed form value safely.
    private static string? GetIndexed(IReadOnlyList<string> values, int index)
    {
        return index >= 0 && index < values.Count ? values[index] : null;
    }
}
