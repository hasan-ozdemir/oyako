// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Services/ISystemInstructionCache.cs for maintainers.
using webapi_oyako.Domain.Models;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Domain.Services;

// Declares the ISystemInstructionCache contract used to decouple Oyako layers.
public interface ISystemInstructionCache
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<string> GetCurrentAsync(CancellationToken cancellationToken);
    Task<SystemInstructionCacheSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken);
    Task InvalidateAsync(CancellationToken cancellationToken);
    Task ForceRefreshAsync(CancellationToken cancellationToken);
    Task<bool> RecomposeFromActiveBlocksAsync(CancellationToken cancellationToken);
    Task ReloadFromStoreAsync(CancellationToken cancellationToken);
    Task<bool> RefreshIfChangedAsync(CancellationToken cancellationToken);
}
