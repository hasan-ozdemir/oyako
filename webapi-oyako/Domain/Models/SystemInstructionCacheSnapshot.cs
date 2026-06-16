// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Models/SystemInstructionCacheSnapshot.cs for maintainers.
namespace webapi_oyako.Domain.Models;

// Defines the immutable SystemInstructionCacheSnapshot data shape exchanged between Oyako components.
public sealed record SystemInstructionCacheSnapshot(
    string ContentHash,
    string SourceFingerprint,
    int PageCount,
    DateTime BuiltAtUtc);
