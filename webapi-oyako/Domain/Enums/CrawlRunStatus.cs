// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Enums/CrawlRunStatus.cs for maintainers.
namespace webapi_oyako.Domain.Enums;

// Lists the valid CrawlRunStatus values used by Oyako domain logic.
public enum CrawlRunStatus
{
    Running = 0,
    Completed = 1,
    Failed = 2,
    Canceled = 3
}
