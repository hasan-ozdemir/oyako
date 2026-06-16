// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Data/SystemInstructionCacheRepository.cs for maintainers.
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Infrastructure.Configuration;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Infrastructure.Data;

// Implements the SystemInstructionCacheRepository component and its responsibilities in the Oyako codebase.
public sealed class SystemInstructionCacheRepository : ISystemInstructionCacheRepository
{
    // Stores state or a dependency required by the surrounding component.
    private readonly SqliteOptions _options;

    // Creates a new instance and captures the dependencies needed by this component.
    public SystemInstructionCacheRepository(IOptions<SqliteOptions> options)
    {
        _options = options.Value;
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<SystemInstructionCacheEntry?> GetAsync(string cacheKey, CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(_options.ConnectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync(cancellationToken);

        // Returns the computed result to the caller and completes this branch of the workflow.
        return await connection.QuerySingleOrDefaultAsync<SystemInstructionCacheEntry>(
            @"SELECT
                cache_key AS CacheKey,
                content AS Content,
                content_hash AS ContentHash,
                source_fingerprint AS SourceFingerprint,
                page_count AS PageCount,
                built_at_utc AS BuiltAtUtc
            FROM system_instruction_cache
            WHERE cache_key = @CacheKey;",
            // Creates the object needed for the next step of the workflow.
            new { CacheKey = cacheKey });
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task UpsertAsync(SystemInstructionCacheEntry entry, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO system_instruction_cache (
                cache_key, content, content_hash, source_fingerprint, page_count, built_at_utc
            )
            VALUES (@CacheKey, @Content, @ContentHash, @SourceFingerprint, @PageCount, @BuiltAtUtc)
            ON CONFLICT(cache_key) DO UPDATE SET
                content = excluded.content,
                content_hash = excluded.content_hash,
                source_fingerprint = excluded.source_fingerprint,
                page_count = excluded.page_count,
                built_at_utc = excluded.built_at_utc;";

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(_options.ConnectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync(cancellationToken);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.ExecuteAsync(sql, new
        {
            entry.CacheKey,
            entry.Content,
            entry.ContentHash,
            entry.SourceFingerprint,
            entry.PageCount,
            BuiltAtUtc = entry.BuiltAtUtc.ToString("O")
        });
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(_options.ConnectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync(cancellationToken);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.ExecuteAsync("DELETE FROM system_instruction_cache;");
    }
}
