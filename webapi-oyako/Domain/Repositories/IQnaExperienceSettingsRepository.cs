// Codex developer note: Declares persistence for user-facing Q&A experience settings.
using webapi_oyako.Domain.Models;

namespace webapi_oyako.Domain.Repositories;

// Stores and retrieves the single active Q&A experience settings row.
public interface IQnaExperienceSettingsRepository
{
    // Reads the active Q&A experience settings row when it exists.
    Task<QnaExperienceSettingsSnapshot?> GetAsync(CancellationToken cancellationToken);

    // Persists the active Q&A experience settings row.
    Task UpsertAsync(QnaExperienceSettingsSnapshot settings, CancellationToken cancellationToken);
}
