// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Services/IAiChatClient.cs for maintainers.
using webapi_oyako.Domain.Models;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Domain.Services;

// Declares the IAiChatClient contract used to decouple Oyako layers.
public interface IAiChatClient
{
    string ProviderName { get; }

    IAsyncEnumerable<string> StreamChatAsync(
        string systemInstruction,
        string userMessage,
        CancellationToken cancellationToken);

    Task<string> CompleteChatAsync(
        string systemInstruction,
        string userMessage,
        CancellationToken cancellationToken);

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}

// Declares the IAiProviderClient contract used to decouple Oyako layers.
public interface IAiProviderClient : IAiChatClient
{
    Task<IReadOnlyList<AiModelDescriptor>> GetModelsAsync(CancellationToken cancellationToken);
}

// Declares the IAiProviderCatalog contract used to decouple Oyako layers.
public interface IAiProviderCatalog
{
    string ActiveProvider { get; }
    string ActiveModel { get; }

    Task<IReadOnlyList<AiProviderStatus>> GetProviderStatusesAsync(CancellationToken cancellationToken);
}
