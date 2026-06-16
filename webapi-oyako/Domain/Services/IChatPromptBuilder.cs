// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Services/IChatPromptBuilder.cs for maintainers.
namespace webapi_oyako.Domain.Services;

// Declares the IChatPromptBuilder contract used to decouple Oyako layers.
public interface IChatPromptBuilder
{
    Task<string> BuildSystemPromptAsync(CancellationToken cancellationToken);
}
