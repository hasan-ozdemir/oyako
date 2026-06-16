// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Data/ReadyQuestionRepository.cs for maintainers.
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Infrastructure.Configuration;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Infrastructure.Data;

// Implements the ReadyQuestionRepository component and its responsibilities in the Oyako codebase.
public sealed class ReadyQuestionRepository : IReadyQuestionRepository
{
    private const string EligibleQuestionFilter = @"
              WHERE EXISTS (
                    SELECT 1
                    FROM ready_question_documents existing_rqd
                    WHERE existing_rqd.ready_question_id = rq.id
                )
                AND NOT EXISTS (
                    SELECT 1
                    FROM ready_question_documents invalid_rqd
                    LEFT JOIN web_pages invalid_p ON invalid_p.id = invalid_rqd.document_id
                    LEFT JOIN knowledge_sources invalid_s ON invalid_s.id = invalid_rqd.source_id
                    WHERE invalid_rqd.ready_question_id = rq.id
                      AND (
                          invalid_p.id IS NULL
                          OR invalid_s.id IS NULL
                          OR invalid_p.is_enabled <> 1
                          OR invalid_p.is_archived <> 0
                          OR invalid_p.status_code <> 'ok'
                          OR invalid_s.is_enabled <> 1
                          OR invalid_s.is_archived <> 0
                      )
                )";

    // Stores state or a dependency required by the surrounding component.
    private readonly SqliteOptions _options;

