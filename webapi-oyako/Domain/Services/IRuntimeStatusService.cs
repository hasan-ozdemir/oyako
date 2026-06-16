// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Services/IRuntimeStatusService.cs for maintainers.
using webapi_oyako.Domain.Models;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Domain.Services;

// Declares the IRuntimeStatusService contract used to decouple Oyako layers.
public interface IRuntimeStatusService
{
    RuntimeStatusSnapshot Current { get; }
    Task PublishAsync(
        string operation,
        string phase,
        string stepKey,
        int stepIndex,
        int stepCount,
        bool isTerminal,
        string message,
        string severity,
        string icon,
        int? pageCount = null,
        CancellationToken cancellationToken = default);
    IAsyncEnumerable<RuntimeStatusSnapshot> WatchAsync(CancellationToken cancellationToken);
}
