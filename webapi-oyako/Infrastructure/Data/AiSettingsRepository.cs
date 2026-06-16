// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Data/AiSettingsRepository.cs for maintainers.
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Globalization;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Infrastructure.Configuration;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Infrastructure.Data;

// Implements the AiSettingsRepository component and its responsibilities in the Oyako codebase.
public sealed class AiSettingsRepository : IAiSettingsRepository
{
    // Stores state or a dependency required by the surrounding component.
    private readonly SqliteOptions _options;

    // Creates a new instance and captures the dependencies needed by this component.
    public AiSettingsRepository(IOptions<SqliteOptions> options)
    {
        _options = options.Value;
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<AiSettingsSnapshot?> GetAsync(CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(_options.ConnectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<AiSettingsRow>(
            @"SELECT
                active_provider AS ActiveProvider,
                azure_model AS AzureModel,
                ollama_local_model AS OllamaLocalModel,
                ollama_cloud_model AS OllamaCloudModel,
                updated_at_utc AS UpdatedAtUtc
              FROM ai_settings
              WHERE id = 1;");

        // Guards the following branch so the workflow handles this condition deliberately.
        if (row is null)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return null;
        }

        var updatedAtUtc = DateTime.TryParse(
            row.UpdatedAtUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : DateTime.UtcNow;

        // Returns the computed result to the caller and completes this branch of the workflow.
        return new AiSettingsSnapshot(row.ActiveProvider, row.AzureModel, row.OllamaLocalModel, row.OllamaCloudModel, updatedAtUtc);
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task UpsertAsync(AiSettingsSnapshot settings, CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(_options.ConnectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync(cancellationToken);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.ExecuteAsync(
            @"INSERT INTO ai_settings (id, active_provider, azure_model, ollama_local_model, ollama_cloud_model, updated_at_utc)
              VALUES (1, @ActiveProvider, @AzureModel, @OllamaLocalModel, @OllamaCloudModel, @UpdatedAtUtc)
              ON CONFLICT(id) DO UPDATE SET
                active_provider = excluded.active_provider,
                azure_model = excluded.azure_model,
                ollama_local_model = excluded.ollama_local_model,
                ollama_cloud_model = excluded.ollama_cloud_model,
                updated_at_utc = excluded.updated_at_utc;",
            new
            {
                settings.ActiveProvider,
                settings.AzureModel,
                settings.OllamaLocalModel,
                settings.OllamaCloudModel,
                UpdatedAtUtc = settings.UpdatedAtUtc.ToString("O")
            });
    }

    private sealed record AiSettingsRow(
        string ActiveProvider,
        string AzureModel,
        string OllamaLocalModel,
        string OllamaCloudModel,
        string UpdatedAtUtc);
}
