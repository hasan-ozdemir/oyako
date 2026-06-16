// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Services/IAnswerActionLinkifier.cs for maintainers.
namespace webapi_oyako.Domain.Services;

// Declares the IAnswerActionLinkifier contract used to enrich assistant HTML with safe, actionable links.
public interface IAnswerActionLinkifier
{
    // Converts contact-like plain text inside assistant HTML into safe actionable anchor elements.
    string Linkify(string html);
}
