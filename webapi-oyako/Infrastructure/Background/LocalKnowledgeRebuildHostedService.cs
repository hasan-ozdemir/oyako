// Codex developer note: Rebuilds local-file knowledge records after API serving starts.
using webapi_oyako.Domain.Services;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Infrastructure.Background;

// Runs manifest-backed local knowledge rebuild without blocking API startup.
public sealed class LocalKnowledgeRebuildHostedService : BackgroundService
{
    // Resolves scoped services only when the background rebuild actually runs.
    private readonly IServiceProvider _serviceProvider;
    // Writes operational diagnostics without affecting runtime status messages.
    private readonly ILogger<LocalKnowledgeRebuildHostedService> _logger;

    // Captures service dependencies used by the hosted background worker.
    public LocalKnowledgeRebuildHostedService(
        IServiceProvider serviceProvider,
        ILogger<LocalKnowledgeRebuildHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    // Starts the local rebuild after yielding control so the web host can continue starting.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await RebuildLocalKnowledgeAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Application shutdown requested the cancellation.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local knowledge rebuild worker failed.");
        }
    }

    // Replays raw-file manifests and activates fresh knowledge artifacts only when data changes.
    private async Task RebuildLocalKnowledgeAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var operationGate = scope.ServiceProvider.GetRequiredService<IKnowledgeOperationGate>();

        if (!await operationGate.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken))
        {
            _logger.LogWarning("Local knowledge rebuild skipped because another knowledge operation is still running.");
            return;
        }

        try
        {
            var rebuildService = scope.ServiceProvider.GetRequiredService<ILocalKnowledgeRebuildService>();
            var rebuiltCount = await rebuildService.RebuildMissingAsync(cancellationToken);
            if (rebuiltCount <= 0)
            {
                _logger.LogInformation("Local knowledge rebuild inspected manifests and found no missing records.");
                return;
            }

            var systemInstructionCache = scope.ServiceProvider.GetRequiredService<ISystemInstructionCache>();
            var cacheChanged = await systemInstructionCache.RefreshIfChangedAsync(cancellationToken);
            if (cacheChanged)
            {
                var readyQuestionService = scope.ServiceProvider.GetRequiredService<IReadyQuestionService>();
                readyQuestionService.QueueRefreshFromKnowledge();
            }

            _logger.LogInformation(
                "Local knowledge rebuild completed. Rebuilt={RebuiltCount}, CacheChanged={CacheChanged}.",
                rebuiltCount,
                cacheChanged);
        }
        finally
        {
            operationGate.Release();
        }
    }
}
