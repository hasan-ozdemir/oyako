// Codex developer note: Implements fast source/document visibility switches for active knowledge cache recomposition.
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Domain.Services;

namespace webapi_oyako.Application.Services;

// Applies enable/archive flags and recomposes system instructions without crawler, scraper, redownload, or LLM calls.
public sealed class KnowledgeActivationSwitchService : IKnowledgeActivationSwitchService
{
    // Stores knowledge source and document state.
    private readonly IWebPageRepository _webPageRepository;
    // Rebuilds only the active prompt view from existing document cache blocks.
    private readonly ISystemInstructionCache _systemInstructionCache;

    // Captures dependencies required for fast activation switching.
    public KnowledgeActivationSwitchService(
        IWebPageRepository webPageRepository,
        ISystemInstructionCache systemInstructionCache)
    {
        _webPageRepository = webPageRepository;
        _systemInstructionCache = systemInstructionCache;
    }

    // Enables or disables a source and updates only the active system prompt view.
    public async Task<bool> SetSourceEnabledAsync(int sourceId, bool isEnabled, CancellationToken cancellationToken)
    {
        var source = await _webPageRepository.GetSourceByIdAsync(sourceId, cancellationToken);
        if (source is null)
        {
            return false;
        }

        if (source.IsEnabled == isEnabled)
        {
            return true;
        }

        var changed = await _webPageRepository.SetSourceEnabledAsync(sourceId, isEnabled, cancellationToken);
        if (changed)
        {
            await _systemInstructionCache.RecomposeFromActiveBlocksAsync(cancellationToken);
        }

        return changed;
    }

    // Archives or restores a source and updates only the active system prompt view.
    public async Task<bool> SetSourceArchivedAsync(int sourceId, bool isArchived, CancellationToken cancellationToken)
    {
        var source = await _webPageRepository.GetSourceByIdAsync(sourceId, cancellationToken);
        if (source is null)
        {
            return false;
        }

        if (source.IsArchived == isArchived)
        {
            return true;
        }

        var changed = await _webPageRepository.SetSourceArchivedAsync(sourceId, isArchived, cancellationToken);
        if (changed)
        {
            await _systemInstructionCache.RecomposeFromActiveBlocksAsync(cancellationToken);
        }

        return changed;
    }

    // Enables or disables a document and updates only the active system prompt view.
    public async Task<bool> SetDocumentEnabledAsync(int documentId, bool isEnabled, CancellationToken cancellationToken)
    {
        var document = await _webPageRepository.GetDocumentByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            return false;
        }

        if (document.IsEnabled == isEnabled)
        {
            return true;
        }

        var changed = await _webPageRepository.SetDocumentEnabledAsync(documentId, isEnabled, cancellationToken);
        if (changed)
        {
            await _systemInstructionCache.RecomposeFromActiveBlocksAsync(cancellationToken);
        }

        return changed;
    }

    // Archives or restores a document and updates only the active system prompt view.
    public async Task<bool> SetDocumentArchivedAsync(int documentId, bool isArchived, CancellationToken cancellationToken)
    {
        var document = await _webPageRepository.GetDocumentByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            return false;
        }

        if (document.IsArchived == isArchived)
        {
            return true;
        }

        var changed = await _webPageRepository.SetDocumentArchivedAsync(documentId, isArchived, cancellationToken);
        if (changed)
        {
            await _systemInstructionCache.RecomposeFromActiveBlocksAsync(cancellationToken);
        }

        return changed;
    }
}
