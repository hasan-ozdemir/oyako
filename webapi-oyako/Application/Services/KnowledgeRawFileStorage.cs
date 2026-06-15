// Codex developer note: Centralizes raw knowledge-file storage helpers shared by local files and web-link downloads.
using webapi_oyako.Domain.Entities;

namespace webapi_oyako.Application.Services;

// Provides deterministic GUID-based storage paths and safe raw-file operations for supported documents.
public static class KnowledgeRawFileStorage
{
    // Builds the canonical raw-file directory used by all document ingestion paths.
    public static string BuildStorageDirectory(string dataRoot, KnowledgeSource source, string sourceFolderGuid, string folderDocumentGuid)
    {
        return Path.Combine(
            dataRoot,
            source.TenantGuid,
            source.TenantKnowledgeGuid,
            source.KnowledgeSourceGuid,
            sourceFolderGuid,
            folderDocumentGuid);
    }

    // Creates a safe stored file name without trusting a browser path or a remote server path.
    public static string BuildSafeFileName(string fileName, string fallback = "document.bin")
    {
        var name = Path.GetFileName(fileName);
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }

    // Writes one raw file after removing stale files from the document storage directory.
    public static async Task<string> ReplaceRawFileAsync(string storageDirectory, string fileName, byte[] bytes, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(storageDirectory);
        CleanDirectoryFiles(storageDirectory);
        var storedFileName = BuildSafeFileName(fileName);
        await File.WriteAllBytesAsync(Path.Combine(storageDirectory, storedFileName), bytes, cancellationToken);
        return storedFileName;
    }

    // Removes existing files from a document directory before overwriting.
    public static void CleanDirectoryFiles(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            File.Delete(file);
        }
    }

    // Deletes directories only when they resolve under the Oyako Data directory.
    public static void DeleteDirectoryIfSafe(string dataRoot, string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var root = Path.GetFullPath(dataRoot);
        var target = Path.GetFullPath(directory);
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(target))
        {
            return;
        }

        Directory.Delete(target, recursive: true);
    }
}
