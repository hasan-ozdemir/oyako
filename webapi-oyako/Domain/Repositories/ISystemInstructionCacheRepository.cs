// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Repositories/ISystemInstructionCacheRepository.cs for maintainers.
using webapi_oyako.Domain.Entities;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Domain.Repositories;

// Declares the ISystemInstructionCacheRepository contract used to decouple Oyako layers.
public interface ISystemInstructionCacheRepository
{
    Task<SystemInstructionCacheEntry?> GetAsync(string cacheKey, CancellationToken cancellationToken);
    Task UpsertAsync(SystemInstructionCacheEntry entry, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}
