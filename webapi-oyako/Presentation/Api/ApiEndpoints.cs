// Codex developer note: Explains the purpose and flow of webapi-oyako/Presentation/Api/ApiEndpoints.cs for maintainers.
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Enums;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Domain.Services;
using webapi_oyako.Infrastructure.Configuration;
using webapi_oyako.Presentation.Api;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Presentation;

// Implements the ApiEndpoints component and its responsibilities in the Oyako codebase.
public static class ApiEndpoints
{
    // Executes this component behavior as part of the Oyako application flow.
    public static IEndpointRouteBuilder MapOyakoEndpoints(this IEndpointRouteBuilder app)
    {
        // Registers or maps application behavior into the runtime pipeline.
        app.MapGet("/api/health", async (
            IKnowledgeSourceRefreshService knowledgeSourceRefreshService,
            IWebPageRepository webPageRepository,
            IReadyQuestionRepository readyQuestionRepository,
            ISystemInstructionCache systemInstructionCache,
            IAiChatClient aiChatClient,
            IAiProviderCatalog aiProviderCatalog,
            IOptions<AiOptions> aiOptions,
            IRuntimeStatusService runtimeStatusService,
            CancellationToken cancellationToken) =>
        {
            var apiHealth = await BuildApiHealthAsync(aiChatClient, aiProviderCatalog, aiOptions.Value, runtimeStatusService, cancellationToken);
            var knowledgeHealth = await BuildKnowledgeHealthAsync(
                knowledgeSourceRefreshService,
                webPageRepository,
                readyQuestionRepository,
                systemInstructionCache,
                cancellationToken);

            // Returns the computed result to the caller and completes this branch of the workflow.
            return Results.Ok(new HealthResponse(
                BuildAggregateStatus(apiHealth.Status, knowledgeHealth.Status),
                apiHealth,
                knowledgeHealth,
                DateTime.UtcNow));
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapGet("/api/api-health", async (
            IAiChatClient aiChatClient,
            IAiProviderCatalog aiProviderCatalog,
            IOptions<AiOptions> aiOptions,
            IRuntimeStatusService runtimeStatusService,
            CancellationToken cancellationToken) =>
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Results.Ok(await BuildApiHealthAsync(aiChatClient, aiProviderCatalog, aiOptions.Value, runtimeStatusService, cancellationToken));
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapGet("/api/knowledge-health", async (
            IKnowledgeSourceRefreshService knowledgeSourceRefreshService,
            IWebPageRepository webPageRepository,
            IReadyQuestionRepository readyQuestionRepository,
            ISystemInstructionCache systemInstructionCache,
            CancellationToken cancellationToken) =>
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Results.Ok(await BuildKnowledgeHealthAsync(
                knowledgeSourceRefreshService,
                webPageRepository,
                readyQuestionRepository,
                systemInstructionCache,
                cancellationToken));
        });

        app.MapGet("/api/tenant-config", (
            IOptions<TenantOptions> tenantOptions,
            IOptions<AiOptions> aiOptions) =>
        {
            var tenant = tenantOptions.Value;
            var ai = aiOptions.Value;
            var fallbackProvider = ai.FallbackProviders.FirstOrDefault() ?? "azure";
            return Results.Ok(new TenantConfigResponse(
                tenant.Id,
                tenant.OrderNumber,
                tenant.Name,
                tenant.DisplayName,
                tenant.AzureDomainName,
                tenant.CustomDomainName,
                tenant.WebUrl,
                tenant.AdminEmail,
                tenant.FeedbackEmail,
                ai.DefaultProvider,
                fallbackProvider,
                tenant.UiWebBrandName,
                tenant.UiWebAssistantName,
                tenant.UiWebTitle,
                tenant.UiWebHeaderTitle,
                tenant.UiWebBrandLogoUrl,
                tenant.UiWebAssistantWelcomeMessage,
                tenant.UiWebAssistantHeaderTitle,
                tenant.UiWebMoreMenuBrandLink,
                tenant.UiWebMoreMenuFeedbackLink,
                tenant.UiWebMoreMenuHelpLink,
                tenant.UiWebSettingsPageTitle,
                tenant.UiWebSettingsHeaderTitle,
                tenant.UiWebKnowledgeBankHeaderTitle,
                tenant.UiWebKnowledgeSourceHeaderTitle,
                tenant.UiWebKnowledgeSourceHeaderMessage,
                tenant.UiWebKnowledgeSourcesTableTitle,
                tenant.UiWebKnowledgeDocumentsTableTitle));
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapPost("/api/knowledge-redownload", async (
            IKnowledgeRedownloadService knowledgeRedownloadService,
            IWebPageRepository webPageRepository,
            CancellationToken cancellationToken) =>
        {
            var result = await knowledgeRedownloadService.RedownloadAsync(cancellationToken);
            var knowledgeBank = result.Status == "succeeded"
                ? await BuildKnowledgeBankResponseAsync(webPageRepository, cancellationToken)
                : null;
            var response = ToKnowledgeRedownloadResponse(result, knowledgeBank);
            // Returns the computed result to the caller and completes this branch of the workflow.
            return BuildKnowledgeRedownloadHttpResult(result, response);
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapPost("/api/knowledge-source-redownload/{sourceId:int}", async (
            int sourceId,
            IKnowledgeRedownloadService knowledgeRedownloadService,
            IWebPageRepository webPageRepository,
            CancellationToken cancellationToken) =>
        {
            var result = await knowledgeRedownloadService.RedownloadSourceAsync(sourceId, cancellationToken);
            var knowledgeBank = result.Status == "succeeded"
                ? await BuildKnowledgeBankResponseAsync(webPageRepository, cancellationToken)
                : null;
            var response = ToKnowledgeRedownloadResponse(result, knowledgeBank);
            // Returns the computed result to the caller and completes this branch of the workflow.
            return BuildKnowledgeRedownloadHttpResult(result, response);
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapPost("/api/source-document-redownload/{documentId:int}", async (
            int documentId,
            IKnowledgeRedownloadService knowledgeRedownloadService,
            IWebPageRepository webPageRepository,
            CancellationToken cancellationToken) =>
        {
            var result = await knowledgeRedownloadService.RedownloadDocumentAsync(documentId, cancellationToken);
            var knowledgeBank = result.Status == "succeeded"
                ? await BuildKnowledgeBankResponseAsync(webPageRepository, cancellationToken)
                : null;
            var response = ToKnowledgeRedownloadResponse(result, knowledgeBank);
            // Returns the computed result to the caller and completes this branch of the workflow.
            return BuildKnowledgeRedownloadHttpResult(result, response);
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapGet("/api/ai-settings", async (
            IAiConfigurationService aiConfigurationService,
            IEnumerable<IAiProviderClient> providers,
            IOptions<AiOptions> aiOptions,
            CancellationToken cancellationToken) =>
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Results.Ok(await BuildAiSettingsResponseAsync(aiConfigurationService, providers, aiOptions.Value, cancellationToken));
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapPut("/api/ai-settings", async (
            AiSettingsUpdateRequest request,
            IAiConfigurationService aiConfigurationService,
            IEnumerable<IAiProviderClient> providers,
            IOptions<AiOptions> aiOptions,
            CancellationToken cancellationToken) =>
        {
            var provider = NormalizeProvider(request.Provider);
            var disabledProviders = BuildDisabledProviderSet(aiOptions.Value);
            // Guards the following branch so the workflow handles this condition deliberately.
            if (provider is null || disabledProviders.Contains(provider))
            {
                // Returns the computed result to the caller and completes this branch of the workflow.
                return Results.BadRequest(new { message = "Geçersiz AI tedarikçisi." });
            }

            var providerClient = providers.FirstOrDefault(item => item.ProviderName.Equals(provider, StringComparison.OrdinalIgnoreCase));
            // Guards the following branch so the workflow handles this condition deliberately.
            if (providerClient is null)
            {
                // Returns the computed result to the caller and completes this branch of the workflow.
                return Results.BadRequest(new { message = "AI tedarikçisi bulunamadı." });
            }

            var models = await providerClient.GetModelsAsync(cancellationToken);
            // Guards the following branch so the workflow handles this condition deliberately.
            if (!models.Any(model => model.Id.Equals(request.Model, StringComparison.OrdinalIgnoreCase)))
            {
                // Returns the computed result to the caller and completes this branch of the workflow.
                return Results.BadRequest(new { message = "Seçilen AI modeli kullanılamıyor." });
            }

            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await aiConfigurationService.UpdateAsync(provider, request.Model, cancellationToken);
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Results.Ok(await BuildAiSettingsResponseAsync(aiConfigurationService, providers, aiOptions.Value, cancellationToken));
        });

        app.MapGet("/api/qna-experience-settings", async (
            IQnaExperienceSettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var settings = await settingsService.GetAsync(cancellationToken);
            return Results.Ok(ToQnaExperienceSettingsResponse(settings));
        });

        app.MapPut("/api/qna-experience-settings", async (
            QnaExperienceSettingsUpdateRequest request,
            IQnaExperienceSettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var settings = await settingsService.UpdateAsync(
                    request.DisplayedReadyQuestionCount,
                    request.DisplayedSuggestedQuestionCount,
                    request.AutoSubmitPromptButtons,
                    request.ShowAnswerSourceDocumentNames,
                    cancellationToken);
                return Results.Ok(ToQnaExperienceSettingsResponse(settings));
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapGet("/api/knowledge-source-refresh/status", async (
            IKnowledgeSourceRefreshService knowledgeSourceRefreshService,
            CancellationToken cancellationToken) =>
        {
            var latest = await knowledgeSourceRefreshService.GetLatestAsync(cancellationToken);

            // Guards the following branch so the workflow handles this condition deliberately.
            if (latest is null)
            {
                // Returns the computed result to the caller and completes this branch of the workflow.
                return Results.Ok(new CrawlStatusResponse(
                    null, null, null, 0, 0, 0, "not_started", "No crawl has run yet.", null));
            }

            // Returns the computed result to the caller and completes this branch of the workflow.
            return Results.Ok(new CrawlStatusResponse(
                latest.Id,
                latest.StartedAtUtc,
                latest.CompletedAtUtc,
                latest.PageCount,
                latest.ErrorCount,
                latest.WarningCount,
                latest.Status.ToString(),
                latest.ErrorMessage,
                latest.WarningMessage));
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapGet("/api/runtime/status", (IRuntimeStatusService runtimeStatusService) =>
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Results.Ok(runtimeStatusService.Current);
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapGet("/api/runtime/status/stream", async (
            IRuntimeStatusService runtimeStatusService,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers["X-Accel-Buffering"] = "no";

            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await foreach (var snapshot in runtimeStatusService.WatchAsync(cancellationToken))
            {
                // Creates the object needed for the next step of the workflow.
                var payload = JsonSerializer.Serialize(new { type = "status", status = snapshot });
                // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                await context.Response.Body.FlushAsync(cancellationToken);
            }
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapPost("/api/knowledge-source-refresh", async (
            IKnowledgeSourceRefreshService knowledgeSourceRefreshService,
            CancellationToken cancellationToken) =>
        {
            var run = await knowledgeSourceRefreshService.RefreshWebSourcesAsync(cancellationToken);
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Results.Ok(run);
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapGet("/api/knowledge-bank", async (
            IWebPageRepository webPageRepository,
            CancellationToken cancellationToken) =>
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Results.Ok(await BuildKnowledgeBankResponseAsync(webPageRepository, cancellationToken));
        });

        app.MapGet("/api/knowledge-documents/{id:int}/content", async (
            int id,
            IWebPageRepository webPageRepository,
            CancellationToken cancellationToken) =>
        {
            var document = await webPageRepository.GetDisplayableDocumentContentAsync(id, cancellationToken);
            if (document is null)
            {
                return Results.NotFound(new { message = "Belge bulunamadı veya etkin değil." });
            }

            return Results.Ok(new KnowledgeDocumentContentResponse(
                    document.Id,
                    document.SourceId,
                    document.SourceName,
                    document.SourceType,
                    document.Title,
                    document.Url,
                    document.Content,
                    document.OriginalFileName,
                    document.LastCheckedAtUtc,
                    document.LastCrawledAtUtc));
        });

        app.MapGet("/api/knowledge-settings", async (
            IKnowledgeUploadSettingsService uploadSettingsService,
            CancellationToken cancellationToken) =>
        {
            var settings = await uploadSettingsService.GetAsync(cancellationToken);
            return Results.Ok(new KnowledgeUploadSettingsResponse(
                settings.MaxFileSizeMb,
                settings.MaxBatchFileCount,
                settings.MaxBatchSizeMb,
                settings.UpdatedAtUtc));
        });

        app.MapPut("/api/knowledge-settings", async (
            KnowledgeUploadSettingsUpdateRequest request,
            IKnowledgeUploadSettingsService uploadSettingsService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var settings = await uploadSettingsService.UpdateAsync(
                    request.MaxFileSizeMb,
                    request.MaxBatchFileCount,
                    request.MaxBatchSizeMb,
                    cancellationToken);
                return Results.Ok(new KnowledgeUploadSettingsResponse(
                    settings.MaxFileSizeMb,
                    settings.MaxBatchFileCount,
                    settings.MaxBatchSizeMb,
                    settings.UpdatedAtUtc));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        app.MapPost("/api/knowledge-files/preview", async (
            HttpRequest request,
            IKnowledgeFileImportService importService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var form = await request.ReadFormAsync(cancellationToken);
                var result = await importService.PreviewAsync(
                    form.Files.ToList(),
                    form["relativePaths"].Select(value => value ?? string.Empty).ToList(),
                    cancellationToken);
                return Results.Ok(new KnowledgeFilePreviewResponse(
                    result.Items.Select(item => new KnowledgeFilePreviewItemResponse(
                        item.ClientFileId,
                        item.FileName,
                        item.RelativePath,
                        item.DefaultTitle,
                        item.Content,
                        item.ContentPreview,
                        item.ParseStatus,
                        item.OcrStatus,
                        item.ErrorMessage)).ToList(),
                    result.Messages));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        app.MapPost("/api/knowledge-sources/{sourceId:int}/documents/import", async (
            int sourceId,
            HttpRequest request,
            IKnowledgeFileImportService importService,
            IKnowledgeActivationService activationService,
            IWebPageRepository webPageRepository,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var form = await request.ReadFormAsync(cancellationToken);
                var result = await importService.ImportAsync(
                    sourceId,
                    form.Files.ToList(),
                    form["titles"].Select(value => value ?? string.Empty).ToList(),
                    form["relativePaths"].Select(value => value ?? string.Empty).ToList(),
                    cancellationToken);
                await activationService.ActivateCacheAndQueueReadyQuestionsAsync(cancellationToken);
                return Results.Ok(new KnowledgeFileImportResponse(
                    result.ImportedCount,
                    result.UpdatedCount,
                    result.SkippedCount,
                    result.FailedItems.Select(item => new KnowledgeFileImportFailureResponse(item.FileName, item.RelativePath, item.Message)).ToList(),
                    await BuildKnowledgeBankResponseAsync(webPageRepository, cancellationToken)));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        app.MapPost("/api/knowledge-rebuild/local-files", async (
            ILocalKnowledgeRebuildService rebuildService,
            IKnowledgeActivationService activationService,
            IWebPageRepository webPageRepository,
            CancellationToken cancellationToken) =>
        {
            var inspected = await rebuildService.RebuildMissingAsync(cancellationToken);
            await activationService.ActivateCacheAndQueueReadyQuestionsAsync(cancellationToken);
            return Results.Ok(new LocalKnowledgeRebuildResponse(
                inspected,
                await BuildKnowledgeBankResponseAsync(webPageRepository, cancellationToken)));
        });

        app.MapPost("/api/knowledge-sources/{sourceId:int}/web-documents", async (
            int sourceId,
            KnowledgeWebDocumentCreateRequest request,
            IWebPageRepository webPageRepository,
            IWebCrawler crawler,
            IKnowledgeActivationService activationService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var source = await webPageRepository.GetSourceByIdAsync(sourceId, cancellationToken);
                if (source is null)
                {
                    return Results.NotFound(new { message = "Kaynak bulunamadı." });
                }

                if (!source.SourceType.Equals(KnowledgeSourceTypes.WebLinks, StringComparison.OrdinalIgnoreCase))
                {
                    return Results.BadRequest(new { message = "Yeni web belgesi yalnızca Web Bağlantıları kaynaklarına eklenebilir." });
                }

                var normalizedUrl = NormalizeHttpDocumentUrl(request.Url);
                var existing = await webPageRepository.GetDocumentByUrlAsync(normalizedUrl, cancellationToken);
                if (existing is not null)
                {
                    return Results.Conflict(new { message = "Bu web bağlantısı Bilgi Bankası'nda zaten var." });
                }

                var candidate = new WebPage
                {
                    SourceId = source.Id,
                    SourceName = source.Name,
                    SourceType = source.SourceType,
                    TenantGuid = source.TenantGuid,
                    TenantKnowledgeGuid = source.TenantKnowledgeGuid,
                    KnowledgeSourceGuid = source.KnowledgeSourceGuid,
                    WebSourceUrl = normalizedUrl,
                    WebTitle = string.IsNullOrWhiteSpace(request.Title) ? source.Name : request.Title.Trim(),
                    IsEnabled = request.IsEnabled ?? true,
                    Origin = "manual_web_link"
                };
                var page = await crawler.CrawlDocumentAsync(candidate, cancellationToken);
                if (!page.StatusCode.Equals("ok", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.BadRequest(new { message = page.StatusMessage });
                }

                page.SourceId = source.Id;
                page.SourceName = source.Name;
                page.SourceType = source.SourceType;
                page.IsEnabled = request.IsEnabled ?? true;
                page.Origin = "manual_web_link";
                if (!string.IsNullOrWhiteSpace(request.Title))
                {
                    page.WebTitle = request.Title.Trim();
                }

                await webPageRepository.AddManualWebDocumentAsync(source.Id, page, cancellationToken);
                await activationService.ActivateCacheAndQueueReadyQuestionsAsync(cancellationToken);
                return Results.Ok(await BuildKnowledgeBankResponseAsync(webPageRepository, cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapPost("/api/knowledge-sources", async (
            KnowledgeSourceUpsertRequest request,
            IWebPageRepository webPageRepository,
            IKnowledgeRedownloadService knowledgeRedownloadService,
            IKnowledgeActivationService activationService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var sourceType = NormalizeSourceType(request.SourceType);
                var source = await webPageRepository.AddSourceAsync(sourceType, request.Name ?? string.Empty, request.Description, request.Address, cancellationToken);
                if (sourceType == "web_site" && request.Redownload != false)
                {
                    await knowledgeRedownloadService.RedownloadSourceAsync(source.Id, cancellationToken);
                }

                return Results.Ok(await BuildKnowledgeBankResponseAsync(webPageRepository, cancellationToken));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapPut("/api/knowledge-sources/{id:int}", async (
            int id,
            KnowledgeSourceUpsertRequest request,
            IWebPageRepository webPageRepository,
            IKnowledgeRedownloadService knowledgeRedownloadService,
            IKnowledgeActivationService activationService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var sourceType = NormalizeSourceType(request.SourceType);
                var existingSource = await webPageRepository.GetSourceByIdAsync(id, cancellationToken);
                if (existingSource is null)
                {
                    return Results.NotFound(new { message = "Kaynak bulunamadı." });
                }

                var nextAddress = sourceType == "web_site"
                    ? request.Address ?? string.Empty
                    : existingSource.Address;
                var shouldRedownloadWebSite = sourceType == "web_site"
                    && !NormalizeComparableAddress(existingSource.Address).Equals(NormalizeComparableAddress(nextAddress), StringComparison.OrdinalIgnoreCase);
                var changed = await webPageRepository.UpdateSourceAsync(
                    id,
                    sourceType,
                    request.Name ?? string.Empty,
                    request.Description,
                    nextAddress,
                    request.IsEnabled ?? true,
                    cancellationToken);
                if (!changed)
                {
                    return Results.NotFound(new { message = "Kaynak bulunamadı." });
                }

                if (shouldRedownloadWebSite && request.Redownload != false)
                {
                    await knowledgeRedownloadService.RedownloadSourceAsync(id, cancellationToken);
                }
                else
                {
                    await activationService.ActivateCacheAndQueueReadyQuestionsAsync(cancellationToken);
                }

                return Results.Ok(await BuildKnowledgeBankResponseAsync(webPageRepository, cancellationToken));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapPatch("/api/knowledge-sources/{id:int}/enabled", async (
            int id,
            ToggleRequest request,
            IWebPageRepository webPageRepository,
            IKnowledgeActivationSwitchService activationSwitchService,
            CancellationToken cancellationToken) =>
        {
            var changed = await activationSwitchService.SetSourceEnabledAsync(id, request.IsEnabled, cancellationToken);
            if (!changed)
            {
                return Results.NotFound(new { message = "Kaynak bulunamadı." });
            }

            return Results.Ok(await BuildKnowledgeBankResponseAsync(webPageRepository, cancellationToken));
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapPatch("/api/knowledge-sources/{id:int}/archive", async (
            int id,
            ArchiveRequest request,
            IWebPageRepository webPageRepository,
            IKnowledgeActivationSwitchService activationSwitchService,
            CancellationToken cancellationToken) =>
        {
            var changed = await activationSwitchService.SetSourceArchivedAsync(id, request.IsArchived, cancellationToken);
            if (!changed)
            {
                return Results.NotFound(new { message = "Kaynak bulunamadı." });
            }

            return Results.Ok(await BuildKnowledgeBankResponseAsync(webPageRepository, cancellationToken));
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapGet("/api/knowledge-sources/{id:int}/diagnostics", async (
            int id,
            IWebPageRepository webPageRepository,
            CancellationToken cancellationToken) =>
        {
            var sources = await webPageRepository.GetSourcesAsync(cancellationToken);
            var source = sources.FirstOrDefault(item => item.Id == id);
            if (source is null)
            {
                return Results.NotFound(new { message = "Kaynak bulunamadı." });
            }

            var documents = await webPageRepository.GetKnowledgeSourcesAsync(cancellationToken);
            var warningDocuments = documents
                .Where(document => document.SourceId == id && IsWarningStatus(document.StatusCode))
                .Select(BuildKnowledgeDiagnosticDocument)
                .ToList();

            return Results.Ok(new
            {
                itemType = "source",
                source = BuildKnowledgeDiagnosticSource(source),
                warningDocuments
            });
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapDelete("/api/knowledge-sources/{id:int}", async (
            int id,
            IWebPageRepository webPageRepository,
            IKnowledgeFileImportService importService,
            IKnowledgeActivationService activationService,
            CancellationToken cancellationToken) =>
        {
            await importService.DeleteSourceFilesAsync(id, cancellationToken);
            var changed = await webPageRepository.DeleteSourceAsync(id, cancellationToken);
            if (!changed)
            {
                return Results.NotFound(new { message = "Kaynak bulunamadı." });
            }

            await activationService.ActivateCacheAndQueueReadyQuestionsAsync(cancellationToken);
            return Results.Ok(await BuildKnowledgeBankResponseAsync(webPageRepository, cancellationToken));
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapPatch("/api/knowledge-documents/{id:int}/enabled", async (
            int id,
            ToggleRequest request,
            IWebPageRepository webPageRepository,
            IKnowledgeActivationSwitchService activationSwitchService,
            CancellationToken cancellationToken) =>
        {
            var changed = await activationSwitchService.SetDocumentEnabledAsync(id, request.IsEnabled, cancellationToken);
            if (!changed)
            {
                return Results.NotFound(new { message = "Belge bulunamadı." });
            }

            return Results.Ok(await BuildKnowledgeBankResponseAsync(webPageRepository, cancellationToken));
        });

        app.MapPut("/api/knowledge-documents/{id:int}/local-content", async (
            int id,
            KnowledgeDocumentUpdateRequest request,
            IWebPageRepository webPageRepository,
            IKnowledgeActivationService activationService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return Results.BadRequest(new { message = "Belge içeriği boş olamaz." });
            }

            var document = await webPageRepository.GetDocumentByIdAsync(id, cancellationToken);
            if (document is null)
            {
                return Results.NotFound(new { message = "Belge bulunamadı." });
            }

            if (!document.SourceType.Equals(KnowledgeSourceTypes.LocalFiles, StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { message = "Belge metni yalnızca Yerel Dosyalar belgelerinde düzenlenebilir." });
            }

            var changed = await webPageRepository.UpdateDocumentAsync(id, request.Title, request.Content, request.IsEnabled, cancellationToken);
            if (!changed)
            {
                return Results.NotFound(new { message = "Belge bulunamadı." });
            }

            await activationService.ActivateCacheAndQueueReadyQuestionsAsync(cancellationToken);
            return Results.Ok(await BuildKnowledgeBankResponseAsync(webPageRepository, cancellationToken));
        });

        app.MapPatch("/api/knowledge-documents/{id:int}/web-link", async (
            int id,
            KnowledgeDocumentWebLinkUpdateRequest request,
            IWebPageRepository webPageRepository,
            IWebCrawler crawler,
            IKnowledgeActivationService activationService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var document = await webPageRepository.GetDocumentByIdAsync(id, cancellationToken);
                if (document is null)
                {
                    return Results.NotFound(new { message = "Belge bulunamadı." });
                }

                if (document.SourceType.Equals(KnowledgeSourceTypes.LocalFiles, StringComparison.OrdinalIgnoreCase))
                {
                    return Results.BadRequest(new { message = "Yerel dosya belgelerinde web bağlantısı düzenlenemez." });
                }

                var normalizedUrl = NormalizeHttpDocumentUrl(request.Url);
                var duplicate = await webPageRepository.GetDocumentByUrlAsync(normalizedUrl, cancellationToken);
                if (duplicate is not null && duplicate.Id != id)
                {
                    return Results.Conflict(new { message = "Bu web bağlantısı Bilgi Bankası'nda başka bir belge tarafından kullanılıyor." });
                }

                var candidate = new WebPage
                {
                    Id = document.Id,
                    SourceId = document.SourceId,
                    SourceName = document.SourceName,
                    SourceType = document.SourceType,
                    TenantGuid = document.TenantGuid,
                    TenantKnowledgeGuid = document.TenantKnowledgeGuid,
                    KnowledgeSourceGuid = document.KnowledgeSourceGuid,
                    SourceFolderGuid = document.SourceFolderGuid,
                    FolderDocumentGuid = document.FolderDocumentGuid,
                    NormalizedFolderPath = document.NormalizedFolderPath,
                    NormalizedRelativePath = document.NormalizedRelativePath,
                    StorageDirectory = document.StorageDirectory,
                    StoredFileName = document.StoredFileName,
                    WebSourceUrl = normalizedUrl,
                    WebTitle = document.WebTitle,
                    IsEnabled = document.IsEnabled,
                    IsArchived = document.IsArchived,
                    Origin = document.Origin
                };
                var page = await crawler.CrawlDocumentAsync(candidate, cancellationToken);
                if (!page.StatusCode.Equals("ok", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.BadRequest(new { message = page.StatusMessage });
                }

                page.SourceId = document.SourceId;
                page.SourceName = document.SourceName;
                page.SourceType = document.SourceType;
                page.IsEnabled = document.IsEnabled;
                page.IsArchived = document.IsArchived;
                page.Origin = document.Origin;
                var changed = await webPageRepository.UpdateWebDocumentLinkAsync(id, page, cancellationToken);
                if (!changed)
                {
                    return Results.NotFound(new { message = "Belge bulunamadı." });
                }

                await activationService.ActivateCacheAndQueueReadyQuestionsAsync(cancellationToken);
                return Results.Ok(await BuildKnowledgeBankResponseAsync(webPageRepository, cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapPatch("/api/knowledge-documents/{id:int}/archive", async (
            int id,
            ArchiveRequest request,
            IWebPageRepository webPageRepository,
            IKnowledgeActivationSwitchService activationSwitchService,
            CancellationToken cancellationToken) =>
        {
            var changed = await activationSwitchService.SetDocumentArchivedAsync(id, request.IsArchived, cancellationToken);
            if (!changed)
            {
                return Results.NotFound(new { message = "Belge bulunamadı." });
            }

            return Results.Ok(await BuildKnowledgeBankResponseAsync(webPageRepository, cancellationToken));
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapGet("/api/knowledge-documents/{id:int}/diagnostics", async (
            int id,
            IWebPageRepository webPageRepository,
            CancellationToken cancellationToken) =>
        {
            var documents = await webPageRepository.GetKnowledgeSourcesAsync(cancellationToken);
            var document = documents.FirstOrDefault(item => item.Id == id);
            if (document is null)
            {
                return Results.NotFound(new { message = "Belge bulunamadı." });
            }

            return Results.Ok(new
            {
                itemType = "document",
                document = BuildKnowledgeDiagnosticDocument(document)
            });
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapDelete("/api/knowledge-documents/{id:int}", async (
            int id,
            IWebPageRepository webPageRepository,
            IKnowledgeFileImportService importService,
            IKnowledgeActivationService activationService,
            CancellationToken cancellationToken) =>
        {
            await importService.DeleteDocumentFileAsync(id, cancellationToken);
            var changed = await webPageRepository.DeleteDocumentAsync(id, cancellationToken);
            if (!changed)
            {
                return Results.NotFound(new { message = "Belge bulunamadı." });
            }

            await activationService.ActivateCacheAndQueueReadyQuestionsAsync(cancellationToken);
            return Results.Ok(await BuildKnowledgeBankResponseAsync(webPageRepository, cancellationToken));
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapGet("/api/ready-questions", async (
            HttpRequest request,
            IReadyQuestionService readyQuestionService,
            CancellationToken cancellationToken) =>
        {
            var count = request.Query.TryGetValue("count", out var countValue)
                && int.TryParse(countValue.ToString(), out var requestedCount)
                ? requestedCount
                : 4;
            var result = await readyQuestionService.GetNextAsync(count, cancellationToken);

            // Returns the computed result to the caller and completes this branch of the workflow.
            return Results.Ok(new ReadyQuestionsResponse(
                // Creates the object needed for the next step of the workflow.
                result.Questions.Select(question => new ReadyQuestionResponse(question)).ToList(),
                result.Source,
                result.GeneratedAtUtc,
                result.TotalAvailable,
                result.SourceFingerprint,
                result.IsRefreshing));
        });

        // Registers or maps application behavior into the runtime pipeline.
        app.MapPost("/api/chat/stream", async (
            ChatStreamRequest request,
            IChatService chatService,
            IRuntimeStatusService runtimeStatusService,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            // Guards the following branch so the workflow handles this condition deliberately.
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                await context.Response.WriteAsync("Message cannot be empty.", cancellationToken);
                // Returns the computed result to the caller and completes this branch of the workflow.
                return;
            }

            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers["X-Accel-Buffering"] = "no";

            try
            {
                // Creates the object needed for the next step of the workflow.
                var phasePayload = JsonSerializer.Serialize(new { type = "phase", phase = "asking" });
                // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                await context.Response.WriteAsync($"data: {phasePayload}\n\n", cancellationToken);
                // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                await context.Response.Body.FlushAsync(cancellationToken);

                // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                await foreach (var answer in chatService.StreamAnswerAsync(request.Message, cancellationToken))
                {
                    var payload = JsonSerializer.Serialize(new
                    {
                        type = "answer",
                        answer_content = answer.AnswerContent,
                        suggested_questions = answer.SuggestedQuestions,
                        source_attributions = answer.SourceAttributions.Select(attribution => new
                        {
                            source_id = attribution.SourceId,
                            source_name = attribution.SourceName,
                            source_type = attribution.SourceType,
                            document_id = attribution.DocumentId,
                            document_title = attribution.DocumentTitle,
                            display_label = attribution.DisplayLabel,
                            url = attribution.Url,
                            open_mode = attribution.OpenMode
                        })
                    });
                    // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                    await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                    // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                    await context.Response.Body.FlushAsync(cancellationToken);
                }

                // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                await context.Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            }
            catch (Exception ex)
            {
                // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                await runtimeStatusService.PublishAsync(
                    "chat",
                    "error",
                    "failed",
                    3,
                    3,
                    true,
                    "işlem kontrol edilmeli",
                    "error",
                    "alert",
                    cancellationToken: CancellationToken.None);
                // Guards the following branch so the workflow handles this condition deliberately.
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                }

                // Creates the object needed for the next step of the workflow.
                var payload = JsonSerializer.Serialize(new { type = "error", content = ex.Message });
                // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
            }
        });

        // Returns the computed result to the caller and completes this branch of the workflow.
        return app;
    }

    // Returns true when a crawler or document status indicates that user attention is needed.
    private static bool IsWarningStatus(string? statusCode)
    {
        return !string.Equals(statusCode?.Trim(), "ok", StringComparison.OrdinalIgnoreCase);
    }

    // Builds the source diagnostics payload consumed by the Knowledge Bank warning log viewer.
    private static object BuildKnowledgeDiagnosticSource(KnowledgeSource source)
    {
        return new
        {
            source.Id,
            source.SourceType,
            source.Name,
            source.Description,
            source.Address,
            source.IsEnabled,
            source.IsArchived,
            source.StatusCode,
            source.StatusLabel,
            source.StatusMessage,
            source.DocumentCount,
            source.ActiveDocumentCount,
            source.LastCheckedAtUtc,
            source.UpdatedAtUtc
        };
    }

    // Builds the document diagnostics payload consumed by the Knowledge Bank warning log viewer.
    private static object BuildKnowledgeDiagnosticDocument(WebPage document)
    {
        return new
        {
            document.Id,
            document.SourceId,
            SourceName = document.SourceName ?? "Bilinmeyen kaynak",
            document.SourceType,
            Title = string.IsNullOrWhiteSpace(document.WebTitle) ? BuildTitleFromUrl(document.WebSourceUrl) : document.WebTitle!,
            Url = document.WebSourceUrl,
            document.IsEnabled,
            document.IsArchived,
            document.StatusCode,
            document.StatusLabel,
            document.StatusMessage,
            document.HttpStatusCode,
            document.PreviewStatus,
            document.ParseStatus,
            document.OcrStatus,
            document.LastCheckedAtUtc,
            document.LastCrawledAtUtc
        };
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static async Task<KnowledgeBankResponse> BuildKnowledgeBankResponseAsync(
        IWebPageRepository webPageRepository,
        CancellationToken cancellationToken)
    {
        var sources = await webPageRepository.GetSourcesAsync(cancellationToken);
        var documents = await webPageRepository.GetKnowledgeSourcesAsync(cancellationToken);
        var folders = await webPageRepository.GetFoldersAsync(cancellationToken);
        var folderResponses = folders
            .Select(folder => new KnowledgeFolderResponse(
                folder.Id,
                folder.KnowledgeSourceGuid,
                folder.SourceFolderGuid,
                folder.FolderName,
                folder.NormalizedFolderPath))
            .ToList();
        var sourceResponses = sources
            .Select(source => new KnowledgeBankSourceResponse(
                source.Id,
                source.TenantGuid,
                source.TenantKnowledgeGuid,
                source.KnowledgeSourceGuid,
                source.SourceType,
                source.Name,
                source.Description,
                source.Address,
                source.Protocol,
                source.IsEnabled,
                source.IsArchived,
                source.StatusCode,
                source.StatusLabel,
                source.StatusMessage,
                BuildWebPageAdditionMode(source.SourceType),
                BuildWebPageAdditionModeLabel(source.SourceType),
                source.DocumentCount,
                source.ActiveDocumentCount,
                source.LastCheckedAtUtc,
                source.UpdatedAtUtc))
            .ToList();
        var documentResponses = documents
            .Select(document => new KnowledgeBankDocumentResponse(
                document.Id,
                document.SourceId,
                document.SourceName ?? "Bilinmeyen kaynak",
                document.SourceType,
                document.TenantGuid,
                document.TenantKnowledgeGuid,
                document.KnowledgeSourceGuid,
                document.SourceFolderGuid,
                document.FolderDocumentGuid,
                string.IsNullOrWhiteSpace(document.WebTitle) ? BuildTitleFromUrl(document.WebSourceUrl) : document.WebTitle!,
                document.WebSourceUrl,
                document.WebContent,
                string.IsNullOrWhiteSpace(document.ContentPreview) ? BuildDocumentPreview(document) : document.ContentPreview,
                document.IsEnabled,
                document.IsArchived,
                document.StatusCode,
                document.StatusLabel,
                document.StatusMessage,
                document.HttpStatusCode,
                document.PreviewStatus,
                document.PreviewGeneratedAtUtc,
                document.LastCheckedAtUtc,
                document.LastCrawledAtUtc,
                document.OriginalFileName,
                document.NormalizedRelativePath,
                document.NormalizedFolderPath,
                document.StoredFileName,
                document.FileExtension,
                document.FileSizeBytes,
                document.ParseStatus,
                document.OcrStatus,
                document.Origin))
            .ToList();

        return new KnowledgeBankResponse(sourceResponses.Count, documentResponses.Count, folderResponses, sourceResponses, documentResponses);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static IResult BuildKnowledgeRedownloadHttpResult(KnowledgeRedownloadResult result, KnowledgeRedownloadResponse response)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return result.Status switch
        {
            "not_found" => Results.NotFound(response),
            "conflict" => Results.Conflict(response),
            "failed_restored" => Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable),
            "failed" => Results.Json(response, statusCode: StatusCodes.Status500InternalServerError),
            "failed_restore_failed" => Results.Json(response, statusCode: StatusCodes.Status500InternalServerError),
            _ => Results.Ok(response)
        };
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static KnowledgeRedownloadResponse ToKnowledgeRedownloadResponse(KnowledgeRedownloadResult result, KnowledgeBankResponse? knowledgeBank)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return new KnowledgeRedownloadResponse(
            result.Status,
            result.BackupSetId,
            result.StartedAtUtc,
            result.CompletedAtUtc,
            result.PageCount,
            result.WarningCount,
            result.ErrorCount,
            result.ReadyQuestionsCount,
            result.SourceFingerprint,
            result.CacheBuiltAtUtc,
            result.RestoredFromBackup,
            result.Message,
            knowledgeBank);
    }

    private static async Task<AiSettingsResponse> BuildAiSettingsResponseAsync(
        IAiConfigurationService aiConfigurationService,
        IEnumerable<IAiProviderClient> providers,
        AiOptions aiOptions,
        CancellationToken cancellationToken)
    {
        var settings = await aiConfigurationService.GetAsync(cancellationToken);
        var disabledProviders = BuildDisabledProviderSet(aiOptions);
        var providerClients = providers
            .Where(provider => !disabledProviders.Contains(provider.ProviderName))
            .ToDictionary(provider => provider.ProviderName, StringComparer.OrdinalIgnoreCase);
        // Creates the object needed for the next step of the workflow.
        var providerResponses = new List<AiProviderOptionResponse>();

        // Iterates through the collection to process each item consistently.
        foreach (var providerId in new[] { "azure", "ollama-local", "ollama-cloud" }.Where(provider => !disabledProviders.Contains(provider)))
        {
            var selectedModel = settings.GetModel(providerId);
            providerClients.TryGetValue(providerId, out var providerClient);
            IReadOnlyList<AiModelDescriptor> models = providerClient is null
                ? Array.Empty<AiModelDescriptor>()
                : await providerClient.GetModelsAsync(cancellationToken);
            // Guards the following branch so the workflow handles this condition deliberately.
            if (!models.Any(model => model.Id.Equals(selectedModel, StringComparison.OrdinalIgnoreCase)))
            {
                models = models
                    // Creates the object needed for the next step of the workflow.
                    .Concat(new[] { new AiModelDescriptor(selectedModel, selectedModel, false) })
                    .ToList();
            }

            // Creates the object needed for the next step of the workflow.
            providerResponses.Add(new AiProviderOptionResponse(
                providerId,
                BuildProviderLabel(providerId),
                selectedModel,
                models.Any(model => model.IsAvailable && model.Id.Equals(selectedModel, StringComparison.OrdinalIgnoreCase)),
                models
                    // Creates the object needed for the next step of the workflow.
                    .Select(model => new AiModelOptionResponse(model.Id, model.Label, model.IsAvailable))
                    .ToList()));
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return new AiSettingsResponse(settings.ActiveProvider, settings.ActiveModel, providerResponses);
    }

    private static QnaExperienceSettingsResponse ToQnaExperienceSettingsResponse(QnaExperienceSettingsSnapshot settings)
    {
        return new QnaExperienceSettingsResponse(
            settings.DisplayedReadyQuestionCount,
            settings.DisplayedSuggestedQuestionCount,
            settings.AutoSubmitPromptButtons,
            settings.ShowAnswerSourceDocumentNames,
            settings.UpdatedAtUtc);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string NormalizeSourceType(string? sourceType)
    {
        if (sourceType?.Equals(KnowledgeSourceTypes.LocalFiles, StringComparison.OrdinalIgnoreCase) == true
            || sourceType?.Equals("Yerel Dosyalar", StringComparison.OrdinalIgnoreCase) == true)
        {
            return KnowledgeSourceTypes.LocalFiles;
        }

        if (sourceType?.Equals(KnowledgeSourceTypes.WebLinks, StringComparison.OrdinalIgnoreCase) == true
            || sourceType?.Equals("Web Bağlantıları", StringComparison.OrdinalIgnoreCase) == true)
        {
            return KnowledgeSourceTypes.WebLinks;
        }

        if (sourceType?.Equals(KnowledgeSourceTypes.WebSite, StringComparison.OrdinalIgnoreCase) == true
            || sourceType?.Equals("Web Sitesi", StringComparison.OrdinalIgnoreCase) == true)
        {
            return KnowledgeSourceTypes.WebSite;
        }

        throw new ArgumentException("Geçerli bir kaynak türü seçin.");
    }

    private static string BuildWebPageAdditionMode(string sourceType)
    {
        return sourceType.Equals(KnowledgeSourceTypes.WebSite, StringComparison.OrdinalIgnoreCase) ? "automatic" : "manual";
    }

    private static string BuildWebPageAdditionModeLabel(string sourceType)
    {
        return sourceType.Equals(KnowledgeSourceTypes.WebSite, StringComparison.OrdinalIgnoreCase) ? "Otomatik Eklenir" : "Kullanıcı Ekler";
    }

    // Normalizes source addresses only for deciding whether a web-site source must be redownloaded.
    private static string NormalizeComparableAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return string.Empty;
        }

        return address.Trim().TrimEnd('/');
    }

    private static string NormalizeHttpDocumentUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Web bağlantısı boş olamaz.");
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not "http" and not "https")
        {
            throw new ArgumentException("Geçerli bir http/https web bağlantısı girin.");
        }

        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        var path = uri.AbsolutePath.Replace('\\', '/').TrimEnd('/');
        while (path.Contains("//", StringComparison.Ordinal))
        {
            path = path.Replace("//", "/", StringComparison.Ordinal);
        }

        path = string.IsNullOrWhiteSpace(path) ? string.Empty : path;
        return $"{uri.Scheme.ToLowerInvariant()}://{host.ToLowerInvariant()}{port}{path}";
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string? NormalizeProvider(string provider)
    {
        // Guards the following branch so the workflow handles this condition deliberately.
        if (provider.Equals("azure", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("azure-cloud", StringComparison.OrdinalIgnoreCase))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return "azure";
        }

        // Guards the following branch so the workflow handles this condition deliberately.
        if (provider.Equals("ollama-local", StringComparison.OrdinalIgnoreCase))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return "ollama-local";
        }

        // Guards the following branch so the workflow handles this condition deliberately.
        if (provider.Equals("ollama-cloud", StringComparison.OrdinalIgnoreCase))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return "ollama-cloud";
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return null;
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string BuildProviderLabel(string provider)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return provider.ToLowerInvariant() switch
        {
            "ollama-local" => "Ollama Local",
            "ollama-cloud" => "Ollama Cloud",
            _ => "Azure"
        };
    }

    private static async Task<ApiHealthResponse> BuildApiHealthAsync(
        IAiChatClient aiChatClient,
        IAiProviderCatalog aiProviderCatalog,
        AiOptions aiOptions,
        IRuntimeStatusService runtimeStatusService,
        CancellationToken cancellationToken)
    {
        // Creates the object needed for the next step of the workflow.
        var components = new List<HealthComponentResponse>();
        var runtime = runtimeStatusService.Current;
        var activeProviderAlive = false;
        string? activeProviderMessage = null;
        var disabledProviders = BuildDisabledProviderSet(aiOptions);
        var providerStatuses = (await aiProviderCatalog.GetProviderStatusesAsync(cancellationToken))
            .Where(status => !disabledProviders.Contains(status.Provider))
            .ToList();

        try
        {
            activeProviderAlive = await aiChatClient.IsAvailableAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            activeProviderMessage = ex.Message;
        }

        var providerHealth = providerStatuses
            .Select(status => new HealthComponentResponse(
                status.Provider,
                status.Status,
                status.Message))
            .ToList();
        // Creates the object needed for the next step of the workflow.
        components.Add(new HealthComponentResponse(
            "active_ai_provider",
            activeProviderAlive ? "ok" : "unavailable",
            activeProviderAlive ? aiProviderCatalog.ActiveProvider : activeProviderMessage));
        // Creates the object needed for the next step of the workflow.
        components.AddRange(providerStatuses.Select(status => new HealthComponentResponse(
            $"ai_provider:{status.Provider}",
            status.Status,
            status.Message)));
        // Creates the object needed for the next step of the workflow.
        components.Add(new HealthComponentResponse(
            "runtime",
            "ok",
            runtime.Message));
        // Creates the object needed for the next step of the workflow.
        components.Add(new HealthComponentResponse(
            "chat",
            activeProviderAlive ? "ready" : "degraded",
            activeProviderAlive ? "Chat stream yanıt üretmeye hazır." : "Aktif AI provider kullanılamıyor."));

        var status = activeProviderAlive ? "ready" : "degraded";
        // Returns the computed result to the caller and completes this branch of the workflow.
        return new ApiHealthResponse(
            status,
            aiProviderCatalog.ActiveProvider,
            aiProviderCatalog.ActiveModel,
            activeProviderAlive ? "ok" : "unavailable",
            "ok",
            activeProviderAlive ? "ready" : "degraded",
            activeProviderAlive ? "API hazır." : "API ayakta ancak aktif AI provider kullanılamıyor.",
            DateTime.UtcNow,
            providerHealth,
            components);
    }

    // Builds a case-insensitive set of providers that are disabled for this runtime.
    private static HashSet<string> BuildDisabledProviderSet(AiOptions aiOptions)
    {
        return new HashSet<string>(
            aiOptions.DisabledProviders
                .Select(NormalizeProvider)
                .OfType<string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<KnowledgeHealthResponse> BuildKnowledgeHealthAsync(
        IKnowledgeSourceRefreshService knowledgeSourceRefreshService,
        IWebPageRepository webPageRepository,
        IReadyQuestionRepository readyQuestionRepository,
        ISystemInstructionCache systemInstructionCache,
        CancellationToken cancellationToken)
    {
        var checkedAt = DateTime.UtcNow;
        try
        {
            var latest = await knowledgeSourceRefreshService.GetLatestAsync(cancellationToken);
            var pages = await webPageRepository.GetKnowledgeSourcesAsync(cancellationToken);
            var readyMetadata = await readyQuestionRepository.GetMetadataAsync(cancellationToken);
            var cache = await systemInstructionCache.GetSnapshotAsync(cancellationToken);
            var fingerprintsMatch = cache is not null
                && !string.IsNullOrWhiteSpace(readyMetadata.SourceFingerprint)
                && string.Equals(cache.SourceFingerprint, readyMetadata.SourceFingerprint, StringComparison.Ordinal);

            var warningCount = latest?.WarningCount ?? 0;
            var errorCount = latest?.ErrorCount ?? 0;
            var status = BuildKnowledgeStatus(latest, pages.Count, cache, readyMetadata, fingerprintsMatch);
            // Creates the object needed for the next step of the workflow.
            var components = new List<HealthComponentResponse>
            {
                new("database", "ok", "SQLite erişilebilir."),
                new("crawler", latest?.Status.ToString() ?? "not_started", latest?.ErrorMessage),
                new("scraper", pages.Count > 0 ? "ok" : "warming_up", pages.Count > 0 ? "Metinsel kaynaklar mevcut." : "Henüz kaynak toplanmadı."),
                new("browser", latest is { Status: CrawlRunStatus.Failed, PageCount: 0 } ? "degraded" : "ok", "Playwright/Chromium crawler akışına bağlı izleniyor."),
                new("cache", cache is null ? "warming_up" : "ok", cache is null ? "Knowledge cache henüz oluşmadı." : "Knowledge cache hazır."),
                new("ready_questions", readyMetadata.TotalAvailable > 0 ? "ok" : "warming_up", readyMetadata.TotalAvailable > 0 ? "Hazır sorular mevcut." : "Hazır sorular hazırlanıyor.")
            };

            // Returns the computed result to the caller and completes this branch of the workflow.
            return new KnowledgeHealthResponse(
                status,
                "ok",
                latest?.Status.ToString() ?? "not_started",
                pages.Count > 0 ? "ok" : "warming_up",
                latest is { Status: CrawlRunStatus.Failed, PageCount: 0 } ? "degraded" : "ok",
                cache is null ? "warming_up" : "ok",
                readyMetadata.TotalAvailable > 0 ? "ok" : "warming_up",
                BuildKnowledgeMessage(status, pages.Count, warningCount, errorCount),
                pages.Count,
                warningCount,
                errorCount,
                latest?.Status.ToString(),
                latest?.StartedAtUtc,
                latest?.CompletedAtUtc,
                latest?.ErrorMessage,
                latest?.WarningMessage,
                CountDistinctSourceWebsites(pages.Select(page => page.WebSourceUrl)),
                cache?.SourceFingerprint,
                readyMetadata.SourceFingerprint,
                fingerprintsMatch,
                readyMetadata.TotalAvailable,
                readyMetadata.GeneratedAtUtc,
                cache?.BuiltAtUtc,
                checkedAt,
                components);
        }
        catch (Exception ex)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return new KnowledgeHealthResponse(
                "degraded",
                "unavailable",
                "unknown",
                "unknown",
                "unknown",
                "unknown",
                "unknown",
                $"Bilgi sağlığı kontrol edilemedi: {ex.Message}",
                0,
                0,
                1,
                null,
                null,
                null,
                ex.Message,
                null,
                0,
                null,
                null,
                false,
                0,
                null,
                null,
                checkedAt,
                // Creates the object needed for the next step of the workflow.
                new[] { new HealthComponentResponse("database", "unavailable", ex.Message) });
        }
    }

    private static string BuildKnowledgeStatus(
        CrawlRun? latest,
        int pageCount,
        SystemInstructionCacheSnapshot? cache,
        ReadyQuestionMetadata readyMetadata,
        bool fingerprintsMatch)
    {
        // Guards the following branch so the workflow handles this condition deliberately.
        if (latest?.Status == CrawlRunStatus.Running)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return "warming_up";
        }

        // Guards the following branch so the workflow handles this condition deliberately.
        if (pageCount == 0 || latest is { Status: CrawlRunStatus.Failed, PageCount: 0 })
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return "degraded";
        }

        // Guards the following branch so the workflow handles this condition deliberately.
        if ((latest?.ErrorCount ?? 0) > 0
            || (latest?.WarningCount ?? 0) > 0
            || cache is null
            || readyMetadata.TotalAvailable == 0
            || !fingerprintsMatch)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return "ready_with_warnings";
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return "ready";
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string BuildKnowledgeMessage(string status, int pageCount, int warningCount, int errorCount)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return status switch
        {
            "ready" => $"Bilgi kaynakları hazır. Toplam {pageCount} kaynak kullanılabilir.",
            "ready_with_warnings" => $"Bilgi kaynakları kullanılabilir; {warningCount} uyarı ve {errorCount} kritik hata kaydı var.",
            "warming_up" => "Bilgi kaynakları hazırlanıyor.",
            _ => "Bilgi kaynakları kontrol edilmeli."
        };
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string BuildAggregateStatus(string apiStatus, string knowledgeStatus)
    {
        // Guards the following branch so the workflow handles this condition deliberately.
        if (apiStatus == "degraded" || knowledgeStatus == "degraded")
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return "degraded";
        }

        // Guards the following branch so the workflow handles this condition deliberately.
        if (apiStatus == "warming_up" || knowledgeStatus == "warming_up")
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return "warming_up";
        }

        // Guards the following branch so the workflow handles this condition deliberately.
        if (apiStatus == "ready_with_warnings" || knowledgeStatus == "ready_with_warnings")
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return "ready_with_warnings";
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return "ready";
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static int CountDistinctSourceWebsites(IEnumerable<string> urls)
    {
        // Creates the object needed for the next step of the workflow.
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Iterates through the collection so each item is processed consistently.
        foreach (var url in urls)
        {
            // Guards the following branch so the workflow handles this condition deliberately.
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                continue;
            }

            // Creates the object needed for the next step of the workflow.
            var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                ? uri.Host[4..]
                : uri.Host;

            // Calls the collection API needed to accumulate the distinct website count.
            hosts.Add(host);
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return hosts.Count;
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string BuildDocumentPreview(WebPage page)
    {
        var text = string.IsNullOrWhiteSpace(page.WebContent) ? page.StatusMessage : page.WebContent;
        var normalized = Regex.Replace(text, @"\s+", " ").Trim();
        if (normalized.Length == 0)
        {
            return "Bu belge için önizleme henüz oluşturulamadı.";
        }

        return normalized.Length <= 260 ? normalized : $"{normalized[..260].TrimEnd()}...";
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string BuildTitleFromUrl(string url)
    {
        // Creates the object needed for the next step of the workflow.
        var path = new Uri(url).AbsolutePath.Trim('/');
        // Guards the following branch so the workflow handles this condition deliberately.
        if (string.IsNullOrWhiteSpace(path))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return "Oyak Dijital";
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return string.Join(
            " ",
            path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Last()
                .Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
