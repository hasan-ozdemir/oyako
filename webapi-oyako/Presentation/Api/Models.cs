// Codex developer note: Explains the purpose and flow of webapi-oyako/Presentation/Api/Models.cs for maintainers.
namespace webapi_oyako.Presentation.Api;

// Defines the immutable HealthComponentResponse data shape exchanged between Oyako components.
public sealed record HealthComponentResponse(
    string Name,
    string Status,
    string? Message);

// Defines the immutable ApiHealthResponse data shape exchanged between Oyako components.
public sealed record ApiHealthResponse(
    string Status,
    string ActiveAiProvider,
    string ActiveAiModel,
    string AiProvider,
    string Runtime,
    string Chat,
    string Message,
    DateTime CheckedAtUtc,
    IReadOnlyList<HealthComponentResponse> ProviderStatuses,
    IReadOnlyList<HealthComponentResponse> Components);

// Defines the immutable KnowledgeHealthResponse data shape exchanged between Oyako components.
public sealed record KnowledgeHealthResponse(
    string Status,
    string Database,
    string Crawler,
    string Scraper,
    string Browser,
    string Cache,
    string ReadyQuestions,
    string Message,
    int PageCount,
    int WarningCount,
    int ErrorCount,
    string? LastCrawlStatus,
    DateTime? LastCrawlStartedAtUtc,
    DateTime? LastCrawlCompletedAtUtc,
    string? LastCrawlErrorMessage,
    string? LastCrawlWarningMessage,
    int SourceCount,
    string? SourceFingerprint,
    string? ReadyQuestionsFingerprint,
    bool ReadyQuestionsFingerprintMatches,
    int ReadyQuestionsCount,
    DateTime? ReadyQuestionsGeneratedAtUtc,
    DateTime? KnowledgeCacheBuiltAtUtc,
    DateTime CheckedAtUtc,
    IReadOnlyList<HealthComponentResponse> Components);

// Defines the immutable HealthResponse data shape exchanged between Oyako components.
public sealed record HealthResponse(
    string Status,
    ApiHealthResponse Api,
    KnowledgeHealthResponse Knowledge,
    DateTime CheckedAtUtc);

// Defines the immutable KnowledgeRedownloadResponse data shape exchanged between Oyako components.
public sealed record KnowledgeRedownloadResponse(
    string Status,
    string BackupSetId,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    int PageCount,
    int WarningCount,
    int ErrorCount,
    int ReadyQuestionsCount,
    string? SourceFingerprint,
    DateTime? CacheBuiltAtUtc,
    bool RestoredFromBackup,
    string Message,
    KnowledgeBankResponse? KnowledgeBank);

// Defines the immutable AiSettingsResponse data shape exchanged between Oyako components.
public sealed record AiSettingsResponse(
    string ActiveProvider,
    string ActiveModel,
    IReadOnlyList<AiProviderOptionResponse> Providers);

// Defines the immutable AiProviderOptionResponse data shape exchanged between Oyako components.
public sealed record AiProviderOptionResponse(
    string Id,
    string Label,
    string SelectedModel,
    bool IsAvailable,
    IReadOnlyList<AiModelOptionResponse> Models);

// Defines the immutable AiModelOptionResponse data shape exchanged between Oyako components.
public sealed record AiModelOptionResponse(
    string Id,
    string Label,
    bool IsAvailable);

// Defines the immutable AiSettingsUpdateRequest data shape exchanged between Oyako components.
public sealed record AiSettingsUpdateRequest(
    string Provider,
    string Model);

// Defines the immutable QnaExperienceSettingsResponse data shape exchanged between Oyako components.
public sealed record QnaExperienceSettingsResponse(
    int DisplayedReadyQuestionCount,
    int DisplayedSuggestedQuestionCount,
    bool AutoSubmitPromptButtons,
    bool ShowAnswerSourceDocumentNames,
    DateTime UpdatedAtUtc);

// Defines the immutable QnaExperienceSettingsUpdateRequest data shape exchanged between Oyako components.
public sealed record QnaExperienceSettingsUpdateRequest(
    int DisplayedReadyQuestionCount,
    int DisplayedSuggestedQuestionCount,
    bool AutoSubmitPromptButtons,
    bool ShowAnswerSourceDocumentNames);

// Defines the immutable CrawlStatusResponse data shape exchanged between Oyako components.
public sealed record CrawlStatusResponse(
    int? Id,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    int? PageCount,
    int? ErrorCount,
    int? WarningCount,
    string? Status,
    string? ErrorMessage,
    string? WarningMessage);

// Defines the immutable KnowledgeBankResponse data shape exchanged between Oyako components.
public sealed record KnowledgeBankResponse(
    int SourceCount,
    int DocumentCount,
    IReadOnlyList<KnowledgeFolderResponse> Folders,
    IReadOnlyList<KnowledgeBankSourceResponse> Sources,
    IReadOnlyList<KnowledgeBankDocumentResponse> Documents);

// Defines the immutable KnowledgeFolderResponse data shape exchanged between Oyako components.
public sealed record KnowledgeFolderResponse(
    int Id,
    string KnowledgeSourceGuid,
    string SourceFolderGuid,
    string FolderName,
    string NormalizedFolderPath);

// Defines the immutable KnowledgeBankSourceResponse data shape exchanged between Oyako components.
public sealed record KnowledgeBankSourceResponse(
    int Id,
    string TenantGuid,
    string TenantKnowledgeGuid,
    string KnowledgeSourceGuid,
    string SourceType,
    string Name,
    string Description,
    string Address,
    string Protocol,
    bool IsEnabled,
    bool IsArchived,
    string StatusCode,
    string StatusLabel,
    string StatusMessage,
    string WebPageAdditionMode,
    string WebPageAdditionModeLabel,
    int DocumentCount,
    int ActiveDocumentCount,
    DateTime? LastCheckedAtUtc,
    DateTime UpdatedAtUtc);

