// Codex developer note: Explains the purpose and flow of webapi-oyako/Application/Services/KnowledgeRedownloadService.cs for maintainers.
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Enums;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Domain.Services;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Application.Services;

// Implements the primary Knowledge Redownload workflow for global, source-level, and document-level knowledge updates.
public sealed class KnowledgeRedownloadService : IKnowledgeRedownloadService
{
    private const string Operation = "knowledge_redownload";
    private const int StepCount = 9;
    // Stores state or a dependency required by the surrounding component.
    private readonly IWebCrawler _crawler;
    // Stores state or a dependency required by the surrounding component.
    private readonly IWebPageRepository _webPageRepository;
    // Stores state or a dependency required by the surrounding component.
    private readonly ICrawlRunRepository _crawlRunRepository;
    // Stores state or a dependency required by the surrounding component.
    private readonly IKnowledgeStoreMaintenanceRepository _maintenanceRepository;
    // Stores state or a dependency required by the surrounding component.
    private readonly ISystemInstructionCache _systemInstructionCache;
    // Stores state or a dependency required by the surrounding component.
    private readonly IReadyQuestionService _readyQuestionService;
    // Stores state or a dependency required by the surrounding component.
    private readonly IReadyQuestionRepository _readyQuestionRepository;
    // Stores state or a dependency required by the surrounding component.
    private readonly IRuntimeStatusService _runtimeStatusService;
    // Stores state or a dependency required by the surrounding component.
    private readonly IKnowledgeOperationGate _operationGate;
    // Stores state or a dependency required by the surrounding component.
    private readonly ILocalKnowledgeRebuildService _localKnowledgeRebuildService;

    // Creates a new instance and captures the dependencies needed by this component.
    public KnowledgeRedownloadService(
        IWebCrawler crawler,
        IWebPageRepository webPageRepository,
        ICrawlRunRepository crawlRunRepository,
        IKnowledgeStoreMaintenanceRepository maintenanceRepository,
        ISystemInstructionCache systemInstructionCache,
        IReadyQuestionService readyQuestionService,
        IReadyQuestionRepository readyQuestionRepository,
        IRuntimeStatusService runtimeStatusService,
        IKnowledgeOperationGate operationGate,
        ILocalKnowledgeRebuildService localKnowledgeRebuildService)
    {
        _crawler = crawler;
        _webPageRepository = webPageRepository;
        _crawlRunRepository = crawlRunRepository;
        _maintenanceRepository = maintenanceRepository;
        _systemInstructionCache = systemInstructionCache;
        _readyQuestionService = readyQuestionService;
        _readyQuestionRepository = readyQuestionRepository;
        _runtimeStatusService = runtimeStatusService;
        _operationGate = operationGate;
        _localKnowledgeRebuildService = localKnowledgeRebuildService;
    }

    // Redownloads every knowledge source, restores the backup on core failures, and activates the fresh cache.
    public async Task<KnowledgeRedownloadResult> RedownloadAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        if (!await _operationGate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return BuildConflictResult(startedAt);
        }

        var backupSetId = BuildBackupSetId();
        var backupCreated = false;
        int? runId = null;
        var crawlCompleted = false;

