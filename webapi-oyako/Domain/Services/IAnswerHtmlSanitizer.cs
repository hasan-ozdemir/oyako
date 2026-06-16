// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Services/IAnswerHtmlSanitizer.cs for maintainers.
using webapi_oyako.Domain.Models;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Domain.Services;

// Declares the IAnswerHtmlSanitizer contract used to decouple Oyako layers.
public interface IAnswerHtmlSanitizer
{
    ChatAnswerSnapshot RenderAssistantMarkdown(string markdown, int suggestedQuestionLimit = 5, bool enableActionLinks = true);
}
