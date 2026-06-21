// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Data/SqliteDbInitializer.cs for maintainers.
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using webapi_oyako.Domain.Entities;
using webapi_oyako.Infrastructure.Configuration;

namespace webapi_oyako.Infrastructure.Data;

// Bootstraps the final pre-alpha SQLite schema and required seed data.
public sealed class SqliteDbInitializer
{
    // Stores the current application version used by final-schema metadata.
    public const string AppVersion = "v2026.6.21.400";
    // Stores the schema version that represents this aggressive cutover.
    private const string SchemaVersion = "v2026.6.21.400-tenant-seed-refresh";

    private readonly SqliteOptions _options;
    private readonly AiOptions _aiOptions;
    private readonly AzureAiOptions _azureAiOptions;
    private readonly OllamaLocalOptions _ollamaLocalOptions;
    private readonly OllamaCloudOptions _ollamaCloudOptions;
    private readonly TenantOptions _tenantOptions;

    public SqliteDbInitializer(
        IOptions<SqliteOptions> options,
        IOptions<AiOptions> aiOptions,
        IOptions<AzureAiOptions> azureAiOptions,
        IOptions<OllamaLocalOptions> ollamaLocalOptions,
        IOptions<OllamaCloudOptions> ollamaCloudOptions,
        IOptions<TenantOptions> tenantOptions)
    {
        _options = options.Value;
        _aiOptions = aiOptions.Value;
        _azureAiOptions = azureAiOptions.Value;
        _ollamaLocalOptions = ollamaLocalOptions.Value;
        _ollamaCloudOptions = ollamaCloudOptions.Value;
        _tenantOptions = tenantOptions.Value;
    }

    // Creates or resets the database to the final schema expected by this version.
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder(_options.ConnectionString);
        var directory = Path.GetDirectoryName(builder.DataSource);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = new SqliteConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteNonQueryAsync("PRAGMA foreign_keys = OFF;", cancellationToken);

        var currentVersion = await ReadSchemaVersionAsync(connection, cancellationToken);
        if (!string.Equals(currentVersion, SchemaVersion, StringComparison.Ordinal))
        {
            await DropFinalTablesAsync(connection, cancellationToken);
        }

