// Codex developer note: Explains the purpose and flow of webapi-oyako/Application/Services/KnowledgeOperationGate.cs for maintainers.
using webapi_oyako.Domain.Services;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Application.Services;

// Implements the KnowledgeOperationGate component and its responsibilities in the Oyako codebase.
public sealed class KnowledgeOperationGate : IKnowledgeOperationGate
{
    // Stores state or a dependency required by the surrounding component.
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // Executes this component behavior as part of the Oyako application flow.
    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return _semaphore.WaitAsync(timeout, cancellationToken);
    }

    // Executes this component behavior as part of the Oyako application flow.
    public void Release()
    {
        _semaphore.Release();
    }
}
