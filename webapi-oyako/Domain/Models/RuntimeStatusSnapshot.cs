// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Models/RuntimeStatusSnapshot.cs for maintainers.
namespace webapi_oyako.Domain.Models;

// Defines the immutable RuntimeStatusSnapshot data shape exchanged between Oyako components.
public sealed record RuntimeStatusSnapshot(
    string Operation,
    string Phase,
    string StepKey,
    int StepIndex,
    int StepCount,
    bool IsTerminal,
    string Message,
    string Severity,
    string Icon,
    int? PageCount,
    DateTime UpdatedAtUtc);
