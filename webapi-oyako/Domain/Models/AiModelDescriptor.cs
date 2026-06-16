// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Models/AiModelDescriptor.cs for maintainers.
namespace webapi_oyako.Domain.Models;

// Defines the immutable AiModelDescriptor data shape exchanged between Oyako components.
public sealed record AiModelDescriptor(
    string Id,
    string Label,
    bool IsAvailable);
