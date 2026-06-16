// Codex developer note: Persists Q&A experience settings in SQLite.
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Globalization;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Infrastructure.Configuration;

namespace webapi_oyako.Infrastructure.Data;

// Implements SQLite access for the single active Q&A experience settings row.
public sealed class QnaExperienceSettingsRepository : IQnaExperienceSettingsRepository
{
    private readonly SqliteOptions _options;

    // Captures SQLite connection options.
    public QnaExperienceSettingsRepository(IOptions<SqliteOptions> options)
    {
        _options = options.Value;
    }

    // Reads the persisted Q&A experience settings row.
    public async Task<QnaExperienceSettingsSnapshot?> GetAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<QnaExperienceSettingsRow>(
            @"SELECT
                displayed_ready_question_count AS DisplayedReadyQuestionCount,
                displayed_suggested_question_count AS DisplayedSuggestedQuestionCount,
                auto_submit_prompt_buttons AS AutoSubmitPromptButtons,
                show_answer_source_document_names AS ShowAnswerSourceDocumentNames,
                updated_at_utc AS UpdatedAtUtc
              FROM qna_experience_settings
              WHERE id = 1;");
        if (row is null)
        {
            return null;
        }

        var updatedAtUtc = DateTime.TryParse(
            row.UpdatedAtUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : DateTime.UtcNow;

        return new QnaExperienceSettingsSnapshot(
            (int)row.DisplayedReadyQuestionCount,
            (int)row.DisplayedSuggestedQuestionCount,
            row.AutoSubmitPromptButtons != 0,
            row.ShowAnswerSourceDocumentNames != 0,
            updatedAtUtc);
    }

    // Upserts the persisted Q&A experience settings row.
    public async Task UpsertAsync(QnaExperienceSettingsSnapshot settings, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            @"INSERT INTO qna_experience_settings (
                id, displayed_ready_question_count, displayed_suggested_question_count,
                auto_submit_prompt_buttons, show_answer_source_document_names, updated_at_utc
              ) VALUES (
                1, @DisplayedReadyQuestionCount, @DisplayedSuggestedQuestionCount,
                @AutoSubmitPromptButtons, @ShowAnswerSourceDocumentNames, @UpdatedAtUtc
              )
              ON CONFLICT(id) DO UPDATE SET
                displayed_ready_question_count = excluded.displayed_ready_question_count,
                displayed_suggested_question_count = excluded.displayed_suggested_question_count,
                auto_submit_prompt_buttons = excluded.auto_submit_prompt_buttons,
                show_answer_source_document_names = excluded.show_answer_source_document_names,
                updated_at_utc = excluded.updated_at_utc;",
            new
            {
                settings.DisplayedReadyQuestionCount,
                settings.DisplayedSuggestedQuestionCount,
                AutoSubmitPromptButtons = settings.AutoSubmitPromptButtons ? 1 : 0,
                ShowAnswerSourceDocumentNames = settings.ShowAnswerSourceDocumentNames ? 1 : 0,
                UpdatedAtUtc = settings.UpdatedAtUtc.ToString("O")
            });
    }

    private sealed record QnaExperienceSettingsRow(
        long DisplayedReadyQuestionCount,
        long DisplayedSuggestedQuestionCount,
        long AutoSubmitPromptButtons,
        long ShowAnswerSourceDocumentNames,
        string UpdatedAtUtc);
}
