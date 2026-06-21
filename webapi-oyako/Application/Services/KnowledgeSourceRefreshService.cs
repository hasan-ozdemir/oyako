// Codex developer note: Implements non-blocking background refresh for web-site knowledge sources.
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Enums;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Domain.Services;
using webapi_oyako.Infrastructure.Configuration;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Application.Services;

// Refreshes active web-site sources without disturbing chat, settings, cache serving, or UI runtime status.
public sealed class KnowledgeSourceRefreshService : IKnowledgeSourceRefreshService
{
    // Stores state or a dependency required by the surrounding component.
    private readonly IWebCrawler _crawler;
    // Stores state or a dependency required by the surrounding component.
    private readonly IWebPageRepository _webPageRepository;
    // Stores state or a dependency required by the surrounding component.
    private readonly ICrawlRunRepository _crawlRunRepository;
    private readonly IKnowledgeStoreMaintenanceRepository _maintenanceRepository;
    // Stores state or a dependency required by the surrounding component.
    private readonly ISystemInstructionCache _systemInstructionCache;
    // Stores state or a dependency required by the surrounding component.
    private readonly IReadyQuestionService _readyQuestionService;
    // Stores state or a dependency required by the surrounding component.
    private readonly IKnowledgeOperationGate _operationGate;
    // Stores state or a dependency required by the surrounding component.
    private readonly CrawlerOptions _options;
    // Stores state or a dependency required by the surrounding component.
    private readonly ILogger<KnowledgeSourceRefreshService> _logger;

    // Creates a new instance and captures the dependencies needed by this component.
    public KnowledgeSourceRefreshService(
        IWebCrawler crawler,
        IWebPageRepository webPageRepository,
        ICrawlRunRepository crawlRunRepository,
        IKnowledgeStoreMaintenanceRepository maintenanceRepository,
        ISystemInstructionCache systemInstructionCache,
        IReadyQuestionService readyQuestionService,
        IKnowledgeOperationGate operationGate,
        IOptions<CrawlerOptions> options,
        ILogger<KnowledgeSourceRefreshService> logger)
    {
        _crawler = crawler;
        _webPageRepository = webPageRepository;
        _crawlRunRepository = crawlRunRepository;
        _maintenanceRepository = maintenanceRepository;
        _systemInstructionCache = systemInstructionCache;
        _readyQuestionService = readyQuestionService;
        _operationGate = operationGate;
        _options = options.Value;
        _logger = logger;
    }

