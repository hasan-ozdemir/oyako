// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Repositories/IWebPageRepository.cs for maintainers.
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Models;

namespace webapi_oyako.Domain.Repositories;

// Declares the repository contract for knowledge sources, folders, documents, raw files, and settings.
public interface IWebPageRepository
{
    Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    Task<IReadOnlyList<KnowledgeSource>> GetSourcesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<KnowledgeSource>>(Array.Empty<KnowledgeSource>());
    Task<IReadOnlyList<KnowledgeSource>> GetActiveSourcesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<KnowledgeSource>>(Array.Empty<KnowledgeSource>());
    Task<KnowledgeSource?> GetSourceByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult<KnowledgeSource?>(null);
    Task<KnowledgeSource> AddSourceAsync(string sourceType, string name, string? description, string? address, CancellationToken cancellationToken) => throw new NotSupportedException("This repository implementation does not support source creation.");
    Task<KnowledgeSource> EnsureLocalSourceAsync(string tenantGuid, string tenantKnowledgeGuid, string knowledgeSourceGuid, string name, string? description, CancellationToken cancellationToken) => throw new NotSupportedException("This repository implementation does not support local source rebuild.");
    Task<bool> UpdateSourceAsync(int id, string sourceType, string name, string? description, string? address, bool isEnabled, CancellationToken cancellationToken) => Task.FromResult(false);
    Task<bool> SetSourceEnabledAsync(int id, bool isEnabled, CancellationToken cancellationToken) => Task.FromResult(false);
    Task<bool> SetSourceArchivedAsync(int id, bool isArchived, CancellationToken cancellationToken) => Task.FromResult(false);
    Task<bool> DeleteSourceAsync(int id, CancellationToken cancellationToken) => Task.FromResult(false);
    Task<bool> SetDocumentEnabledAsync(int id, bool isEnabled, CancellationToken cancellationToken) => Task.FromResult(false);
    Task<bool> SetDocumentArchivedAsync(int id, bool isArchived, CancellationToken cancellationToken) => Task.FromResult(false);
    Task<bool> UpdateDocumentAsync(int id, string title, string content, bool isEnabled, CancellationToken cancellationToken) => Task.FromResult(false);
    Task<WebPage?> GetDocumentByUrlAsync(string url, CancellationToken cancellationToken) => Task.FromResult<WebPage?>(null);
    Task<int> AddManualWebDocumentAsync(int sourceId, WebPage page, CancellationToken cancellationToken) => Task.FromResult(0);
    Task<bool> UpdateWebDocumentLinkAsync(int id, WebPage page, CancellationToken cancellationToken) => Task.FromResult(false);
    Task<bool> DeleteDocumentAsync(int id, CancellationToken cancellationToken) => Task.FromResult(false);
    Task<IReadOnlyDictionary<string, WebPage>> GetAllPagesByUrlAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyDictionary<string, WebPage>>(new Dictionary<string, WebPage>());
    Task<IReadOnlyList<WebPage>> GetAllPagesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<WebPage>>(Array.Empty<WebPage>());
    Task<IReadOnlyList<WebPage>> GetKnowledgeSourcesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<WebPage>>(Array.Empty<WebPage>());
    Task<IReadOnlyList<KnowledgeDocumentCacheBlock>> RebuildDocumentCacheBlocksAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<KnowledgeDocumentCacheBlock>>(Array.Empty<KnowledgeDocumentCacheBlock>());
    Task<IReadOnlyList<KnowledgeDocumentCacheBlock>> GetActiveDocumentCacheBlocksAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<KnowledgeDocumentCacheBlock>>(Array.Empty<KnowledgeDocumentCacheBlock>());
    Task<KnowledgeDocumentContent?> GetDisplayableDocumentContentAsync(int documentId, CancellationToken cancellationToken) => Task.FromResult<KnowledgeDocumentContent?>(null);
    Task<IReadOnlyList<KnowledgeFolder>> GetFoldersAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<KnowledgeFolder>>(Array.Empty<KnowledgeFolder>());
    Task UpsertPagesAsync(IReadOnlyCollection<WebPage> pages, CancellationToken cancellationToken) => Task.CompletedTask;
    Task<int> MarkMissingWebDocumentsForSourceAsync(int sourceId, IReadOnlyCollection<string> discoveredUrls, CancellationToken cancellationToken) => Task.FromResult(0);
    Task<bool> MarkDocumentInvalidAsync(int documentId, string statusCode, string statusLabel, string statusMessage, int? httpStatusCode, CancellationToken cancellationToken) => Task.FromResult(false);
    Task DeleteByUrlsAsync(IReadOnlyCollection<string> urls, CancellationToken cancellationToken) => Task.CompletedTask;
    Task ClearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    Task<KnowledgeFolder> GetOrCreateFolderAsync(KnowledgeSource source, string folderName, string normalizedFolderPath, CancellationToken cancellationToken) => throw new NotSupportedException("This repository implementation does not support folder creation.");
    Task<KnowledgeFolder> EnsureFolderAsync(KnowledgeSource source, string sourceFolderGuid, string folderName, string normalizedFolderPath, CancellationToken cancellationToken) => throw new NotSupportedException("This repository implementation does not support folder rebuild.");
    Task<LocalDocumentIdentity?> FindLocalDocumentIdentityAsync(int sourceId, string normalizedFolderPath, string normalizedRelativePath, CancellationToken cancellationToken) => Task.FromResult<LocalDocumentIdentity?>(null);
    Task<int> UpsertLocalDocumentAsync(LocalDocumentUpsert document, CancellationToken cancellationToken) => Task.FromResult(0);
    Task<IReadOnlyList<WebPage>> GetDocumentsBySourceAsync(int sourceId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<WebPage>>(Array.Empty<WebPage>());
    Task<WebPage?> GetDocumentByIdAsync(int documentId, CancellationToken cancellationToken) => Task.FromResult<WebPage?>(null);
    Task<KnowledgeUploadSettings> GetUploadSettingsAsync(CancellationToken cancellationToken) => Task.FromResult(new KnowledgeUploadSettings());
    Task<KnowledgeUploadSettings> UpdateUploadSettingsAsync(KnowledgeUploadSettings settings, CancellationToken cancellationToken) => Task.FromResult(settings);
    Task<(string TenantGuid, string TenantKnowledgeGuid)> GetKnowledgeIdentityAsync(CancellationToken cancellationToken) => Task.FromResult((Guid.Empty.ToString("D"), Guid.Empty.ToString("D")));
}