// Defines the immutable KnowledgeBankDocumentResponse data shape exchanged between Oyako components.
public sealed record KnowledgeBankDocumentResponse(
    int Id,
    int? SourceId,
    string SourceName,
    string SourceType,
    string TenantGuid,
    string TenantKnowledgeGuid,
    string KnowledgeSourceGuid,
    string SourceFolderGuid,
    string FolderDocumentGuid,
    string Title,
    string Url,
    string Content,
    string ContentPreview,
    bool IsEnabled,
    bool IsArchived,
    string StatusCode,
    string StatusLabel,
    string StatusMessage,
    int? HttpStatusCode,
    string PreviewStatus,
    DateTime? PreviewGeneratedAtUtc,
    DateTime? LastCheckedAtUtc,
    DateTime LastCrawledAtUtc,
    string OriginalFileName,
    string NormalizedRelativePath,
    string NormalizedFolderPath,
    string StoredFileName,
    string FileExtension,
    long FileSizeBytes,
    string ParseStatus,
    string OcrStatus,
    string Origin);

// Defines the immutable KnowledgeDocumentContentResponse data shape exchanged between Oyako components.
public sealed record KnowledgeDocumentContentResponse(
    int Id,
    int? SourceId,
    string SourceName,
    string SourceType,
    string Title,
    string Url,
    string Content,
    string OriginalFileName,
    DateTime? LastCheckedAtUtc,
    DateTime LastCrawledAtUtc);

// Defines the immutable KnowledgeSourceUpsertRequest data shape exchanged between Oyako components.
public sealed record KnowledgeSourceUpsertRequest(
    string SourceType,
    string? Name,
    string? Description,
    string? Address,
    bool? IsEnabled,
    bool? Redownload);

// Defines the immutable KnowledgeDocumentUpdateRequest data shape exchanged between Oyako components.
public sealed record KnowledgeDocumentUpdateRequest(
    string Title,
    string Content,
    bool IsEnabled);

// Defines the immutable KnowledgeWebDocumentCreateRequest data shape exchanged between Oyako components.
public sealed record KnowledgeWebDocumentCreateRequest(
    string Url,
    string? Title,
    bool? IsEnabled);

// Defines the immutable KnowledgeDocumentWebLinkUpdateRequest data shape exchanged between Oyako components.
public sealed record KnowledgeDocumentWebLinkUpdateRequest(string Url);

// Defines the immutable ToggleRequest data shape exchanged between Oyako components.
public sealed record ToggleRequest(bool IsEnabled);

// Defines the immutable ArchiveRequest data shape exchanged between Oyako components.
public sealed record ArchiveRequest(bool IsArchived);

// Defines the immutable ReadyQuestionsResponse data shape exchanged between Oyako components.
public sealed record ReadyQuestionsResponse(
    IReadOnlyList<ReadyQuestionResponse> Questions,
    string Source,
    DateTime? GeneratedAtUtc,
    int TotalAvailable,
    string? SourceFingerprint,
    bool IsRefreshing);

// Defines the immutable ReadyQuestionResponse data shape exchanged between Oyako components.
public sealed record ReadyQuestionResponse(string Text);

// Defines the immutable ChatStreamRequest data shape exchanged between Oyako components.
public sealed record ChatStreamRequest(string Message);

// Defines the immutable KnowledgeFilePreviewResponse data shape exchanged between Oyako components.
public sealed record KnowledgeFilePreviewResponse(
    IReadOnlyList<KnowledgeFilePreviewItemResponse> Items,
    IReadOnlyList<string> Messages);

// Defines the immutable KnowledgeFilePreviewItemResponse data shape exchanged between Oyako components.
public sealed record KnowledgeFilePreviewItemResponse(
    string ClientFileId,
    string FileName,
    string RelativePath,
    string DefaultTitle,
    string Content,
    string ContentPreview,
    string ParseStatus,
    string OcrStatus,
    string? ErrorMessage);

// Defines the immutable KnowledgeFileImportResponse data shape exchanged between Oyako components.
public sealed record KnowledgeFileImportResponse(
    int ImportedCount,
    int UpdatedCount,
    int SkippedCount,
    IReadOnlyList<KnowledgeFileImportFailureResponse> FailedItems,
    KnowledgeBankResponse KnowledgeBank);

// Defines the immutable KnowledgeFileImportFailureResponse data shape exchanged between Oyako components.
public sealed record KnowledgeFileImportFailureResponse(
    string FileName,
    string RelativePath,
    string Message);

// Defines the immutable KnowledgeUploadSettingsResponse data shape exchanged between Oyako components.
public sealed record KnowledgeUploadSettingsResponse(
    int MaxFileSizeMb,
    int MaxBatchFileCount,
    int MaxBatchSizeMb,
    DateTime UpdatedAtUtc);

// Defines the immutable KnowledgeUploadSettingsUpdateRequest data shape exchanged between Oyako components.
public sealed record KnowledgeUploadSettingsUpdateRequest(
    int MaxFileSizeMb,
    int MaxBatchFileCount,
    int MaxBatchSizeMb);

// Defines the immutable LocalKnowledgeRebuildResponse data shape exchanged between Oyako components.
public sealed record LocalKnowledgeRebuildResponse(
    int InspectedManifestCount,
    KnowledgeBankResponse KnowledgeBank);
