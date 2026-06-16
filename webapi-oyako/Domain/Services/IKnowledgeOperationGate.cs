// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Services/IKnowledgeOperationGate.cs for maintainers.
namespace webapi_oyako.Domain.Services;

// Declares the IKnowledgeOperationGate contract used to decouple Oyako layers.
public interface IKnowledgeOperationGate
{
    Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);
    void Release();
}
