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
        ISystemInstructionCache systemInstructionCache,
        IReadyQuestionService readyQuestionService,
        IKnowledgeOperationGate operationGate,
        IOptions<CrawlerOptions> options,
        ILogger<KnowledgeSourceRefreshService> logger)
    {
        _crawler = crawler;
        _webPageRepository = webPageRepository;
        _crawlRunRepository = crawlRunRepository;
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
            var sources = await _webPageRepository.GetActiveSourcesAsync(cancellationToken);
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
            var changed = results.Any(result => result.Changed);
            var cacheActivated = false;

            if (changed)
            {
                cacheActivated = await _systemInstructionCache.RefreshIfChangedAsync(cancellationToken);
                if (cacheActivated)
                {
                    _readyQuestionService.QueueRefreshFromKnowledge();
                }
            }

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
                changed ? "Knowledge source refresh tamamlandı ve bilgi önbelleği kontrol edildi." : "Knowledge source refresh tamamlandı; değişiklik bulunmadı.");
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
        try
        {
            var result = await CrawlWithRetryAsync(source, cancellationToken);
            if (IsSourceUnreachable(result))
            {
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

            var existingDocuments = await _webPageRepository.GetDocumentsBySourceAsync(source.Id, cancellationToken);
            var existingByUrl = existingDocuments.ToDictionary(document => document.WebSourceUrl, StringComparer.OrdinalIgnoreCase);
            var pages = result.Pages.ToList();
            var pagesToPersist = pages
                .Where(page => page.HttpStatusCode is not 404 and not 410)
                .ToList();
            var added = 0;
            var updated = 0;
            var unchanged = 0;

            foreach (var page in pagesToPersist)
            {
                if (!existingByUrl.TryGetValue(page.WebSourceUrl, out var existing))
                {
                    added++;
                    continue;
                }

                if (!string.Equals(existing.ContentHash, page.ContentHash, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(existing.StatusCode, page.StatusCode, StringComparison.OrdinalIgnoreCase)
                    || existing.HttpStatusCode != page.HttpStatusCode)
                {
                    updated++;
                }
                else
                {
                    unchanged++;
                }
            }

            await _webPageRepository.UpsertPagesAsync(pagesToPersist, cancellationToken);
            var deleted = await _webPageRepository.MarkMissingWebDocumentsForSourceAsync(
                source.Id,
                pagesToPersist.Select(page => page.WebSourceUrl).ToArray(),
                cancellationToken);
            var failed = pages.Count(page => page.StatusCode != "ok");
            var changed = added + updated + deleted > 0;
            var hasSourceLevelCrawlerErrors = !result.IsSuccessful && result.Errors.Count > 0;
            var completionMessage = hasSourceLevelCrawlerErrors
                ? string.Join(" | ", result.Errors.Take(3))
                : changed
                    ? "Kaynak belgeleri güncellendi."
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
}
