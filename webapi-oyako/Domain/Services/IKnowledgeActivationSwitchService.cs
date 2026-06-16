// Codex developer note: Declares fast source/document visibility switches without crawl, redownload, or LLM work.
namespace webapi_oyako.Domain.Services;

// Switches source/document active or archived state and recomposes the active knowledge cache from existing blocks.
public interface IKnowledgeActivationSwitchService
{
    // Enables or disables one knowledge source for Q&A without changing stored content.
    Task<bool> SetSourceEnabledAsync(int sourceId, bool isEnabled, CancellationToken cancellationToken);

    // Archives or restores one knowledge source for Q&A without changing stored content.
    Task<bool> SetSourceArchivedAsync(int sourceId, bool isArchived, CancellationToken cancellationToken);

    // Enables or disables one source document for Q&A without changing stored content.
    Task<bool> SetDocumentEnabledAsync(int documentId, bool isEnabled, CancellationToken cancellationToken);

    // Archives or restores one source document for Q&A without changing stored content.
    Task<bool> SetDocumentArchivedAsync(int documentId, bool isArchived, CancellationToken cancellationToken);
}
