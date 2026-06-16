// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Repositories/IReadyQuestionRepository.cs for maintainers.
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Models;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Domain.Repositories;

// Declares the IReadyQuestionRepository contract used to decouple Oyako layers.
public interface IReadyQuestionRepository
{
    Task<int> CountAsync(CancellationToken cancellationToken);
    Task<string?> GetCurrentSourceFingerprintAsync(CancellationToken cancellationToken);
    Task<ReadyQuestionMetadata> GetMetadataAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ReadyQuestion>> GetNextAsync(int count, CancellationToken cancellationToken);
    Task ReplaceAllAsync(
        IReadOnlyList<ReadyQuestionCandidate> questions,
        string sourceFingerprint,
        DateTime createdAtUtc,
        CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}
