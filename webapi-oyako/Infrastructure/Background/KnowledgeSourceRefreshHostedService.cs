// Codex developer note: Runs web-site knowledge source refresh in the background without blocking API serving.
using Microsoft.Extensions.Options;
using webapi_oyako.Domain.Services;
using webapi_oyako.Infrastructure.Configuration;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Infrastructure.Background;

// Implements the hosted worker that periodically runs knowledge-source-refresh.
public sealed class KnowledgeSourceRefreshHostedService : BackgroundService
{
    // Stores state or a dependency required by the surrounding component.
    private readonly IServiceProvider _serviceProvider;
    // Stores state or a dependency required by the surrounding component.
    private readonly ILogger<KnowledgeSourceRefreshHostedService> _logger;
    // Stores state or a dependency required by the surrounding component.
    private readonly CrawlerOptions _options;

    // Creates a new instance and captures the dependencies needed by this component.
    public KnowledgeSourceRefreshHostedService(
        IServiceProvider serviceProvider,
        ILogger<KnowledgeSourceRefreshHostedService> logger,
        IOptions<CrawlerOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    // Runs the periodic source refresh loop after the application starts serving requests.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!_options.SourceRefreshEnabled)
            {
                _logger.LogInformation("knowledge-source-refresh worker is disabled.");
                return;
            }

            await DelayWithJitterAsync(stoppingToken);
            await RefreshAsync(stoppingToken);

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _options.SourceRefreshIntervalMinutes)));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await DelayWithJitterAsync(stoppingToken);
                await RefreshAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Application shutdown requested the cancellation.
        }
    }

    // Creates a scope and runs one background knowledge-source-refresh cycle.
    private async Task RefreshAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var refreshService = scope.ServiceProvider.GetRequiredService<IKnowledgeSourceRefreshService>();
            var result = await refreshService.RefreshWebSourcesAsync(stoppingToken);
            _logger.LogInformation(
                "knowledge-source-refresh finished with status {Status}. Sources={SourceCount}, Added={Added}, Updated={Updated}, Deleted={Deleted}, CacheActivated={CacheActivated}.",
                result.Status,
                result.SourceCount,
                result.AddedCount,
                result.UpdatedCount,
                result.DeletedCount,
                result.CacheActivated);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Application shutdown requested the cancellation.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "knowledge-source-refresh worker failed.");
        }
    }

    // Applies startup and per-run jitter so multiple instances do not hit remote sources at the same moment.
    private async Task DelayWithJitterAsync(CancellationToken stoppingToken)
    {
        var seconds = Math.Max(1, _options.SourceRefreshStartupJitterSeconds);
        await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(1, seconds + 1)), stoppingToken);
    }
}