    // Refreshes all active web sources and activates knowledge only when DB content actually changes.
    public async Task<KnowledgeSourceRefreshRunResult> RefreshWebSourcesAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        if (!await _operationGate.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken))
        {
            return new KnowledgeSourceRefreshRunResult(
                startedAt,
                DateTime.UtcNow,
                "skipped_busy",
                0,
                0,
                0,
                0,
                0,
                0,
                false,
                Array.Empty<KnowledgeSourceRefreshResult>(),
                "Başka bir bilgi bakım işlemi çalıştığı için knowledge-source-refresh atlandı.");
        }

        int? runId = null;
        try
        {
            var sources = await _webPageRepository.GetDueSeedSourcesAsync(DateTime.UtcNow, cancellationToken);
            runId = await _crawlRunRepository.StartAsync(startedAt, cancellationToken);
            var results = new List<KnowledgeSourceRefreshResult>();

            foreach (var source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(await RefreshSourceAsync(source, cancellationToken));
            }

            var added = results.Sum(result => result.AddedCount);
            var updated = results.Sum(result => result.UpdatedCount);
            var deleted = results.Sum(result => result.DeletedCount);
            var unchanged = results.Sum(result => result.UnchangedCount);
            var failed = results.Sum(result => result.FailedDocumentCount);
            var cacheActivated = results.Any(result => result.Changed);

            var failedSourceCount = results.Count(result => result.Status == "failed");
            var warningSourceCount = results.Count(result => result.Status is "warning" or "unreachable");
            var completedStatus = failedSourceCount > 0
                ? CrawlRunStatus.Failed
                : CrawlRunStatus.Completed;
            await _crawlRunRepository.SetCompletedAsync(
                runId.Value,
                DateTime.UtcNow,
                added + updated + unchanged + failed,
                failedSourceCount,
                warningSourceCount,
                string.Join(" | ", results.Where(result => result.Status == "failed").Select(result => result.Message).Take(3)),
                string.Join(" | ", results.Where(result => result.Status is "warning" or "unreachable").Select(result => result.Message).Take(3)),
                completedStatus,
                cancellationToken);

            return new KnowledgeSourceRefreshRunResult(
                startedAt,
                DateTime.UtcNow,
                failedSourceCount > 0
                    ? "failed"
                    : warningSourceCount > 0
                        ? "completed_with_warnings"
                        : "completed",
                sources.Count,
                added,
                updated,
                deleted,
                unchanged,
                failed,
                cacheActivated,
                results,
                cacheActivated ? "Seed knowledge source refresh tamamlandı ve bilgi önbelleği güncellendi." : "Seed knowledge source refresh tamamlandı; değişiklik bulunmadı.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "knowledge-source-refresh failed.");
            if (runId is not null)
            {
                await _crawlRunRepository.SetCompletedAsync(
                    runId.Value,
                    DateTime.UtcNow,
                    0,
                    1,
                    0,
                    ex.Message,
                    null,
                    CrawlRunStatus.Failed,
                    CancellationToken.None);
            }

            return new KnowledgeSourceRefreshRunResult(
                startedAt,
                DateTime.UtcNow,
                "failed",
                0,
                0,
                0,
                0,
                0,
                0,
                false,
                Array.Empty<KnowledgeSourceRefreshResult>(),
                ex.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    // Reads the latest persisted crawl/refresh metadata for health endpoints.
    public Task<CrawlRun?> GetLatestAsync(CancellationToken cancellationToken)
    {
        return _crawlRunRepository.GetLatestAsync(cancellationToken);
    }

    // Refreshes one source and reconciles DB content only when the source produced a usable reachable snapshot.
    private async Task<KnowledgeSourceRefreshResult> RefreshSourceAsync(KnowledgeSource source, CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var backupSetId = BuildBackupSetId(source.Id);
        var backupCreated = false;
        try
        {
            var result = await CrawlWithRetryAsync(source, cancellationToken);
            if (IsSourceUnreachable(result))
            {
                await ScheduleNextAttemptAsync(source, "unreachable", "Ulaşılamadı", "Kaynağa ulaşılamadı; mevcut belgeler korundu.", cancellationToken);
                _logger.LogWarning("knowledge-source-refresh preserved existing documents because source {SourceId} was unreachable.", source.Id);
                return new KnowledgeSourceRefreshResult(
                    source.Id,
                    source.Name,
                    startedAt,
                    DateTime.UtcNow,
                    "unreachable",
                    0,
                    0,
                    0,
                    0,
                    0,
                    false,
                    "Kaynağa ulaşılamadı; mevcut belgeler korundu.");
            }

            if (result.Pages.Count == 0)
            {
                await ScheduleNextAttemptAsync(source, "warning", "Uyarı", "Kaynak erişilebilir görünüyor ancak yenilenecek belge üretmedi.", cancellationToken);
                return new KnowledgeSourceRefreshResult(
                    source.Id,
                    source.Name,
                    startedAt,
                    DateTime.UtcNow,
                    "warning",
                    0,
                    0,
                    0,
                    0,
                    0,
                    false,
                    "Kaynak erişilebilir görünüyor ancak yenilenecek belge üretmedi.");
            }

            var pages = result.Pages.ToList();
            var pagesToPersist = pages
                .Where(page => page.HttpStatusCode is not 404 and not 410)
                .ToList();

            await _maintenanceRepository.BackupAsync(backupSetId, cancellationToken);
            backupCreated = true;
            var (added, updated, deleted, unchanged) = await _webPageRepository.ReplaceWebCrawlDocumentsForSourceAsync(
                source.Id,
                pagesToPersist,
                DateTime.UtcNow,
                cancellationToken);
            var failed = pages.Count(page => page.StatusCode != "ok");
            var changed = added + updated + deleted > 0;
            var hasSourceLevelCrawlerErrors = !result.IsSuccessful && result.Errors.Count > 0;
            if (changed)
            {
                if (await _systemInstructionCache.RefreshIfChangedAsync(cancellationToken))
                {
                    _readyQuestionService.QueueRefreshFromKnowledge();
                }
            }

            await _maintenanceRepository.CleanupBackupsExceptAsync(backupSetId, cancellationToken);
            var completionMessage = hasSourceLevelCrawlerErrors
                ? string.Join(" | ", result.Errors.Take(3))
                : changed
                    ? "Kaynak belgeleri atomik staging refresh ile güncellendi."
                    : "Kaynakta değişiklik bulunmadı.";

            return new KnowledgeSourceRefreshResult(
                source.Id,
                source.Name,
                startedAt,
                DateTime.UtcNow,
                hasSourceLevelCrawlerErrors ? "warning" : "completed",
                added,
                updated,
                deleted,
                unchanged,
                failed,
                changed,
                completionMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "knowledge-source-refresh failed for source {SourceId}.", source.Id);
            if (backupCreated)
            {
                try
                {
                    await _maintenanceRepository.RestoreAsync(backupSetId, CancellationToken.None);
                    await _systemInstructionCache.ReloadFromStoreAsync(CancellationToken.None);
                    await _maintenanceRepository.CleanupBackupsExceptAsync(backupSetId, CancellationToken.None);
                }
                catch (Exception restoreEx)
                {
                    _logger.LogError(restoreEx, "knowledge-source-refresh restore failed for source {SourceId}.", source.Id);
                }
            }

            await ScheduleNextAttemptAsync(source, "failed", "Hata", ex.Message, CancellationToken.None);
            return new KnowledgeSourceRefreshResult(
                source.Id,
                source.Name,
                startedAt,
                DateTime.UtcNow,
                "failed",
                0,
                0,
                0,
                0,
                0,
                false,
                ex.Message);
        }
    }

    private async Task ScheduleNextAttemptAsync(
        KnowledgeSource source,
        string statusCode,
        string statusLabel,
        string statusMessage,
        CancellationToken cancellationToken)
    {
        var checkedAtUtc = DateTime.UtcNow;
        var nextRefreshAtUtc = checkedAtUtc.AddMinutes(Math.Max(1, source.RefreshPeriodMinutes));
        await _webPageRepository.UpdateSeedSourceRefreshStatusAsync(
            source.Id,
            statusCode,
            statusLabel,
            statusMessage,
            checkedAtUtc,
            nextRefreshAtUtc,
            markSuccessfulRefresh: false,
            cancellationToken);
    }

    // Crawls a source with retry for transient network and HTTP failures.
    private async Task<CrawlerResult> CrawlWithRetryAsync(KnowledgeSource source, CancellationToken cancellationToken)
    {
        var retryCount = Math.Max(0, _options.SourceRefreshRetryCount);
        CrawlerResult? lastResult = null;
        for (var attempt = 0; attempt <= retryCount; attempt++)
        {
            lastResult = await _crawler.CrawlSourceAsync(source, cancellationToken);
            if (!ShouldRetry(lastResult) || attempt == retryCount)
            {
                return lastResult;
            }

            await Task.Delay(BuildRetryDelay(attempt), cancellationToken);
        }

        return lastResult!;
    }

    // Detects source-level failures where no trustworthy remote snapshot exists.
    private static bool IsSourceUnreachable(CrawlerResult result)
    {
        return result.Pages.Count == 0 && result.Errors.Count > 0
            || (result.Errors.Count > 0
                && result.Pages.Count > 0
                && result.Pages.All(page => page.HttpStatusCode is null)
                && result.Pages.All(page => page.StatusCode is "error" or "timeout"));
    }

    // Detects transient conditions worth retrying before deciding whether to apply the source snapshot.
    private static bool ShouldRetry(CrawlerResult result)
    {
        if (result.Pages.Any(page => page.StatusCode == "ok"))
        {
            return false;
        }

        if (IsSourceUnreachable(result))
        {
            return true;
        }

        return result.Pages.Count > 0
            && result.Pages.All(page => page.HttpStatusCode is 408 or 429 or 502 or 503 or 504);
    }

    // Builds a bounded exponential backoff delay with jitter.
    private TimeSpan BuildRetryDelay(int attempt)
    {
        var baseSeconds = Math.Max(1, _options.SourceRefreshRetryBaseDelaySeconds);
        var exponentialSeconds = Math.Min(60, baseSeconds * Math.Pow(2, attempt));
        var jitterMilliseconds = Random.Shared.Next(100, 1000);
        return TimeSpan.FromSeconds(exponentialSeconds) + TimeSpan.FromMilliseconds(jitterMilliseconds);
    }

    private static string BuildBackupSetId(int sourceId)
    {
        return $"ksr_{sourceId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}";
    }
}
