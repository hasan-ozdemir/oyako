// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Services/IKnowledgeSourceRefreshService.cs for maintainers.
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Models;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Domain.Services;

// Declares the background-safe source refresh contract used to decouple Oyako layers.
public interface IKnowledgeSourceRefreshService
{
    // Refreshes active web-site knowledge sources without blocking normal application serving.
    Task<KnowledgeSourceRefreshRunResult> RefreshWebSourcesAsync(CancellationToken cancellationToken);

    // Reads the latest persisted crawl/refresh run metadata.
    Task<CrawlRun?> GetLatestAsync(CancellationToken cancellationToken);
}
