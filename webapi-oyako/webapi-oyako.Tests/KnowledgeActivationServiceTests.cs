// Codex developer note: Verifies lightweight knowledge activation behavior after source and document mutations.
using System.Runtime.CompilerServices;
using webapi_oyako.Application.Services;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Services;
using Xunit;

namespace webapi_oyako.Tests;

// Covers cache activation behavior that must stay separate from heavy redownload and blocking ready-question generation.
public class KnowledgeActivationServiceTests
{
    [Fact]
    // Ensures cache-only activation refreshes system instructions without touching ready-question generation.
    public async Task ActivateCacheOnlyAsync_RefreshesCacheWithoutReadyQuestionWork()
    {
        var cache = new CountingSystemInstructionCache();
        var readyQuestions = new CountingReadyQuestionService();
        var runtimeStatus = new RecordingRuntimeStatusService();
        var service = new KnowledgeActivationService(cache, readyQuestions, runtimeStatus);

        await service.ActivateCacheOnlyAsync(CancellationToken.None);

        Assert.Equal(1, cache.ForceRefreshCalls);
        Assert.Equal(0, readyQuestions.QueueRefreshCalls);
        Assert.Equal(0, readyQuestions.ForceRefreshCalls);
        Assert.DoesNotContain(runtimeStatus.Operations, operation => operation == "knowledge_redownload");
    }

    [Fact]
    // Ensures lightweight mutations queue ready-question regeneration without waiting for the LLM-backed refresh.
    public async Task ActivateCacheAndQueueReadyQuestionsAsync_QueuesReadyQuestionsWithoutBlockingForceRefresh()
    {
        var cache = new CountingSystemInstructionCache();
        var readyQuestions = new CountingReadyQuestionService();
        var runtimeStatus = new RecordingRuntimeStatusService();
        var service = new KnowledgeActivationService(cache, readyQuestions, runtimeStatus);

        await service.ActivateCacheAndQueueReadyQuestionsAsync(CancellationToken.None);

        Assert.Equal(1, cache.ForceRefreshCalls);
        Assert.Equal(1, readyQuestions.QueueRefreshCalls);
        Assert.Equal(0, readyQuestions.ForceRefreshCalls);
        Assert.DoesNotContain(runtimeStatus.Operations, operation => operation == "knowledge_redownload");
    }

    // Counts system instruction cache calls for activation tests.
    private sealed class CountingSystemInstructionCache : ISystemInstructionCache
    {
        // Counts full cache refresh operations.
        public int ForceRefreshCalls { get; private set; }

        // Initializes the fake cache without side effects.
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        // Returns the active fake system instruction text.
        public Task<string> GetCurrentAsync(CancellationToken cancellationToken) => Task.FromResult("system");

        // Returns a deterministic fake cache snapshot.
        public Task<SystemInstructionCacheSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<SystemInstructionCacheSnapshot?>(new SystemInstructionCacheSnapshot("hash", "fingerprint", 1, DateTime.UtcNow));
        }

        // Invalidates the fake cache without side effects.
        public Task InvalidateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        // Counts the full cache refresh requested by the activation service.
        public Task ForceRefreshAsync(CancellationToken cancellationToken)
        {
            ForceRefreshCalls++;
            return Task.CompletedTask;
        }

        // Recomposition is unused by this service test.
        public Task<bool> RecomposeFromActiveBlocksAsync(CancellationToken cancellationToken) => Task.FromResult(false);

        // Reload is unused by this service test.
        public Task ReloadFromStoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        // Changed refresh is unused by this service test.
        public Task<bool> RefreshIfChangedAsync(CancellationToken cancellationToken) => Task.FromResult(false);
    }

    // Counts ready-question calls for activation tests.
    private sealed class CountingReadyQuestionService : IReadyQuestionService
    {
        // Counts background ready-question refresh queue requests.
        public int QueueRefreshCalls { get; private set; }

        // Counts blocking ready-question refresh calls.
        public int ForceRefreshCalls { get; private set; }

        // Returns no ready questions because retrieval is outside this test scope.
        public Task<ReadyQuestionSet> GetNextAsync(int count, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ReadyQuestionSet(Array.Empty<string>(), "generated", null, 0, null, false));
        }

        // Counts that a non-blocking refresh was queued.
        public void QueueRefreshFromKnowledge()
        {
            QueueRefreshCalls++;
        }

        // Returns a no-op refresh result for unused background refresh calls.
        public Task<bool> RefreshFromKnowledgeAsync(CancellationToken cancellationToken) => Task.FromResult(false);

        // Counts blocking refresh calls so tests can prevent them in lightweight mutations.
        public Task<ReadyQuestionRefreshResult> ForceRefreshFromKnowledgeAsync(CancellationToken cancellationToken)
        {
            ForceRefreshCalls++;
            return Task.FromResult(new ReadyQuestionRefreshResult(true, 100, "fingerprint"));
        }
    }

    // Records runtime status operations emitted by the activation service.
    private sealed class RecordingRuntimeStatusService : IRuntimeStatusService
    {
        // Stores operations published during the test.
        public List<string> Operations { get; } = [];

        // Exposes the latest fake runtime state.
        public RuntimeStatusSnapshot Current { get; } = new("app", "ready_for_question", "ready", 1, 1, true, "Uygulama Hazır", "ready", "message", null, DateTime.UtcNow);

        // Records every published operation name.
        public Task PublishAsync(
            string operation,
            string phase,
            string stepKey,
            int stepIndex,
            int stepCount,
            bool isTerminal,
            string message,
            string severity,
            string icon,
            int? pageCount = null,
            CancellationToken cancellationToken = default)
        {
            Operations.Add(operation);
            return Task.CompletedTask;
        }

        // Provides an empty runtime status stream for interface completeness.
        public async IAsyncEnumerable<RuntimeStatusSnapshot> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
