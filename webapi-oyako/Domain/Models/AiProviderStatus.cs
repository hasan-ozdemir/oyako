// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Models/AiProviderStatus.cs for maintainers.
namespace webapi_oyako.Domain.Models;

// Defines the immutable AiProviderStatus data shape exchanged between Oyako components.
public sealed record AiProviderStatus(
    string Provider,
    string Status,
    bool IsActive,
    string? Message);
