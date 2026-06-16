// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Data/CrawlRunRepository.cs for maintainers.
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Enums;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Infrastructure.Configuration;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Infrastructure.Data;

// Implements the CrawlRunRepository component and its responsibilities in the Oyako codebase.
public sealed class CrawlRunRepository : ICrawlRunRepository
{
    // Stores state or a dependency required by the surrounding component.
    private readonly SqliteOptions _options;

    // Creates a new instance and captures the dependencies needed by this component.
    public CrawlRunRepository(IOptions<SqliteOptions> options)
    {
        _options = options.Value;
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<int> StartAsync(DateTime startedAtUtc, CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(_options.ConnectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync(cancellationToken);

        var id = await connection.ExecuteScalarAsync<int>(
            @"INSERT INTO crawl_runs (
                  started_at_utc, status, page_count, error_count, warning_count
              ) VALUES (@StartedAtUtc, @Status, 0, 0, 0);
              SELECT last_insert_rowid();",
            new
            {
                StartedAtUtc = startedAtUtc.ToString("O"),
                Status = (int)CrawlRunStatus.Running
            });

        // Returns the computed result to the caller and completes this branch of the workflow.
        return id;
    }

    public async Task SetCompletedAsync(
        int id,
        DateTime completedAtUtc,
        int pageCount,
        int errorCount,
        int warningCount,
        string? errorMessage,
        string? warningMessage,
        CrawlRunStatus status,
        CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(_options.ConnectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync(cancellationToken);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.ExecuteAsync(
            @"UPDATE crawl_runs
              SET completed_at_utc = @CompletedAtUtc,
                  status = @Status,
                  page_count = @PageCount,
                  error_count = @ErrorCount,
                  warning_count = @WarningCount,
                  error_message = @ErrorMessage,
                  warning_message = @WarningMessage
              WHERE id = @Id;",
            new
            {
                Id = id,
                CompletedAtUtc = completedAtUtc.ToString("O"),
                Status = (int)status,
                PageCount = pageCount,
                ErrorCount = errorCount,
                WarningCount = warningCount,
                ErrorMessage = errorMessage,
                WarningMessage = warningMessage
            });
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<CrawlRun?> GetLatestAsync(CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(_options.ConnectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<CrawlRun>(
            @"SELECT
                id AS Id,
                started_at_utc AS StartedAtUtc,
                completed_at_utc AS CompletedAtUtc,
                status AS Status,
                page_count AS PageCount,
                error_count AS ErrorCount,
                warning_count AS WarningCount,
                error_message AS ErrorMessage,
                warning_message AS WarningMessage
              FROM crawl_runs
              ORDER BY id DESC
              LIMIT 1;");

        // Guards the following branch so the workflow handles this condition deliberately.
        if (row is not null)
        {
            row.Status = (CrawlRunStatus)row.Status;
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return row;
    }
}
