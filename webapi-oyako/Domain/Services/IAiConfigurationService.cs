// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Services/IAiConfigurationService.cs for maintainers.
using webapi_oyako.Domain.Models;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Domain.Services;

// Declares the IAiConfigurationService contract used to decouple Oyako layers.
public interface IAiConfigurationService
{
    AiSettingsSnapshot Current { get; }
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<AiSettingsSnapshot> GetAsync(CancellationToken cancellationToken);
    Task<AiSettingsSnapshot> UpdateAsync(string provider, string model, CancellationToken cancellationToken);
    Task<string> GetSelectedModelAsync(string provider, CancellationToken cancellationToken);
}
