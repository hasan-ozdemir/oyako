// Codex developer note: Represents persisted user-facing Q&A experience settings.
namespace webapi_oyako.Domain.Models;

// Defines the Q&A experience settings applied by backend APIs and the web UI.
public sealed record QnaExperienceSettingsSnapshot(
    int DisplayedReadyQuestionCount,
    int DisplayedSuggestedQuestionCount,
    bool AutoSubmitPromptButtons,
    bool ShowAnswerSourceDocumentNames,
    DateTime UpdatedAtUtc)
{
    // Provides the primary default Q&A behavior for fresh databases.
    public static QnaExperienceSettingsSnapshot Default(DateTime nowUtc) => new(
        4,
        4,
        true,
        true,
        nowUtc);
}
