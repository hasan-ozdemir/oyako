// Codex developer note: Explains the purpose and flow of webapi-oyako/webapi-oyako.Tests/SystemInstructionCacheTests.cs for maintainers.
using webapi_oyako.Application.Services;
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Domain.Services;
using Xunit;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Tests;

// Implements the SystemInstructionCacheTests component and its responsibilities in the Oyako codebase.
public class SystemInstructionCacheTests
{
    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task InitializeAsync_UsesPersistedCacheWithoutRebuildingPrompt()
    {
        // Creates the object needed for the next step of the workflow.
        var promptBuilder = new CountingPromptBuilder();
        // Creates the object needed for the next step of the workflow.
        var pageRepository = new MutableWebPageRepository(new[]
        {
            CreatePage("https://www.tenantdemo.example", "hash-1")
        });
        // Creates the object needed for the next step of the workflow.
        var cacheRepository = new StubSystemInstructionCacheRepository(new SystemInstructionCacheEntry
        {
            CacheKey = "oyako-default-system-instruction-v7-source-agnostic-citation-contract",
            Content = "persisted system instruction",
            ContentHash = "content-hash",
            SourceFingerprint = "persisted-fingerprint",
            PageCount = 1,
            BuiltAtUtc = DateTime.UtcNow
        });
        // Creates the object needed for the next step of the workflow.
        var cache = new SystemInstructionCache(promptBuilder, pageRepository, cacheRepository);

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await cache.InitializeAsync(CancellationToken.None);
        var content = await cache.GetCurrentAsync(CancellationToken.None);

        // Verifies the expected behavior for this test scenario.
        Assert.Equal("persisted system instruction", content);
        // Verifies the expected behavior for this test scenario.
        Assert.Equal(0, promptBuilder.BuildCount);
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task RefreshIfChangedAsync_RebuildsOnlyWhenPageFingerprintChanges()
    {
        // Creates the object needed for the next step of the workflow.
        var promptBuilder = new CountingPromptBuilder();
        // Creates the object needed for the next step of the workflow.
        var pageRepository = new MutableWebPageRepository(new[]
        {
            CreatePage("https://www.tenantdemo.example", "hash-1")
        });
        // Creates the object needed for the next step of the workflow.
        var cacheRepository = new StubSystemInstructionCacheRepository(null);
        // Creates the object needed for the next step of the workflow.
        var cache = new SystemInstructionCache(promptBuilder, pageRepository, cacheRepository);

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await cache.RefreshIfChangedAsync(CancellationToken.None);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await cache.RefreshIfChangedAsync(CancellationToken.None);

        // Verifies the expected behavior for this test scenario.
        Assert.Equal(1, promptBuilder.BuildCount);
        // Verifies the expected behavior for this test scenario.
        Assert.Equal("generated system instruction 1", cacheRepository.Entry?.Content);

        pageRepository.SetPages(new[]
        {
            CreatePage("https://www.tenantdemo.example", "hash-2")
        });

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await cache.RefreshIfChangedAsync(CancellationToken.None);
        var content = await cache.GetCurrentAsync(CancellationToken.None);

        // Verifies the expected behavior for this test scenario.
        Assert.Equal(2, promptBuilder.BuildCount);
        // Verifies the expected behavior for this test scenario.
        Assert.Equal("generated system instruction 2", content);
        // Verifies the expected behavior for this test scenario.
        Assert.Equal("generated system instruction 2", cacheRepository.Entry?.Content);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static WebPage CreatePage(string url, string contentHash)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return new WebPage
        {
            WebSourceUrl = url,
            WebTitle = "Tenant Demo",
            WebContent = "Tenant Demo hizmetleri hakkinda kaynak icerik.",
            ContentHash = contentHash,
            LastCrawledAtUtc = DateTime.UtcNow
        };
    }

    private sealed class CountingPromptBuilder : IChatPromptBuilder
    {
        // Exposes data consumed by other layers while preserving the domain or DTO shape.
        public int BuildCount { get; private set; }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<string> BuildSystemPromptAsync(CancellationToken cancellationToken)
        {
            BuildCount++;
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult($"generated system instruction {BuildCount}");
        }

        public string BuildSystemPrompt(IReadOnlyList<KnowledgeDocumentCacheBlock> blocks)
        {
            BuildCount++;
            return $"generated system instruction {BuildCount}";
        }
    }

    private sealed class MutableWebPageRepository : IWebPageRepository
    {
        // Stores state or a dependency required by the surrounding component.
        private IReadOnlyList<WebPage> _pages;

        // Executes this component behavior as part of the Oyako application flow.
        public MutableWebPageRepository(IReadOnlyList<WebPage> pages)
        {
            _pages = pages;
        }

        // Executes this component behavior as part of the Oyako application flow.
        public void SetPages(IReadOnlyList<WebPage> pages)
        {
            _pages = pages;
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.CompletedTask;
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<IReadOnlyDictionary<string, WebPage>> GetAllPagesByUrlAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult<IReadOnlyDictionary<string, WebPage>>(
                _pages.ToDictionary(page => page.WebSourceUrl, StringComparer.OrdinalIgnoreCase));
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<IReadOnlyList<WebPage>> GetAllPagesAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(_pages);
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<IReadOnlyList<WebPage>> GetKnowledgeSourcesAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(_pages);
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<IReadOnlyList<KnowledgeDocumentCacheBlock>> RebuildDocumentCacheBlocksAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(BuildBlocks());
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<IReadOnlyList<KnowledgeDocumentCacheBlock>> GetActiveDocumentCacheBlocksAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(BuildBlocks());
        }

        // Creates active document cache blocks from the mutable in-memory pages used by this test double.
        private IReadOnlyList<KnowledgeDocumentCacheBlock> BuildBlocks()
        {
            return _pages.Select((page, index) => new KnowledgeDocumentCacheBlock
            {
                DocumentId = index + 1,
                SourceId = 1,
                SourceName = "Tenant Demo",
                SourceType = "web_site",
                DocumentTitle = string.IsNullOrWhiteSpace(page.WebTitle) ? "Tenant Demo" : page.WebTitle!,
                DocumentUrl = page.WebSourceUrl,
                DocumentCitationLabel = $"Tenant Demo - {(string.IsNullOrWhiteSpace(page.WebTitle) ? "Tenant Demo" : page.WebTitle!)}",
                ContentHash = page.ContentHash,
                PromptBlock = page.WebContent,
                UpdatedAtUtc = page.LastCrawledAtUtc
            }).ToArray();
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task UpsertPagesAsync(IReadOnlyCollection<WebPage> pages, CancellationToken cancellationToken)
        {
            SetPages(pages.ToArray());
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.CompletedTask;
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task DeleteByUrlsAsync(IReadOnlyCollection<string> urls, CancellationToken cancellationToken)
        {
            _pages = _pages
                .Where(page => !urls.Contains(page.WebSourceUrl, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.CompletedTask;
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task ClearAsync(CancellationToken cancellationToken)
        {
            _pages = Array.Empty<WebPage>();
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.CompletedTask;
        }
    }

    private sealed class StubSystemInstructionCacheRepository : ISystemInstructionCacheRepository
    {
        // Executes this component behavior as part of the Oyako application flow.
        public StubSystemInstructionCacheRepository(SystemInstructionCacheEntry? entry)
        {
            Entry = entry;
        }

        // Exposes data consumed by other layers while preserving the domain or DTO shape.
        public SystemInstructionCacheEntry? Entry { get; private set; }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<SystemInstructionCacheEntry?> GetAsync(string cacheKey, CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(
                Entry is not null && string.Equals(Entry.CacheKey, cacheKey, StringComparison.Ordinal)
                    ? Entry
                    : null);
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task UpsertAsync(SystemInstructionCacheEntry entry, CancellationToken cancellationToken)
        {
            Entry = entry;
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.CompletedTask;
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task ClearAsync(CancellationToken cancellationToken)
        {
            Entry = null;
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.CompletedTask;
        }
    }
}


