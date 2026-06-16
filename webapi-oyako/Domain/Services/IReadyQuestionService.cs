// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Services/IReadyQuestionService.cs for maintainers.
using webapi_oyako.Domain.Models;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Domain.Services;

// Declares the IReadyQuestionService contract used to decouple Oyako layers.
public interface IReadyQuestionService
{
    Task<ReadyQuestionSet> GetNextAsync(int count, CancellationToken cancellationToken);
    void QueueRefreshFromKnowledge();
    Task<bool> RefreshFromKnowledgeAsync(CancellationToken cancellationToken);
    Task<ReadyQuestionRefreshResult> ForceRefreshFromKnowledgeAsync(CancellationToken cancellationToken);
}
