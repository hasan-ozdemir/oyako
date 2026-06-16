// Codex developer note: Declares application behavior for Q&A experience settings.
using webapi_oyako.Domain.Models;

namespace webapi_oyako.Domain.Services;

// Provides validated access to persisted Q&A experience settings.
public interface IQnaExperienceSettingsService
{
    // Reads the active Q&A experience settings, returning defaults for a fresh database.
    Task<QnaExperienceSettingsSnapshot> GetAsync(CancellationToken cancellationToken);

    // Validates and persists the active Q&A experience settings.
    Task<QnaExperienceSettingsSnapshot> UpdateAsync(
        int displayedReadyQuestionCount,
        int displayedSuggestedQuestionCount,
        bool autoSubmitPromptButtons,
        bool showAnswerSourceDocumentNames,
        CancellationToken cancellationToken);
}
