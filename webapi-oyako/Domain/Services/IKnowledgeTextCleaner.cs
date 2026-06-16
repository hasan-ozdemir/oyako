// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Services/IKnowledgeTextCleaner.cs for maintainers.
namespace webapi_oyako.Domain.Services;

// Declares the IKnowledgeTextCleaner contract used to decouple Oyako layers.
public interface IKnowledgeTextCleaner
{
    string Clean(string text);
    string BuildPreview(string text, string? title = null, int maxLength = 220);
}