    // Creates a new instance and captures the dependencies needed by this component.
    public ReadyQuestionRepository(IOptions<SqliteOptions> options)
    {
        _options = options.Value;
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<int> CountAsync(CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(_options.ConnectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync(cancellationToken);
        // Returns the computed result to the caller and completes this branch of the workflow.
        return await connection.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*)
              FROM ready_questions rq
              " + EligibleQuestionFilter + ";");
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<string?> GetCurrentSourceFingerprintAsync(CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(_options.ConnectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync(cancellationToken);
        // Returns the computed result to the caller and completes this branch of the workflow.
        return await connection.ExecuteScalarAsync<string?>(
            @"SELECT rq.source_fingerprint
              FROM ready_questions rq
              " + EligibleQuestionFilter + @"
              ORDER BY rq.created_at_utc DESC
              LIMIT 1;");
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<ReadyQuestionMetadata> GetMetadataAsync(CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(_options.ConnectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync(cancellationToken);
        // Returns the computed result to the caller and completes this branch of the workflow.
        return await connection.QuerySingleAsync<ReadyQuestionMetadata>(
            @"SELECT
                COUNT(rq.id) AS TotalAvailable,
                MAX(rq.created_at_utc) AS GeneratedAtUtc,
                (
                    SELECT rq2.source_fingerprint
                    FROM ready_questions rq2
                    WHERE EXISTS (
                        SELECT 1
                        FROM ready_question_documents existing_rqd
                        WHERE existing_rqd.ready_question_id = rq2.id
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM ready_question_documents invalid_rqd
                        LEFT JOIN web_pages invalid_p ON invalid_p.id = invalid_rqd.document_id
                        LEFT JOIN knowledge_sources invalid_s ON invalid_s.id = invalid_rqd.source_id
                        WHERE invalid_rqd.ready_question_id = rq2.id
                          AND (
                              invalid_p.id IS NULL
                              OR invalid_s.id IS NULL
                              OR invalid_p.is_enabled <> 1
                              OR invalid_p.is_archived <> 0
                              OR invalid_p.status_code <> 'ok'
                              OR invalid_s.is_enabled <> 1
                              OR invalid_s.is_archived <> 0
                          )
                    )
                    ORDER BY rq2.created_at_utc DESC
                    LIMIT 1
                ) AS SourceFingerprint
              FROM ready_questions rq
              " + EligibleQuestionFilter + ";");
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<IReadOnlyList<ReadyQuestion>> GetNextAsync(int count, CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(_options.ConnectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync(cancellationToken);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var rows = (await connection.QueryAsync<ReadyQuestion>(
            @"SELECT
                rq.id AS Id,
                rq.text AS Text,
                rq.source_fingerprint AS SourceFingerprint,
                rq.served_count AS ServedCount,
                rq.last_served_at_utc AS LastServedAtUtc,
                rq.created_at_utc AS CreatedAtUtc
              FROM ready_questions rq
              " + EligibleQuestionFilter + @"
              ORDER BY rq.served_count ASC, RANDOM()
              LIMIT @Count;",
            // Creates the object needed for the next step of the workflow.
            new { Count = count },
            transaction)).ToList();

        // Guards the following branch so the workflow handles this condition deliberately.
        if (rows.Count > 0)
        {
            var references = (await connection.QueryAsync<ReadyQuestionDocumentReferenceRow>(
                @"SELECT
                    ready_question_id AS ReadyQuestionId,
                    source_id AS SourceId,
                    document_id AS DocumentId,
                    document_content_hash AS DocumentContentHash
                  FROM ready_question_documents
                  WHERE ready_question_id IN @Ids
                  ORDER BY ready_question_id, document_id;",
                new { Ids = rows.Select(row => row.Id).ToArray() },
                transaction)).ToList();
            var referencesByQuestion = references
                .GroupBy(reference => reference.ReadyQuestionId)
                .ToDictionary(
                    group => (int)group.Key,
                    group => (IReadOnlyList<ReadyQuestionDocumentReference>)group
                        .Select(reference => new ReadyQuestionDocumentReference(
                            (int)reference.SourceId,
                            (int)reference.DocumentId,
                            reference.DocumentContentHash))
                        .ToArray());
            foreach (var row in rows)
            {
                if (referencesByQuestion.TryGetValue(row.Id, out var rowReferences))
                {
                    row.DocumentReferences = rowReferences;
                }
            }

            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await connection.ExecuteAsync(
                @"UPDATE ready_questions
                  SET served_count = served_count + 1,
                      last_served_at_utc = @Now
                  WHERE id IN @Ids;",
                new
                {
                    Now = DateTime.UtcNow.ToString("O"),
                    Ids = rows.Select(row => row.Id).ToArray()
                },
                transaction);
        }

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await transaction.CommitAsync(cancellationToken);
        // Returns the computed result to the caller and completes this branch of the workflow.
        return rows;
    }

    public async Task ReplaceAllAsync(
        IReadOnlyList<ReadyQuestionCandidate> questions,
        string sourceFingerprint,
        DateTime createdAtUtc,
        CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(_options.ConnectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync(cancellationToken);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.ExecuteAsync("DELETE FROM ready_questions;", transaction: transaction);
        // Iterates through the collection to process each item consistently.
        foreach (var question in questions)
        {
            var references = question.DocumentReferences
                .GroupBy(reference => reference.DocumentId)
                .Select(group => group.First())
                .ToArray();
            if (references.Length == 0)
            {
                continue;
            }

            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            var readyQuestionId = await connection.ExecuteScalarAsync<long>(
                @"INSERT INTO ready_questions (
                    text, source_fingerprint, served_count, last_served_at_utc, created_at_utc
                  ) VALUES (
                    @Text, @SourceFingerprint, 0, NULL, @CreatedAtUtc
                  );
                  SELECT last_insert_rowid();",
                new
                {
                    question.Text,
                    SourceFingerprint = sourceFingerprint,
                    CreatedAtUtc = createdAtUtc.ToString("O")
                },
                transaction);

            foreach (var reference in references)
            {
                await connection.ExecuteAsync(
                    @"INSERT INTO ready_question_documents (
                        ready_question_id, source_id, document_id, document_content_hash, created_at_utc
                      ) VALUES (
                        @ReadyQuestionId, @SourceId, @DocumentId, @DocumentContentHash, @CreatedAtUtc
                      );",
                    new
                    {
                        ReadyQuestionId = readyQuestionId,
                        reference.SourceId,
                        reference.DocumentId,
                        reference.DocumentContentHash,
                        CreatedAtUtc = createdAtUtc.ToString("O")
                    },
                    transaction);
            }
        }

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await transaction.CommitAsync(cancellationToken);
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(_options.ConnectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync(cancellationToken);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.ExecuteAsync("DELETE FROM ready_questions;");
    }

    private sealed record ReadyQuestionDocumentReferenceRow(
        long ReadyQuestionId,
        long SourceId,
        long DocumentId,
        string DocumentContentHash);
}
