// Codex developer note: Implements validation and persistence flow for Q&A experience settings.
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Domain.Services;

namespace webapi_oyako.Application.Services;

// Validates Q&A experience settings before they become active.
public sealed class QnaExperienceSettingsService : IQnaExperienceSettingsService
{
    private readonly IQnaExperienceSettingsRepository _repository;

    // Captures the repository used to persist the single active settings row.
    public QnaExperienceSettingsService(IQnaExperienceSettingsRepository repository)
    {
        _repository = repository;
    }

    // Reads persisted settings or returns the primary defaults for a fresh database.
    public async Task<QnaExperienceSettingsSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        var settings = await _repository.GetAsync(cancellationToken);
        return settings ?? QnaExperienceSettingsSnapshot.Default(DateTime.UtcNow);
    }

    // Validates and persists settings that directly affect ready/suggested question UX.
    public async Task<QnaExperienceSettingsSnapshot> UpdateAsync(
        int displayedReadyQuestionCount,
        int displayedSuggestedQuestionCount,
        bool autoSubmitPromptButtons,
        bool showAnswerSourceDocumentNames,
        CancellationToken cancellationToken)
    {
        if (displayedReadyQuestionCount is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(displayedReadyQuestionCount), "Gösterilen hazır soru sayısı 1 ile 10 arasında olmalıdır.");
        }

        if (displayedSuggestedQuestionCount is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(displayedSuggestedQuestionCount), "Gösterilen önerilen soru sayısı 1 ile 10 arasında olmalıdır.");
        }

        var settings = new QnaExperienceSettingsSnapshot(
            displayedReadyQuestionCount,
            displayedSuggestedQuestionCount,
            autoSubmitPromptButtons,
            showAnswerSourceDocumentNames,
            DateTime.UtcNow);
        await _repository.UpsertAsync(settings, cancellationToken);
        return settings;
    }
}
