// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Repositories/IAiSettingsRepository.cs for maintainers.
using webapi_oyako.Domain.Models;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Domain.Repositories;

// Declares the IAiSettingsRepository contract used to decouple Oyako layers.
public interface IAiSettingsRepository
{
    Task<AiSettingsSnapshot?> GetAsync(CancellationToken cancellationToken);
    Task UpsertAsync(AiSettingsSnapshot settings, CancellationToken cancellationToken);
}
