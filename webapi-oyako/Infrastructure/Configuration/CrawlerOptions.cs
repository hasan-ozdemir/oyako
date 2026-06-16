// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Configuration/CrawlerOptions.cs for maintainers.
namespace webapi_oyako.Infrastructure.Configuration;

// Implements the CrawlerOptions component and its responsibilities in the Oyako codebase.
public sealed class CrawlerOptions
{
    public const string SectionName = "Crawler";

    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string SeedUrl { get; set; } = "https://oyakdijital.com.tr";
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int MaxPagesToCrawl { get; set; } = 180;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int MaxDepth { get; set; } = 8;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int RequestTimeoutSeconds { get; set; } = 5;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int MinimumRequestDelayMilliseconds { get; set; } = 500;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int MaximumRequestDelayMilliseconds { get; set; } = 1000;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int MinimumTextLengthToStore { get; set; } = 40;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int RenderTimeoutSeconds { get; set; } = 5;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int RenderStabilizationTimeoutSeconds { get; set; } = 1;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int RenderExtraWaitMilliseconds { get; set; } = 0;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public bool SourceRefreshEnabled { get; set; } = true;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int SourceRefreshIntervalMinutes { get; set; } = 60;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int SourceRefreshStartupJitterSeconds { get; set; } = 15;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int SourceRefreshRetryCount { get; set; } = 3;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int SourceRefreshRetryBaseDelaySeconds { get; set; } = 5;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int SourceRefreshMaxConsecutiveFailuresBeforeWarning { get; set; } = 3;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36 OyakoCrawler/1.0";
}
