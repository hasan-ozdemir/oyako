// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Data/KnowledgeStoreMaintenanceRepository.cs for maintainers.
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Infrastructure.Configuration;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Infrastructure.Data;

public sealed partial class KnowledgeStoreMaintenanceRepository : IKnowledgeStoreMaintenanceRepository
{
    // Stores state or a dependency required by the surrounding component.
    private readonly SqliteOptions _options;

    // Creates a new instance and captures the dependencies needed by this component.
    public KnowledgeStoreMaintenanceRepository(IOptions<SqliteOptions> options)
    {
        _options = options.Value;
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task BackupAsync(string backupSetId, CancellationToken cancellationToken)
    {
        ValidateBackupSetId(backupSetId);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(_options.ConnectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync(cancellationToken);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Iterates through the collection to process each item consistently.
        foreach (var table in KnowledgeTables)
        {
            var backupTable = BuildBackupTableName(backupSetId, table);
            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await connection.ExecuteAsync($"DROP TABLE IF EXISTS {backupTable};", transaction: transaction);
            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await connection.ExecuteAsync($"CREATE TABLE {backupTable} AS SELECT * FROM {table};", transaction: transaction);
        }

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await transaction.CommitAsync(cancellationToken);
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task ClearKnowledgeTablesAsync(CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(_options.ConnectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync(cancellationToken);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync("DELETE FROM web_pages WHERE origin = 'web_crawl';", transaction: transaction);
        await connection.ExecuteAsync("DELETE FROM knowledge_document_cache_blocks;", transaction: transaction);
        await connection.ExecuteAsync("DELETE FROM system_instruction_cache;", transaction: transaction);
        await connection.ExecuteAsync("DELETE FROM ready_question_documents;", transaction: transaction);
        await connection.ExecuteAsync("DELETE FROM ready_questions;", transaction: transaction);

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await transaction.CommitAsync(cancellationToken);
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task RestoreAsync(string backupSetId, CancellationToken cancellationToken)
    {
        ValidateBackupSetId(backupSetId);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(_options.ConnectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync(cancellationToken);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Iterates through the collection to process each item consistently.
        foreach (var table in KnowledgeTables.Reverse())
        {
            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await connection.ExecuteAsync($"DELETE FROM {table};", transaction: transaction);
        }

        // Iterates through the collection to process each item consistently.
        foreach (var table in KnowledgeTables)
        {
            var backupTable = BuildBackupTableName(backupSetId, table);
            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await connection.ExecuteAsync($"INSERT INTO {table} SELECT * FROM {backupTable};", transaction: transaction);
        }

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await transaction.CommitAsync(cancellationToken);
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task CleanupBackupsExceptAsync(string backupSetIdToKeep, CancellationToken cancellationToken)
    {
        ValidateBackupSetId(backupSetIdToKeep);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(_options.ConnectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync(cancellationToken);

        var names = await connection.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name LIKE 'kr_backup_%';");
        var keepPrefix = $"kr_backup_{backupSetIdToKeep}_";
        // Iterates through the collection to process each item consistently.
        foreach (var name in names.Where(name => !name.StartsWith(keepPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            // Guards the following branch so the workflow handles this condition deliberately.
            if (BackupTableNameRegex().IsMatch(name))
            {
                // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                await connection.ExecuteAsync($"DROP TABLE IF EXISTS {name};");
            }
        }
    }

    private static readonly string[] KnowledgeTables =
    [
        "knowledge_sources",
        "web_pages",
        "knowledge_document_cache_blocks",
        "system_instruction_cache",
        "ready_questions",
        "ready_question_documents"
    ];

    // Executes this component behavior as part of the Oyako application flow.
    private static string BuildBackupTableName(string backupSetId, string sourceTable)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return $"kr_backup_{backupSetId}_{sourceTable}";
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static void ValidateBackupSetId(string backupSetId)
    {
        // Guards the following branch so the workflow handles this condition deliberately.
        if (!BackupSetIdRegex().IsMatch(backupSetId))
        {
            // Stops the current workflow with an explicit failure that upstream handlers can report.
            throw new ArgumentException("Invalid backup set id.", nameof(backupSetId));
        }
    }

    [GeneratedRegex("^[a-zA-Z0-9_]+$")]
    // Executes this component behavior as part of the Oyako application flow.
    private static partial Regex BackupSetIdRegex();

    [GeneratedRegex("^kr_backup_[a-zA-Z0-9_]+_(knowledge_sources|web_pages|knowledge_document_cache_blocks|system_instruction_cache|ready_questions|ready_question_documents)$")]
    // Executes this component behavior as part of the Oyako application flow.
    private static partial Regex BackupTableNameRegex();
}