        await EnsurePrimaryAiSettingsSchemaAsync(connection, cancellationToken);
        await CreateFinalTablesAsync(connection, cancellationToken);
        await SeedRequiredDataAsync(connection, cancellationToken);
        await connection.ExecuteNonQueryAsync("PRAGMA foreign_keys = ON;", cancellationToken);
    }

    // Reads the schema version if the metadata table already exists.
    private static async Task<string?> ReadSchemaVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'schema_metadata';";
        var exists = await existsCommand.ExecuteScalarAsync(cancellationToken);
        if (exists is null)
        {
            return null;
        }

        using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "SELECT value FROM schema_metadata WHERE key = 'schema_version';";
        return await versionCommand.ExecuteScalarAsync(cancellationToken) as string;
    }

    // Drops known final-schema tables so old pre-alpha schemas cannot remain active.
    private static async Task DropFinalTablesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var tables = new[]
        {
            "web_pages",
            "raw_files",
            "knowledge_folders",
            "knowledge_sources",
            "knowledge_document_cache_blocks",
            "knowledge_upload_settings",
            "knowledge_refresh_settings",
            "knowledge_bank_metadata",
            "tenant_metadata",
            "system_instruction_cache",
            "ready_question_documents",
            "ready_questions",
            "crawl_runs",
            "ai_settings",
            "qna_experience_settings",
            "schema_metadata"
        };

        foreach (var table in tables)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS {table};";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    // Creates the final SQLite schema used by the app.
    private static async Task CreateFinalTablesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var sql = @"
            CREATE TABLE IF NOT EXISTS schema_metadata (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS tenant_metadata (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                tenant_guid TEXT NOT NULL UNIQUE,
                name TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS knowledge_bank_metadata (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                tenant_guid TEXT NOT NULL,
                tenant_knowledge_guid TEXT NOT NULL UNIQUE,
                name TEXT NOT NULL,
                version TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS knowledge_sources (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                tenant_guid TEXT NOT NULL,
                tenant_knowledge_guid TEXT NOT NULL,
                knowledge_source_guid TEXT NOT NULL UNIQUE,
                source_type TEXT NOT NULL,
                name TEXT NOT NULL,
                description TEXT NOT NULL DEFAULT '',
                address TEXT NOT NULL UNIQUE,
                protocol TEXT NOT NULL,
                is_enabled INTEGER NOT NULL DEFAULT 1,
                is_archived INTEGER NOT NULL DEFAULT 0,
                status_code TEXT NOT NULL DEFAULT 'ok',
                status_label TEXT NOT NULL DEFAULT 'Tamam',
                status_message TEXT NOT NULL DEFAULT 'Kaynak kullanılabilir.',
                last_checked_at_utc TEXT NULL,
                is_seed_managed INTEGER NOT NULL DEFAULT 0,
                seed_key TEXT NOT NULL DEFAULT '',
                refresh_period_minutes INTEGER NOT NULL DEFAULT 60,
                auto_refresh_enabled INTEGER NOT NULL DEFAULT 0,
                last_refresh_at_utc TEXT NULL,
                next_refresh_at_utc TEXT NULL,
                created_at_utc TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS knowledge_folders (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                tenant_guid TEXT NOT NULL,
                tenant_knowledge_guid TEXT NOT NULL,
                knowledge_source_guid TEXT NOT NULL,
                source_folder_guid TEXT NOT NULL UNIQUE,
                folder_name TEXT NOT NULL,
                normalized_folder_path TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL,
                UNIQUE(tenant_guid, tenant_knowledge_guid, knowledge_source_guid, normalized_folder_path)
            );

            CREATE TABLE IF NOT EXISTS web_pages (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                source_id INTEGER NULL,
                tenant_guid TEXT NOT NULL DEFAULT '',
                tenant_knowledge_guid TEXT NOT NULL DEFAULT '',
                knowledge_source_guid TEXT NOT NULL DEFAULT '',
                source_folder_guid TEXT NOT NULL DEFAULT '',
                folder_document_guid TEXT NOT NULL DEFAULT '',
                web_source_url TEXT NOT NULL UNIQUE,
                web_title TEXT NULL,
                web_content TEXT NOT NULL,
                content_preview TEXT NOT NULL DEFAULT '',
                is_enabled INTEGER NOT NULL DEFAULT 1,
                is_archived INTEGER NOT NULL DEFAULT 0,
                status_code TEXT NOT NULL DEFAULT 'ok',
                status_label TEXT NOT NULL DEFAULT 'Tamam',
                status_message TEXT NOT NULL DEFAULT 'Belge kullanılabilir.',
                http_status_code INTEGER NULL,
                preview_status TEXT NOT NULL DEFAULT 'deterministic',
                preview_generated_at_utc TEXT NULL,
                last_checked_at_utc TEXT NULL,
                content_hash TEXT NOT NULL,
                first_seen_at_utc TEXT NOT NULL,
                last_seen_at_utc TEXT NOT NULL,
                last_crawled_at_utc TEXT NOT NULL,
                original_file_name TEXT NOT NULL DEFAULT '',
                normalized_relative_path TEXT NOT NULL DEFAULT '',
                normalized_folder_path TEXT NOT NULL DEFAULT '',
                storage_directory TEXT NOT NULL DEFAULT '',
                stored_file_name TEXT NOT NULL DEFAULT '',
                file_extension TEXT NOT NULL DEFAULT '',
                file_size_bytes INTEGER NOT NULL DEFAULT 0,
                file_hash TEXT NOT NULL DEFAULT '',
                parse_status TEXT NOT NULL DEFAULT '',
                ocr_status TEXT NOT NULL DEFAULT '',
                origin TEXT NOT NULL DEFAULT 'web_crawl',
                FOREIGN KEY(source_id) REFERENCES knowledge_sources(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS raw_files (
                raw_file_id TEXT PRIMARY KEY,
                source_id INTEGER NOT NULL,
                document_id INTEGER NULL,
                tenant_guid TEXT NOT NULL,
                tenant_knowledge_guid TEXT NOT NULL,
                knowledge_source_guid TEXT NOT NULL,
                source_folder_guid TEXT NOT NULL,
                folder_document_guid TEXT NOT NULL,
                original_file_name TEXT NOT NULL,
                normalized_folder_path TEXT NOT NULL,
                normalized_relative_path TEXT NOT NULL,
                storage_directory TEXT NOT NULL,
                stored_file_name TEXT NOT NULL,
                extension TEXT NOT NULL,
                mime_type TEXT NOT NULL DEFAULT '',
                file_size_bytes INTEGER NOT NULL,
                file_hash TEXT NOT NULL,
                content_hash TEXT NOT NULL,
                parse_status TEXT NOT NULL,
                ocr_status TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL,
                UNIQUE(source_id, normalized_folder_path, normalized_relative_path),
                FOREIGN KEY(source_id) REFERENCES knowledge_sources(id) ON DELETE CASCADE,
                FOREIGN KEY(document_id) REFERENCES web_pages(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS crawl_runs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                started_at_utc TEXT NOT NULL,
                completed_at_utc TEXT NULL,
                status INTEGER NOT NULL,
                page_count INTEGER NOT NULL,
                error_count INTEGER NOT NULL,
                warning_count INTEGER NOT NULL DEFAULT 0,
                error_message TEXT NULL,
                warning_message TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS system_instruction_cache (
                cache_key TEXT PRIMARY KEY,
                content TEXT NOT NULL,
                content_hash TEXT NOT NULL,
                source_fingerprint TEXT NOT NULL,
                page_count INTEGER NOT NULL,
                built_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS knowledge_document_cache_blocks (
                document_id INTEGER PRIMARY KEY,
                source_id INTEGER NOT NULL,
                source_name TEXT NOT NULL,
                source_type TEXT NOT NULL,
                document_title TEXT NOT NULL,
                document_url TEXT NOT NULL,
                document_citation_label TEXT NOT NULL,
                content_hash TEXT NOT NULL,
                prompt_block TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL,
                FOREIGN KEY(document_id) REFERENCES web_pages(id) ON DELETE CASCADE,
                FOREIGN KEY(source_id) REFERENCES knowledge_sources(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS ready_questions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                text TEXT NOT NULL,
                source_fingerprint TEXT NOT NULL,
                served_count INTEGER NOT NULL DEFAULT 0,
                last_served_at_utc TEXT NULL,
                created_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ready_question_documents (
                ready_question_id INTEGER NOT NULL,
                source_id INTEGER NOT NULL,
                document_id INTEGER NOT NULL,
                document_content_hash TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                PRIMARY KEY(ready_question_id, document_id),
                FOREIGN KEY(ready_question_id) REFERENCES ready_questions(id) ON DELETE CASCADE,
                FOREIGN KEY(source_id) REFERENCES knowledge_sources(id) ON DELETE CASCADE,
                FOREIGN KEY(document_id) REFERENCES web_pages(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS ai_settings (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                active_provider TEXT NOT NULL,
                azure_model TEXT NOT NULL,
                ollama_local_model TEXT NOT NULL,
                ollama_cloud_model TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS knowledge_upload_settings (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                max_file_size_mb INTEGER NOT NULL,
                max_batch_file_count INTEGER NOT NULL,
                max_batch_size_mb INTEGER NOT NULL,
                updated_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS knowledge_refresh_settings (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                refresh_period_minutes INTEGER NOT NULL,
                updated_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS qna_experience_settings (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                displayed_ready_question_count INTEGER NOT NULL CHECK(displayed_ready_question_count BETWEEN 1 AND 10),
                displayed_suggested_question_count INTEGER NOT NULL CHECK(displayed_suggested_question_count BETWEEN 1 AND 10),
                auto_submit_prompt_buttons INTEGER NOT NULL CHECK(auto_submit_prompt_buttons IN (0, 1)),
                show_answer_source_document_names INTEGER NOT NULL CHECK(show_answer_source_document_names IN (0, 1)),
                updated_at_utc TEXT NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS idx_web_pages_url ON web_pages(web_source_url);
            CREATE INDEX IF NOT EXISTS idx_web_pages_source_id ON web_pages(source_id);
            CREATE INDEX IF NOT EXISTS idx_web_pages_active_lookup ON web_pages(source_id, is_enabled, is_archived, status_code);
            CREATE INDEX IF NOT EXISTS idx_web_pages_local_identity ON web_pages(source_id, normalized_folder_path, normalized_relative_path);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_knowledge_sources_address ON knowledge_sources(address);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_knowledge_sources_seed_key ON knowledge_sources(seed_key) WHERE seed_key <> '';
            CREATE INDEX IF NOT EXISTS idx_knowledge_sources_active_lookup ON knowledge_sources(is_enabled, is_archived, source_type);
            CREATE INDEX IF NOT EXISTS idx_document_cache_blocks_source ON knowledge_document_cache_blocks(source_id, content_hash);
            CREATE INDEX IF NOT EXISTS idx_ready_questions_rotation ON ready_questions(served_count, last_served_at_utc);
            CREATE INDEX IF NOT EXISTS idx_ready_question_documents_question ON ready_question_documents(ready_question_id);
            CREATE INDEX IF NOT EXISTS idx_ready_question_documents_document ON ready_question_documents(document_id);
            CREATE INDEX IF NOT EXISTS idx_ready_question_documents_source ON ready_question_documents(source_id);
        ";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Seeds tenant, knowledge bank, tenant env seed sources, upload settings, and AI settings.
    private async Task SeedRequiredDataAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (_tenantOptions.KnowledgeSources.Count == 0)
        {
            throw new InvalidOperationException("Tenant configuration must declare at least one tenant knowledge source before SQLite can be initialized.");
        }

        var now = DateTime.UtcNow.ToString("O");
        var tenantGuid = _tenantOptions.Id.Trim();
        var tenantDisplayName = _tenantOptions.DisplayName.Trim();
        var knowledgeBankName = _tenantOptions.UiWebKnowledgeBankHeaderTitle.Trim();
        var knowledgeGuid = BuildDeterministicGuid($"{tenantGuid}:knowledge-bank");
        var defaultRefreshPeriodMinutes = TenantRefreshPeriodParser.TryParseMinutes(_tenantOptions.KnowledgeSources[0].RefreshPeriod, out var parsedRefreshPeriod)
            ? parsedRefreshPeriod
            : 60;

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO schema_metadata (key, value, updated_at_utc)
            VALUES ('schema_version', $schemaVersion, $now)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at_utc = excluded.updated_at_utc;

            INSERT INTO schema_metadata (key, value, updated_at_utc)
            VALUES ('app_version', $appVersion, $now)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at_utc = excluded.updated_at_utc;

            INSERT INTO tenant_metadata (id, tenant_guid, name, created_at_utc, updated_at_utc)
            VALUES (1, $tenantGuid, $tenantName, $now, $now)
            ON CONFLICT(id) DO UPDATE SET tenant_guid = excluded.tenant_guid, name = excluded.name, updated_at_utc = excluded.updated_at_utc;

            INSERT INTO knowledge_bank_metadata (id, tenant_guid, tenant_knowledge_guid, name, version, created_at_utc, updated_at_utc)
            VALUES (1, (SELECT tenant_guid FROM tenant_metadata WHERE id = 1), $knowledgeGuid, $knowledgeBankName, $appVersion, $now, $now)
            ON CONFLICT(id) DO UPDATE SET tenant_guid = excluded.tenant_guid, tenant_knowledge_guid = excluded.tenant_knowledge_guid, name = excluded.name, version = excluded.version, updated_at_utc = excluded.updated_at_utc;

            INSERT INTO knowledge_upload_settings (id, max_file_size_mb, max_batch_file_count, max_batch_size_mb, updated_at_utc)
            SELECT 1, 25, 100, 250, $now
            WHERE NOT EXISTS (SELECT 1 FROM knowledge_upload_settings WHERE id = 1);

            INSERT INTO knowledge_refresh_settings (id, refresh_period_minutes, updated_at_utc)
            SELECT 1, $defaultRefreshPeriodMinutes, $now
            WHERE NOT EXISTS (SELECT 1 FROM knowledge_refresh_settings WHERE id = 1);

            INSERT INTO ai_settings (id, active_provider, azure_model, ollama_local_model, ollama_cloud_model, updated_at_utc)
            SELECT 1, $activeProvider, $azureModel, $ollamaLocalModel, $ollamaCloudModel, $now
            WHERE NOT EXISTS (SELECT 1 FROM ai_settings WHERE id = 1);

            INSERT INTO qna_experience_settings (
                id, displayed_ready_question_count, displayed_suggested_question_count,
                auto_submit_prompt_buttons, show_answer_source_document_names, updated_at_utc)
            SELECT 1, 4, 4, 1, 1, $now
            WHERE NOT EXISTS (SELECT 1 FROM qna_experience_settings WHERE id = 1);
        ";
        command.Parameters.AddWithValue("$schemaVersion", SchemaVersion);
        command.Parameters.AddWithValue("$appVersion", AppVersion);
        command.Parameters.AddWithValue("$tenantGuid", tenantGuid);
        command.Parameters.AddWithValue("$tenantName", tenantDisplayName);
        command.Parameters.AddWithValue("$knowledgeBankName", knowledgeBankName);
        command.Parameters.AddWithValue("$knowledgeGuid", knowledgeGuid);
        command.Parameters.AddWithValue("$defaultRefreshPeriodMinutes", defaultRefreshPeriodMinutes);
        command.Parameters.AddWithValue("$activeProvider", string.IsNullOrWhiteSpace(_aiOptions.DefaultProvider) ? "ollama-cloud" : _aiOptions.DefaultProvider);
        command.Parameters.AddWithValue("$azureModel", _azureAiOptions.DeploymentName);
        command.Parameters.AddWithValue("$ollamaLocalModel", _ollamaLocalOptions.Model);
        command.Parameters.AddWithValue("$ollamaCloudModel", _ollamaCloudOptions.Model);
        command.Parameters.AddWithValue("$now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await SeedTenantKnowledgeSourcesAsync(connection, tenantDisplayName, now, cancellationToken);
    }

    private async Task SeedTenantKnowledgeSourcesAsync(SqliteConnection connection, string tenantDisplayName, string now, CancellationToken cancellationToken)
    {
        var seedKeys = new List<string>();
        foreach (var source in _tenantOptions.KnowledgeSources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var seedKey = string.IsNullOrWhiteSpace(source.Key) ? $"source_{seedKeys.Count + 1}" : source.Key.Trim();
            seedKeys.Add(seedKey);

            var sourceUri = new Uri(source.Url.Trim());
            var address = BuildCanonicalSourceAddress(sourceUri);
            var name = string.IsNullOrWhiteSpace(source.Name) ? tenantDisplayName : source.Name.Trim();
            var description = string.IsNullOrWhiteSpace(source.Description)
                ? $"{tenantDisplayName} seed web sitesi bilgi kaynağı."
                : source.Description.Trim();
            var sourceGuid = BuildDeterministicGuid($"{_tenantOptions.Id}:{seedKey}:{address}");
            TenantRefreshPeriodParser.TryParseMinutes(source.RefreshPeriod, out var refreshPeriodMinutes);
            refreshPeriodMinutes = refreshPeriodMinutes <= 0 ? 60 : refreshPeriodMinutes;
            var nextRefreshAtUtc = DateTime.UtcNow.ToString("O");

            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE knowledge_sources
                SET
                    tenant_guid = (SELECT tenant_guid FROM tenant_metadata WHERE id = 1),
                    tenant_knowledge_guid = (SELECT tenant_knowledge_guid FROM knowledge_bank_metadata WHERE id = 1),
                    knowledge_source_guid = $sourceGuid,
                    source_type = $sourceType,
                    name = $sourceName,
                    description = $sourceDescription,
                    address = $address,
                    protocol = $protocol,
                    is_enabled = $isEnabled,
                    is_archived = 0,
                    status_code = 'ok',
                    status_label = 'Tamam',
                    status_message = 'Kaynak kullanılabilir.',
                    is_seed_managed = 1,
                    seed_key = $seedKey,
                    refresh_period_minutes = $refreshPeriodMinutes,
                    auto_refresh_enabled = $autoRefreshEnabled,
                    next_refresh_at_utc = COALESCE(next_refresh_at_utc, $nextRefreshAtUtc),
                    updated_at_utc = $now
                WHERE seed_key = $seedKey OR address = $address;

                INSERT INTO knowledge_sources (
                    tenant_guid, tenant_knowledge_guid, knowledge_source_guid, source_type, name, description, address, protocol,
                    is_enabled, is_archived, status_code, status_label, status_message,
                    is_seed_managed, seed_key, refresh_period_minutes, auto_refresh_enabled, next_refresh_at_utc,
                    created_at_utc, updated_at_utc)
                SELECT
                    (SELECT tenant_guid FROM tenant_metadata WHERE id = 1),
                    (SELECT tenant_knowledge_guid FROM knowledge_bank_metadata WHERE id = 1),
                    $sourceGuid,
                    $sourceType,
                    $sourceName,
                    $sourceDescription,
                    $address,
                    $protocol,
                    $isEnabled,
                    0,
                    'ok',
                    'Tamam',
                    'Kaynak kullanılabilir.',
                    1,
                    $seedKey,
                    $refreshPeriodMinutes,
                    $autoRefreshEnabled,
                    $nextRefreshAtUtc,
                    $now,
                    $now
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM knowledge_sources
                    WHERE seed_key = $seedKey OR address = $address
                );";
            command.Parameters.AddWithValue("$sourceGuid", sourceGuid);
            command.Parameters.AddWithValue("$sourceType", KnowledgeSourceTypes.WebSite);
            command.Parameters.AddWithValue("$sourceName", name);
            command.Parameters.AddWithValue("$sourceDescription", description);
            command.Parameters.AddWithValue("$address", address);
            command.Parameters.AddWithValue("$protocol", sourceUri.Scheme.ToLowerInvariant());
            command.Parameters.AddWithValue("$isEnabled", source.Enabled ? 1 : 0);
            command.Parameters.AddWithValue("$seedKey", seedKey);
            command.Parameters.AddWithValue("$refreshPeriodMinutes", refreshPeriodMinutes);
            command.Parameters.AddWithValue("$autoRefreshEnabled", source.Enabled ? 1 : 0);
            command.Parameters.AddWithValue("$nextRefreshAtUtc", nextRefreshAtUtc);
            command.Parameters.AddWithValue("$now", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var safeSeedKeys = string.Join(", ", seedKeys.Select(key => $"'{key.Replace("'", "''", StringComparison.Ordinal)}'"));
        await connection.ExecuteNonQueryAsync(
            $"UPDATE knowledge_sources SET is_enabled = 0, is_archived = 1, auto_refresh_enabled = 0, updated_at_utc = '{now.Replace("'", "''", StringComparison.Ordinal)}' WHERE is_seed_managed = 1 AND seed_key NOT IN ({safeSeedKeys});",
            cancellationToken);
    }

    private static string BuildCanonicalSourceAddress(Uri uri)
    {
        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        return $"{uri.Scheme.ToLowerInvariant()}://{host.ToLowerInvariant()}{port}";
    }

    private static string BuildDeterministicGuid(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(bytes.Take(16).ToArray()).ToString("D");
    }

    // Drops only the AI settings table when it still has the retired single-Ollama provider schema.
    private static async Task EnsurePrimaryAiSettingsSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'ai_settings';";
        var exists = await existsCommand.ExecuteScalarAsync(cancellationToken);
        if (exists is null)
        {
            return;
        }

        using var columnsCommand = connection.CreateCommand();
        columnsCommand.CommandText = "PRAGMA table_info(ai_settings);";
        var requiredColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "active_provider",
            "azure_model",
            "ollama_local_model",
            "ollama_cloud_model",
            "updated_at_utc"
        };
        using var reader = await columnsCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            requiredColumns.Remove(reader.GetString(1));
        }

        if (requiredColumns.Count == 0)
        {
            return;
        }

        using var dropCommand = connection.CreateCommand();
        dropCommand.CommandText = "DROP TABLE IF EXISTS ai_settings;";
        await dropCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}

// Provides a small async ExecuteNonQuery helper for initialization statements.
internal static class SqliteCommandExtensions
{
    // Executes a raw SQL command text against the open connection.
    public static async Task ExecuteNonQueryAsync(this SqliteConnection connection, string commandText, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