        try
        {
            await PublishStepAsync("knowledge_redownload_backup", "backup", 1, false, "yedek alınıyor", "info", "database", null, cancellationToken);
            await _maintenanceRepository.BackupAsync(backupSetId, cancellationToken);
            backupCreated = true;

            await PublishStepAsync("knowledge_redownload_clearing", "clearing", 2, false, "bilgi tabloları temizleniyor", "info", "database", null, cancellationToken);
            await _maintenanceRepository.ClearKnowledgeTablesAsync(cancellationToken);

            runId = await _crawlRunRepository.StartAsync(DateTime.UtcNow, cancellationToken);
            await PublishStepAsync("browser_preparing", "browser_preparing", 3, false, "bilgi tarayıcı hazırlanıyor", "info", "browser", null, cancellationToken);
            await PublishStepAsync("crawling", "crawling", 4, false, "bilgiler alınıyor", "info", "search", null, cancellationToken);

            var result = await _crawler.CrawlAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var pagesToStore = StampPages(result.Pages, now);

            await PublishStepAsync("persisting", "persisting", 5, false, "bilgiler kaydediliyor", "info", "database", pagesToStore.Count, cancellationToken);
            await _webPageRepository.UpsertPagesAsync(pagesToStore, cancellationToken);
            var localRebuiltCount = await _localKnowledgeRebuildService.RebuildMissingAsync(cancellationToken);
            var totalDocumentCount = pagesToStore.Count + localRebuiltCount;

            if (totalDocumentCount == 0)
            {
                throw new InvalidOperationException("Yeniden indirilecek bilgi kaynağı veya belge bulunamadı.");
            }

            var sourceLevelErrorCount = result.IsSuccessful ? 0 : result.Errors.Count;
            var errorText = sourceLevelErrorCount > 0 ? string.Join(" | ", result.Errors.Take(3)) : null;
            var warningText = null as string;
            await _crawlRunRepository.SetCompletedAsync(
                runId.Value,
                DateTime.UtcNow,
                totalDocumentCount,
                sourceLevelErrorCount,
                0,
                errorText,
                warningText,
                CrawlRunStatus.Completed,
                cancellationToken);
            crawlCompleted = true;

            var artifacts = await RebuildArtifactsBestEffortAsync(totalDocumentCount, cancellationToken);
            await _maintenanceRepository.CleanupBackupsExceptAsync(backupSetId, cancellationToken);
            await PublishCompletedAsync(totalDocumentCount, sourceLevelErrorCount + artifacts.WarningCount, cancellationToken);

            return new KnowledgeRedownloadResult(
                "succeeded",
                backupSetId,
                startedAt,
                DateTime.UtcNow,
                totalDocumentCount,
                artifacts.WarningCount,
                sourceLevelErrorCount,
                artifacts.ReadyQuestionsCount,
                artifacts.SourceFingerprint,
                artifacts.CacheBuiltAtUtc,
                false,
                "Bilgi Bankası yeniden indirildi.");
        }
        catch (Exception ex)
        {
            if (runId is not null && !crawlCompleted)
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

            if (!backupCreated)
            {
                await PublishRedownloadErrorAsync("Bilgi Bankası yedeklenemedi.", CancellationToken.None);
                return BuildFailureResult("failed_restore_failed", backupSetId, startedAt, false, ex.Message);
            }

            try
            {
                await PublishStepAsync("knowledge_redownload_restore", "restore", 9, false, "önceki bilgi bankası geri yükleniyor", "warning", "refresh", null, CancellationToken.None);
                await _maintenanceRepository.RestoreAsync(backupSetId, CancellationToken.None);
                await _systemInstructionCache.ReloadFromStoreAsync(CancellationToken.None);
                await _maintenanceRepository.CleanupBackupsExceptAsync(backupSetId, CancellationToken.None);
                await PublishStepAsync("knowledge_redownload_restored", "restored", 9, true, "önceki bilgi bankası geri yüklendi", "warning", "database", null, CancellationToken.None);

                var pages = await _webPageRepository.GetKnowledgeSourcesAsync(CancellationToken.None);
                var cache = await _systemInstructionCache.GetSnapshotAsync(CancellationToken.None);
                var readyMetadata = await _readyQuestionRepository.GetMetadataAsync(CancellationToken.None);
                return new KnowledgeRedownloadResult(
                    "failed_restored",
                    backupSetId,
                    startedAt,
                    DateTime.UtcNow,
                    pages.Count,
                    0,
                    1,
                    readyMetadata.TotalAvailable,
                    cache?.SourceFingerprint,
                    cache?.BuiltAtUtc,
                    true,
                    $"Yeniden indirme tamamlanamadı; önceki bilgi bankası geri yüklendi. {ex.Message}");
            }
            catch (Exception restoreEx)
            {
                await PublishRedownloadErrorAsync("Bilgi Bankası geri yüklenemedi.", CancellationToken.None);
                return BuildFailureResult(
                    "failed_restore_failed",
                    backupSetId,
                    startedAt,
                    false,
                    $"Yeniden indirme ve restore başarısız. Yeniden indirme hatası: {ex.Message} Restore hatası: {restoreEx.Message}");
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    // Redownloads one source from its canonical backing store and activates the updated cache.
    public async Task<KnowledgeRedownloadResult> RedownloadSourceAsync(int sourceId, CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var source = await _webPageRepository.GetSourceByIdAsync(sourceId, cancellationToken);
        if (source is null)
        {
            return BuildNotFoundResult(startedAt, "Kaynak bulunamadı.");
        }

        if (!await _operationGate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return BuildConflictResult(startedAt);
        }

        try
        {
            var warnings = 0;
            var errors = 0;
            var documentCount = 0;
            await PublishStepAsync("browser_preparing", "browser_preparing", 3, false, "bilgi tarayıcı hazırlanıyor", "info", "browser", null, cancellationToken);
            await PublishStepAsync("crawling", "crawling", 4, false, "bilgiler alınıyor", "info", "search", null, cancellationToken);

            if (source.SourceType == KnowledgeSourceTypes.LocalFiles)
            {
                await PublishStepAsync("persisting", "persisting", 5, false, "yerel dosyalar yeniden oluşturuluyor", "info", "database", null, cancellationToken);
                documentCount = await _localKnowledgeRebuildService.RedownloadSourceAsync(sourceId, cancellationToken);
            }
            else if (source.SourceType == KnowledgeSourceTypes.WebLinks)
            {
                var documents = await _webPageRepository.GetDocumentsBySourceAsync(sourceId, cancellationToken);
                var pagesToStore = new List<WebPage>();
                foreach (var document in documents.Where(document => !document.IsArchived))
                {
                    var page = await _crawler.CrawlDocumentAsync(document, cancellationToken);
                    if (IsDocumentUnreachable(page))
                    {
                        warnings++;
                        continue;
                    }

                    if (page.StatusCode != "ok")
                    {
                        warnings++;
                    }

                    page.SourceId = source.Id;
                    page.SourceName = source.Name;
                    page.SourceType = source.SourceType;
                    page.IsEnabled = document.IsEnabled;
                    page.IsArchived = document.IsArchived;
                    page.Origin = "manual_web_link";
                    pagesToStore.Add(page);
                }

                var stampedPages = StampPages(pagesToStore, DateTime.UtcNow);
                await PublishStepAsync("persisting", "persisting", 5, false, "manuel web bağlantıları kaydediliyor", "info", "database", stampedPages.Count, cancellationToken);
                await _webPageRepository.UpsertPagesAsync(stampedPages, cancellationToken);
                documentCount = documents.Count;
            }
            else
            {
                var result = await _crawler.CrawlSourceAsync(source, cancellationToken);
                if (IsSourceUnreachable(result))
                {
                    warnings++;
                    documentCount = 0;
                    var cache = await _systemInstructionCache.GetSnapshotAsync(cancellationToken);
                    var readyMetadata = await _readyQuestionRepository.GetMetadataAsync(cancellationToken);
                    await PublishCompletedAsync(documentCount, warnings + errors, cancellationToken);

                    return new KnowledgeRedownloadResult(
                        "succeeded",
                        string.Empty,
                        startedAt,
                        DateTime.UtcNow,
                        documentCount,
                        warnings,
                        errors,
                        readyMetadata.TotalAvailable,
                        cache?.SourceFingerprint,
                        cache?.BuiltAtUtc,
                        false,
                        $"{source.Name} kaynağına ulaşılamadı; mevcut belgeler korundu.");
                }

                var pagesToStore = StampPages(result.Pages, DateTime.UtcNow)
                    .Where(page => page.HttpStatusCode is not 404 and not 410)
                    .ToList();
                await PublishStepAsync("persisting", "persisting", 5, false, "bilgiler kaydediliyor", "info", "database", pagesToStore.Count, cancellationToken);
                await _webPageRepository.UpsertPagesAsync(pagesToStore, cancellationToken);
                await _webPageRepository.MarkMissingWebDocumentsForSourceAsync(sourceId, pagesToStore.Select(page => page.WebSourceUrl).ToArray(), cancellationToken);
                documentCount = result.Pages.Count;
                errors += result.Errors.Count;
            }

            if (documentCount == 0)
            {
                warnings++;
            }

            var artifacts = await RebuildArtifactsBestEffortAsync(documentCount, cancellationToken);
            await PublishCompletedAsync(documentCount, warnings + errors + artifacts.WarningCount, cancellationToken);

            return new KnowledgeRedownloadResult(
                "succeeded",
                string.Empty,
                startedAt,
                DateTime.UtcNow,
                documentCount,
                warnings + artifacts.WarningCount,
                errors,
                artifacts.ReadyQuestionsCount,
                artifacts.SourceFingerprint,
                artifacts.CacheBuiltAtUtc,
                false,
                $"{source.Name} kaynağı yeniden indirildi.");
        }
        catch (Exception ex)
        {
            await PublishRedownloadErrorAsync("Kaynak yeniden indirilemedi.", CancellationToken.None);
            return BuildFailureResult("failed", string.Empty, startedAt, false, ex.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    // Redownloads one document from its original URL or raw-file archive and activates the updated cache.
    public async Task<KnowledgeRedownloadResult> RedownloadDocumentAsync(int documentId, CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var document = await _webPageRepository.GetDocumentByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            return BuildNotFoundResult(startedAt, "Belge bulunamadı.");
        }

        if (!await _operationGate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return BuildConflictResult(startedAt);
        }

        try
        {
            var warnings = 0;
            var errors = 0;
            var documentCount = 1;
            await PublishStepAsync("crawling", "crawling", 4, false, "belge alınıyor", "info", "search", null, cancellationToken);

            if (document.SourceType == KnowledgeSourceTypes.LocalFiles || document.WebSourceUrl.StartsWith("local-file://", StringComparison.OrdinalIgnoreCase))
            {
                await PublishStepAsync("persisting", "persisting", 5, false, "belge ham dosyadan yeniden oluşturuluyor", "info", "database", null, cancellationToken);
                if (!await _localKnowledgeRebuildService.RedownloadDocumentAsync(documentId, cancellationToken))
                {
                    warnings++;
                }
            }
            else
            {
                var page = await _crawler.CrawlDocumentAsync(document, cancellationToken);
                page.SourceId = document.SourceId;
                page.SourceName = document.SourceName;
                page.SourceType = document.SourceType;
                page.IsEnabled = document.IsEnabled;
                page.IsArchived = document.IsArchived;
                page.Origin = document.SourceType == KnowledgeSourceTypes.WebLinks ? "manual_web_link" : document.Origin;
                var pagesToStore = StampPages([page], DateTime.UtcNow);
                await PublishStepAsync("persisting", "persisting", 5, false, "belge kaydediliyor", "info", "database", pagesToStore.Count, cancellationToken);
                if (IsDocumentUnreachable(page))
                {
                    warnings++;
                    var cache = await _systemInstructionCache.GetSnapshotAsync(cancellationToken);
                    var readyMetadata = await _readyQuestionRepository.GetMetadataAsync(cancellationToken);
                    await PublishCompletedAsync(0, warnings + errors, cancellationToken);

                    return new KnowledgeRedownloadResult(
                        "succeeded",
                        string.Empty,
                        startedAt,
                        DateTime.UtcNow,
                        0,
                        warnings,
                        errors,
                        readyMetadata.TotalAvailable,
                        cache?.SourceFingerprint,
                        cache?.BuiltAtUtc,
                        false,
                        $"{document.WebTitle} belgesine ulaşılamadı; mevcut içerik korundu.");
                }
                else if (page.HttpStatusCode is 404 or 410)
                {
                    await _webPageRepository.DeleteByUrlsAsync([page.WebSourceUrl], cancellationToken);
                }
                else
                {
                    await _webPageRepository.UpsertPagesAsync(pagesToStore, cancellationToken);
                }

                if (page.StatusCode != "ok")
                {
                    warnings++;
                }
            }

            var artifacts = await RebuildArtifactsBestEffortAsync(documentCount, cancellationToken);
            await PublishCompletedAsync(documentCount, warnings + errors + artifacts.WarningCount, cancellationToken);

            return new KnowledgeRedownloadResult(
                "succeeded",
                string.Empty,
                startedAt,
                DateTime.UtcNow,
                documentCount,
                warnings + artifacts.WarningCount,
                errors,
                artifacts.ReadyQuestionsCount,
                artifacts.SourceFingerprint,
                artifacts.CacheBuiltAtUtc,
                false,
                $"{document.WebTitle} belgesi yeniden indirildi.");
        }
        catch (Exception ex)
        {
            await PublishRedownloadErrorAsync("Belge yeniden indirilemedi.", CancellationToken.None);
            return BuildFailureResult("failed", string.Empty, startedAt, false, ex.Message);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    // Rebuilds the in-process and persisted knowledge artifacts after DB content changes.
    private async Task<ArtifactRefreshResult> RebuildArtifactsBestEffortAsync(int documentCount, CancellationToken cancellationToken)
    {
        var warningCount = 0;
        await PublishStepAsync("cache_invalidating", "cache_refreshing", 6, false, "sistem talimatları yeniden oluşturuluyor", "info", "cache", documentCount, cancellationToken);

        await PublishStepAsync("cache_building", "cache_refreshing", 6, false, "bilgiler önbellekleniyor", "info", "cache", documentCount, cancellationToken);
        await _systemInstructionCache.ForceRefreshAsync(cancellationToken);

        await PublishStepAsync("ready_questions_building", "ready_questions_building", 7, false, "hazır sorular hazırlanıyor", "info", "refresh", documentCount, cancellationToken);
        try
        {
            var readyResult = await _readyQuestionService.ForceRefreshFromKnowledgeAsync(cancellationToken);
            if (!readyResult.Succeeded || readyResult.QuestionCount < 100)
            {
                warningCount++;
                await PublishStepAsync("ready_questions_warning", "ready_questions_building", 8, false, "hazır sorular şu anda hazırlanamadı", "warning", "warning", documentCount, cancellationToken);
            }
            else
            {
                await PublishStepAsync("ready_questions_saved", "ready_questions_building", 8, false, "hazır sorular kaydediliyor", "info", "database", documentCount, cancellationToken);
            }
        }
        catch
        {
            warningCount++;
            await PublishStepAsync("ready_questions_warning", "ready_questions_building", 8, false, "hazır sorular şu anda hazırlanamadı", "warning", "warning", documentCount, cancellationToken);
        }

        var cache = await _systemInstructionCache.GetSnapshotAsync(cancellationToken);
        var readyMetadata = await _readyQuestionRepository.GetMetadataAsync(cancellationToken);
        return new ArtifactRefreshResult(cache?.SourceFingerprint, cache?.BuiltAtUtc, readyMetadata.TotalAvailable, warningCount);
    }

    // Applies timestamps consistently before a document batch is persisted.
    private static List<WebPage> StampPages(IReadOnlyCollection<WebPage> pages, DateTime now)
    {
        return pages.Select(page =>
        {
            if (page.FirstSeenAtUtc == default)
            {
                page.FirstSeenAtUtc = now;
            }

            page.LastSeenAtUtc = now;
            page.LastCrawledAtUtc = now;
            return page;
        }).ToList();
    }

    // Detects source-level failures where no trustworthy remote snapshot exists.
    private static bool IsSourceUnreachable(CrawlerResult result)
    {
        return result.Pages.Count == 0 && result.Errors.Count > 0
            || (result.Errors.Count > 0
                && result.Pages.Count > 0
                && result.Pages.All(IsDocumentUnreachable));
    }

    // Detects document-level failures where no HTTP response was received.
    private static bool IsDocumentUnreachable(WebPage page)
    {
        return page.HttpStatusCode is null && page.StatusCode is "error" or "timeout";
    }

    // Publishes one structured runtime progress event for the Knowledge Redownload operation.
    private async Task PublishStepAsync(
        string stepKey,
        string phase,
        int stepIndex,
        bool isTerminal,
        string message,
        string severity,
        string icon,
        int? pageCount,
        CancellationToken cancellationToken)
    {
        await _runtimeStatusService.PublishAsync(
            Operation,
            stepKey,
            phase,
            stepIndex,
            StepCount,
            isTerminal,
            message,
            severity,
            icon,
            pageCount,
            cancellationToken);
    }

    // Publishes the terminal successful runtime status after knowledge redownload work finishes.
    private Task PublishCompletedAsync(int pageCount, int warningOrErrorCount, CancellationToken cancellationToken)
    {
        return PublishStepAsync(
            "ready_for_question",
            "completed",
            9,
            true,
            "Uygulama Hazır",
            warningOrErrorCount == 0 ? "ready" : "warning",
            "message",
            pageCount,
            cancellationToken);
    }

    // Builds the standard concurrent-operation conflict response.
    private static KnowledgeRedownloadResult BuildConflictResult(DateTime startedAt)
    {
        return new KnowledgeRedownloadResult(
            "conflict",
            string.Empty,
            startedAt,
            DateTime.UtcNow,
            0,
            0,
            0,
            0,
            null,
            null,
            false,
            "Başka bir bilgi yeniden indirme işlemi zaten çalışıyor.");
    }

    // Builds the standard not-found response for targeted source/document redownload requests.
    private static KnowledgeRedownloadResult BuildNotFoundResult(DateTime startedAt, string message)
    {
        return new KnowledgeRedownloadResult(
            "not_found",
            string.Empty,
            startedAt,
            DateTime.UtcNow,
            0,
            0,
            1,
            0,
            null,
            null,
            false,
            message);
    }

    // Builds the standard failure response for unrecoverable redownload failures.
    private static KnowledgeRedownloadResult BuildFailureResult(
        string status,
        string backupSetId,
        DateTime startedAt,
        bool restoredFromBackup,
        string message)
    {
        return new KnowledgeRedownloadResult(
            status,
            backupSetId,
            startedAt,
            DateTime.UtcNow,
            0,
            0,
            1,
            0,
            null,
            null,
            restoredFromBackup,
            message);
    }

    // Publishes the terminal failed runtime status for the Knowledge Redownload operation.
    private async Task PublishRedownloadErrorAsync(string message, CancellationToken cancellationToken)
    {
        await PublishStepAsync(
            "error",
            "failed",
            StepCount,
            true,
            message,
            "error",
            "alert",
            null,
            cancellationToken);
    }

    // Creates the backup-set identifier retained by the non-destructive global redownload workflow.
    private static string BuildBackupSetId()
    {
        return DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
    }

    // Carries cache and ready-question rebuild metadata back to the redownload result.
    private sealed record ArtifactRefreshResult(
        string? SourceFingerprint,
        DateTime? CacheBuiltAtUtc,
        int ReadyQuestionsCount,
        int WarningCount);
}
