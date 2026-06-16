// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Services/IWebCrawler.cs for maintainers.
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Models;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Domain.Services;

// Declares the IWebCrawler contract used to decouple Oyako layers.
public interface IWebCrawler
{
    Task<CrawlerResult> CrawlAsync(CancellationToken cancellationToken);

    // Crawls one specific web knowledge source without touching unrelated sources.
    Task<CrawlerResult> CrawlSourceAsync(KnowledgeSource source, CancellationToken cancellationToken);

    // Crawls one specific knowledge document URL and returns a replacement document payload.
    Task<WebPage> CrawlDocumentAsync(WebPage document, CancellationToken cancellationToken);
}
