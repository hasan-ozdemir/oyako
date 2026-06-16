// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Services/IChatService.cs for maintainers.
using webapi_oyako.Domain.Models;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Domain.Services;

// Declares the IChatService contract used to decouple Oyako layers.
public interface IChatService
{
    Task<string> BuildSystemPromptAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<ChatAnswerSnapshot> StreamAnswerAsync(string userMessage, CancellationToken cancellationToken);
}
