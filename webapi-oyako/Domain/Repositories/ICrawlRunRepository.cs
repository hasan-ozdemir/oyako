// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Repositories/ICrawlRunRepository.cs for maintainers.
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Enums;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Domain.Repositories;

// Declares the ICrawlRunRepository contract used to decouple Oyako layers.
public interface ICrawlRunRepository
{
    Task<int> StartAsync(DateTime startedAtUtc, CancellationToken cancellationToken);
    Task SetCompletedAsync(
        int id,
        DateTime completedAtUtc,
        int pageCount,
        int errorCount,
        int warningCount,
        string? errorMessage,
        string? warningMessage,
        CrawlRunStatus status,
        CancellationToken cancellationToken);
    Task<CrawlRun?> GetLatestAsync(CancellationToken cancellationToken);
}
