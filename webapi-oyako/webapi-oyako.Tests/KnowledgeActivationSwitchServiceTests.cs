// Codex developer note: Verifies fast knowledge source/document visibility switches.
using webapi_oyako.Application.Services;
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Domain.Services;
using Xunit;

namespace webapi_oyako.Tests;

// Covers activation switch behavior that must not trigger crawler, redownload, or ready-question regeneration.
public class KnowledgeActivationSwitchServiceTests
{
    [Fact]
    // Ensures source archive changes use only the active cache recomposition path.
    public async Task SetSourceArchivedAsync_WhenStateChanges_RecomposesActiveCacheOnce()
    {
        var repository = new SwitchRepository
        {
            Source = new KnowledgeSource { Id = 10, Name = "Oyak Dijital", IsEnabled = true, IsArchived = false }
        };
        var cache = new CountingSystemInstructionCache();
        var service = new KnowledgeActivationSwitchService(repository, cache);

        var changed = await service.SetSourceArchivedAsync(10, true, CancellationToken.None);
        var unchanged = await service.SetSourceArchivedAsync(10, true, CancellationToken.None);

        Assert.True(changed);
        Assert.True(unchanged);
        Assert.True(repository.Source.IsArchived);
        Assert.Equal(1, repository.SourceArchiveWrites);
        Assert.Equal(1, cache.RecomposeCalls);
    }

    [Fact]
    // Ensures document archive changes use only the active cache recomposition path.
    public async Task SetDocumentArchivedAsync_WhenStateChanges_RecomposesActiveCacheOnce()
    {
        var repository = new SwitchRepository
        {
            Document = new WebPage { Id = 20, WebTitle = "Yönetilen Hizmetler", IsEnabled = true, IsArchived = false }
        };
        var cache = new CountingSystemInstructionCache();
        var service = new KnowledgeActivationSwitchService(repository, cache);

        var changed = await service.SetDocumentArchivedAsync(20, true, CancellationToken.None);
        var unchanged = await service.SetDocumentArchivedAsync(20, true, CancellationToken.None);

        Assert.True(changed);
        Assert.True(unchanged);
        Assert.True(repository.Document.IsArchived);
        Assert.Equal(1, repository.DocumentArchiveWrites);
        Assert.Equal(1, cache.RecomposeCalls);
    }

    [Fact]
    // Ensures missing source ids are reported without cache work.
    public async Task SetSourceEnabledAsync_WhenSourceIsMissing_DoesNotRecompose()
    {
        var repository = new SwitchRepository();
        var cache = new CountingSystemInstructionCache();
        var service = new KnowledgeActivationSwitchService(repository, cache);

        var changed = await service.SetSourceEnabledAsync(99, false, CancellationToken.None);

        Assert.False(changed);
        Assert.Equal(0, cache.RecomposeCalls);
    }

    [Fact]
    // Ensures missing document ids are reported without cache work.
    public async Task SetDocumentEnabledAsync_WhenDocumentIsMissing_DoesNotRecompose()
    {
        var repository = new SwitchRepository();
        var cache = new CountingSystemInstructionCache();
        var service = new KnowledgeActivationSwitchService(repository, cache);

        var changed = await service.SetDocumentEnabledAsync(99, false, CancellationToken.None);

        Assert.False(changed);
        Assert.Equal(0, cache.RecomposeCalls);
    }

    // Stores switchable in-memory source and document records for the service tests.
    private sealed class SwitchRepository : IWebPageRepository
    {
        // Stores the test source row.
        public KnowledgeSource? Source { get; set; }

        // Stores the test document row.
        public WebPage? Document { get; set; }

        // Counts source archive writes performed by the service.
        public int SourceArchiveWrites { get; private set; }

        // Counts document archive writes performed by the service.
        public int DocumentArchiveWrites { get; private set; }

        // Reads a source by id from the in-memory test state.
        public Task<KnowledgeSource?> GetSourceByIdAsync(int id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Source?.Id == id ? Source : null);
        }

        // Reads a document by id from the in-memory test state.
        public Task<WebPage?> GetDocumentByIdAsync(int documentId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Document?.Id == documentId ? Document : null);
        }

        // Applies a source enabled flag to the in-memory test state.
        public Task<bool> SetSourceEnabledAsync(int id, bool isEnabled, CancellationToken cancellationToken)
        {
            if (Source?.Id != id)
            {
                return Task.FromResult(false);
            }

            Source.IsEnabled = isEnabled;
            return Task.FromResult(true);
        }

        // Applies a source archived flag to the in-memory test state.
        public Task<bool> SetSourceArchivedAsync(int id, bool isArchived, CancellationToken cancellationToken)
        {
            if (Source?.Id != id)
            {
                return Task.FromResult(false);
            }

            Source.IsArchived = isArchived;
            SourceArchiveWrites++;
            return Task.FromResult(true);
        }

        // Applies a document enabled flag to the in-memory test state.
        public Task<bool> SetDocumentEnabledAsync(int id, bool isEnabled, CancellationToken cancellationToken)
        {
            if (Document?.Id != id)
            {
                return Task.FromResult(false);
            }

            Document.IsEnabled = isEnabled;
            return Task.FromResult(true);
        }

        // Applies a document archived flag to the in-memory test state.
        public Task<bool> SetDocumentArchivedAsync(int id, bool isArchived, CancellationToken cancellationToken)
        {
            if (Document?.Id != id)
            {
                return Task.FromResult(false);
            }

            Document.IsArchived = isArchived;
            DocumentArchiveWrites++;
            return Task.FromResult(true);
        }
    }

    // Counts system instruction cache recomposition calls for fast switch assertions.
    private sealed class CountingSystemInstructionCache : ISystemInstructionCache
    {
        // Counts active-block recomposition calls.
        public int RecomposeCalls { get; private set; }

        // Initializes the fake cache.
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        // Returns a placeholder system prompt.
        public Task<string> GetCurrentAsync(CancellationToken cancellationToken) => Task.FromResult("system");

        // Returns a placeholder cache snapshot.
        public Task<SystemInstructionCacheSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<SystemInstructionCacheSnapshot?>(new SystemInstructionCacheSnapshot("hash", "fingerprint", 1, DateTime.UtcNow));
        }

        // Invalidates the fake cache.
        public Task InvalidateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        // Forces a fake full refresh.
        public Task ForceRefreshAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        // Counts fast active-block recomposition.
        public Task<bool> RecomposeFromActiveBlocksAsync(CancellationToken cancellationToken)
        {
            RecomposeCalls++;
            return Task.FromResult(true);
        }

        // Reloads the fake cache from storage.
        public Task ReloadFromStoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        // Performs a fake change-aware refresh.
        public Task<bool> RefreshIfChangedAsync(CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
