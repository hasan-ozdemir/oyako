// Codex developer note: Explains the purpose and flow of webapi-oyako/Application/Services/SystemInstructionCache.cs for maintainers.
using System.Security.Cryptography;
using System.Text;
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Domain.Services;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Application.Services;

// Implements the SystemInstructionCache component and its responsibilities in the Oyako codebase.
public sealed class SystemInstructionCache : ISystemInstructionCache
{
    private const string CacheKey = "oyako-default-system-instruction-v7-source-agnostic-citation-contract";
    // Stores state or a dependency required by the surrounding component.
    private readonly IChatPromptBuilder _promptBuilder;
    // Stores state or a dependency required by the surrounding component.
    private readonly IWebPageRepository _webPageRepository;
    // Stores state or a dependency required by the surrounding component.
    private readonly ISystemInstructionCacheRepository _cacheRepository;
    // Stores state or a dependency required by the surrounding component.
    private readonly SemaphoreSlim _gate = new(1, 1);
    // Stores state or a dependency required by the surrounding component.
    private CacheSnapshot? _snapshot;

    public SystemInstructionCache(
        IChatPromptBuilder promptBuilder,
        IWebPageRepository webPageRepository,
        ISystemInstructionCacheRepository cacheRepository)
    {
        _promptBuilder = promptBuilder;
        _webPageRepository = webPageRepository;
        _cacheRepository = cacheRepository;
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var entry = await _cacheRepository.GetAsync(CacheKey, cancellationToken);
        // Guards the following branch so the workflow handles this condition deliberately.
        if (entry is not null)
        {
            // Creates the object needed for the next step of the workflow.
            _snapshot = new CacheSnapshot(entry.Content, entry.SourceFingerprint, entry.PageCount, entry.BuiltAtUtc);
        }
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<string> GetCurrentAsync(CancellationToken cancellationToken)
    {
        // Guards the following branch so the workflow handles this condition deliberately.
        if (_snapshot is not null)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return _snapshot.Content;
        }

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await RefreshIfChangedAsync(cancellationToken);
        // Returns the computed result to the caller and completes this branch of the workflow.
        return _snapshot?.Content
            ?? await _promptBuilder.BuildSystemPromptAsync(cancellationToken);
    }

    // Creates a new instance and captures the dependencies needed by this component.
    public Task<SystemInstructionCacheSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        // Guards the following branch so the workflow handles this condition deliberately.
        if (_snapshot is null)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult<SystemInstructionCacheSnapshot?>(null);
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return Task.FromResult<SystemInstructionCacheSnapshot?>(new SystemInstructionCacheSnapshot(
            CalculateHash(_snapshot.Content),
            _snapshot.SourceFingerprint,
            _snapshot.PageCount,
            _snapshot.BuiltAtUtc));
    }

    // Executes this component behavior as part of the Oyako application flow.
    public Task InvalidateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _snapshot = null;
        // Returns the computed result to the caller and completes this branch of the workflow.
        return Task.CompletedTask;
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task ForceRefreshAsync(CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await RefreshIfChangedAsync(cancellationToken);
    }

    // Rebuilds the active prompt from existing document blocks without crawling, scraping, or LLM work.
    public async Task<bool> RecomposeFromActiveBlocksAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var blocks = await _webPageRepository.GetActiveDocumentCacheBlocksAsync(cancellationToken);
            var fingerprint = BuildBlockFingerprint(blocks);
            if (_snapshot is not null && string.Equals(_snapshot.SourceFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return false;
            }

            var content = await _promptBuilder.BuildSystemPromptAsync(cancellationToken);
            var entry = new SystemInstructionCacheEntry
            {
                CacheKey = CacheKey,
                Content = content,
                ContentHash = CalculateHash(content),
                SourceFingerprint = fingerprint,
                PageCount = blocks.Count,
                BuiltAtUtc = DateTime.UtcNow
            };

            await _cacheRepository.UpsertAsync(entry, cancellationToken);
            _snapshot = new CacheSnapshot(entry.Content, entry.SourceFingerprint, entry.PageCount, entry.BuiltAtUtc);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task ReloadFromStoreAsync(CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entry = await _cacheRepository.GetAsync(CacheKey, cancellationToken);
            _snapshot = entry is null
                ? null
                // Creates the object needed for the next step of the workflow.
                : new CacheSnapshot(entry.Content, entry.SourceFingerprint, entry.PageCount, entry.BuiltAtUtc);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<bool> RefreshIfChangedAsync(CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await _webPageRepository.RebuildDocumentCacheBlocksAsync(cancellationToken);
            var blocks = await _webPageRepository.GetActiveDocumentCacheBlocksAsync(cancellationToken);
            var fingerprint = BuildBlockFingerprint(blocks);
            // Guards the following branch so the workflow handles this condition deliberately.
            if (_snapshot is not null && string.Equals(_snapshot.SourceFingerprint, fingerprint, StringComparison.Ordinal))
            {
                // Returns the computed result to the caller and completes this branch of the workflow.
                return false;
            }

            var content = await _promptBuilder.BuildSystemPromptAsync(cancellationToken);
            // Creates the object needed for the next step of the workflow.
            var entry = new SystemInstructionCacheEntry
            {
                CacheKey = CacheKey,
                Content = content,
                ContentHash = CalculateHash(content),
                SourceFingerprint = fingerprint,
                PageCount = blocks.Count,
                BuiltAtUtc = DateTime.UtcNow
            };

            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await _cacheRepository.UpsertAsync(entry, cancellationToken);
            // Creates the object needed for the next step of the workflow.
            _snapshot = new CacheSnapshot(entry.Content, entry.SourceFingerprint, entry.PageCount, entry.BuiltAtUtc);
            // Returns the computed result to the caller and completes this branch of the workflow.
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string BuildFingerprint(IReadOnlyList<WebPage> pages)
    {
        var text = string.Join(
            "\n",
            pages
                .OrderBy(page => page.WebSourceUrl, StringComparer.OrdinalIgnoreCase)
                .Select(page => $"{page.WebSourceUrl}|{page.ContentHash}"));
        // Returns the computed result to the caller and completes this branch of the workflow.
        return CalculateHash(text);
    }

    private static string BuildBlockFingerprint(IReadOnlyList<KnowledgeDocumentCacheBlock> blocks)
    {
        var text = string.Join(
            "\n",
            blocks
                .OrderBy(block => block.DocumentId)
                .Select(block => $"{block.DocumentId}|{block.ContentHash}"));
        return CalculateHash(text);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string CalculateHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        // Returns the computed result to the caller and completes this branch of the workflow.
        return Convert.ToHexString(bytes);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private sealed record CacheSnapshot(string Content, string SourceFingerprint, int PageCount, DateTime BuiltAtUtc);
}
