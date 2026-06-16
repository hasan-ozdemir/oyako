// Codex developer note: Explains the purpose and flow of webapi-oyako/webapi-oyako.Tests/KnowledgeStoreMaintenanceRepositoryTests.cs for maintainers.
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using webapi_oyako.Application.Services;
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Enums;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Domain.Services;
using webapi_oyako.Infrastructure.Configuration;
using webapi_oyako.Infrastructure.Data;
using Xunit;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Tests;

// Implements the KnowledgeStoreMaintenanceRepositoryTests component and its responsibilities in the Oyako codebase.
public sealed class KnowledgeStoreMaintenanceRepositoryTests
{
    [Fact]
    // Verifies that an empty SQLite database receives the complete primary schema and required seed data.
    public async Task InitializeAsync_WhenDatabaseIsEmpty_CreatesPrimarySchemaAndRequiredSeedData()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            // Verifies the expected behavior for this test scenario.
            Assert.True(await TableExistsAsync(database.ConnectionString, "knowledge_sources"));
            // Verifies the expected behavior for this test scenario.
            Assert.True(await TableExistsAsync(database.ConnectionString, "web_pages"));
            // Verifies the expected behavior for this test scenario.
            Assert.True(await TableExistsAsync(database.ConnectionString, "crawl_runs"));
            // Verifies the expected behavior for this test scenario.
            Assert.True(await TableExistsAsync(database.ConnectionString, "system_instruction_cache"));
            // Verifies the expected behavior for this test scenario.
            Assert.True(await TableExistsAsync(database.ConnectionString, "ready_questions"));
            // Verifies the expected behavior for this test scenario.
            Assert.True(await TableExistsAsync(database.ConnectionString, "ai_settings"));
            // Verifies the expected behavior for this test scenario.
            Assert.True(await TableExistsAsync(database.ConnectionString, "qna_experience_settings"));

            // Verifies the expected behavior for this test scenario.
            Assert.True(await ColumnExistsAsync(database.ConnectionString, "knowledge_sources", "address"));
            // Verifies the expected behavior for this test scenario.
            Assert.True(await ColumnExistsAsync(database.ConnectionString, "web_pages", "web_source_url"));
            // Verifies the expected behavior for this test scenario.
            Assert.True(await ColumnExistsAsync(database.ConnectionString, "web_pages", "content_preview"));
            // Verifies the expected behavior for this test scenario.
            Assert.True(await ColumnExistsAsync(database.ConnectionString, "system_instruction_cache", "source_fingerprint"));
            // Verifies the expected behavior for this test scenario.
            Assert.True(await ColumnExistsAsync(database.ConnectionString, "ready_questions", "source_fingerprint"));
            // Verifies the expected behavior for this test scenario.
            Assert.True(await ColumnExistsAsync(database.ConnectionString, "ai_settings", "active_provider"));
            // Verifies the expected behavior for this test scenario.
            Assert.True(await ColumnExistsAsync(database.ConnectionString, "qna_experience_settings", "displayed_ready_question_count"));
            // Verifies the expected behavior for this test scenario.
            Assert.True(await ColumnExistsAsync(database.ConnectionString, "qna_experience_settings", "displayed_suggested_question_count"));
            // Verifies the expected behavior for this test scenario.
            Assert.True(await ColumnExistsAsync(database.ConnectionString, "qna_experience_settings", "auto_submit_prompt_buttons"));
            // Verifies the expected behavior for this test scenario.
            Assert.True(await ColumnExistsAsync(database.ConnectionString, "qna_experience_settings", "show_answer_source_document_names"));

            // Verifies the expected behavior for this test scenario.
            Assert.Equal(1, await CountAsync(database.ConnectionString, "knowledge_sources"));
            // Verifies the expected behavior for this test scenario.
            Assert.Equal("https://oyakdijital.com.tr", await FirstTextAsync(database.ConnectionString, "knowledge_sources", "address"));
            // Verifies the expected behavior for this test scenario.
            Assert.Equal(1, await CountAsync(database.ConnectionString, "ai_settings"));
            // Verifies the expected behavior for this test scenario.
            Assert.Equal("azure", await FirstTextAsync(database.ConnectionString, "ai_settings", "active_provider"));
            // Verifies the expected behavior for this test scenario.
            Assert.Equal(1, await CountAsync(database.ConnectionString, "qna_experience_settings"));
            var qnaRepository = new QnaExperienceSettingsRepository(database.Options);
            var qnaSettings = await qnaRepository.GetAsync(CancellationToken.None);
            Assert.NotNull(qnaSettings);
            Assert.Equal(4, qnaSettings.DisplayedReadyQuestionCount);
            Assert.Equal(4, qnaSettings.DisplayedSuggestedQuestionCount);
            Assert.True(qnaSettings.AutoSubmitPromptButtons);
            Assert.True(qnaSettings.ShowAnswerSourceDocumentNames);
            // Verifies the expected behavior for this test scenario.
            Assert.Equal(0, await CountAsync(database.ConnectionString, "web_pages"));
            // Verifies the expected behavior for this test scenario.
            Assert.Equal(0, await CountAsync(database.ConnectionString, "system_instruction_cache"));
            // Verifies the expected behavior for this test scenario.
            Assert.Equal(0, await CountAsync(database.ConnectionString, "ready_questions"));
        }
        finally
        {
            TryDelete(database.Path);
        }
    }

    [Fact]
    // Verifies that Q&A experience settings can be changed and read back from SQLite.
    public async Task QnaExperienceSettingsService_UpdateAsync_PersistsEveryUserFacingSetting()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var service = new QnaExperienceSettingsService(new QnaExperienceSettingsRepository(database.Options));

            var updated = await service.UpdateAsync(7, 3, false, false, CancellationToken.None);
            var reloaded = await new QnaExperienceSettingsRepository(database.Options).GetAsync(CancellationToken.None);

            Assert.Equal(7, updated.DisplayedReadyQuestionCount);
            Assert.Equal(3, updated.DisplayedSuggestedQuestionCount);
            Assert.False(updated.AutoSubmitPromptButtons);
            Assert.False(updated.ShowAnswerSourceDocumentNames);
            Assert.NotNull(reloaded);
            Assert.Equal(7, reloaded.DisplayedReadyQuestionCount);
            Assert.Equal(3, reloaded.DisplayedSuggestedQuestionCount);
            Assert.False(reloaded.AutoSubmitPromptButtons);
            Assert.False(reloaded.ShowAnswerSourceDocumentNames);
        }
        finally
        {
            TryDelete(database.Path);
        }
    }

    [Fact]
    // Verifies that ready questions can be read from SQLite with document references after generation.
    public async Task ReadyQuestionRepository_GetNextAsync_ReturnsGeneratedQuestionsWithDocumentReferences()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            await SeedKnowledgeRowsAsync(database.ConnectionString);
            var repository = new ReadyQuestionRepository(database.Options);

            var questions = await repository.GetNextAsync(4, CancellationToken.None);

            Assert.Single(questions);
            Assert.Equal("Oyak Dijital hangi hizmetleri sunar?", questions[0].Text);
            Assert.Single(questions[0].DocumentReferences);
            Assert.True(questions[0].DocumentReferences[0].SourceId > 0);
            Assert.True(questions[0].DocumentReferences[0].DocumentId > 0);
            Assert.Equal("hash-1", questions[0].DocumentReferences[0].DocumentContentHash);
        }
        finally
        {
            TryDelete(database.Path);
        }
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task RestoreAsync_WhenRefreshFails_RestoresAllKnowledgeTablesFromBackup()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await SeedKnowledgeRowsAsync(database.ConnectionString);
            // Creates the object needed for the next step of the workflow.
            var repository = new KnowledgeStoreMaintenanceRepository(database.Options);

            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await repository.BackupAsync("restore_test", CancellationToken.None);
            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await repository.ClearKnowledgeTablesAsync(CancellationToken.None);

            // Verifies the expected behavior for this test scenario.
            Assert.Equal(0, await CountAsync(database.ConnectionString, "web_pages"));
            // Verifies the expected behavior for this test scenario.
            Assert.Equal(0, await CountAsync(database.ConnectionString, "system_instruction_cache"));
            // Verifies the expected behavior for this test scenario.
            Assert.Equal(0, await CountAsync(database.ConnectionString, "ready_questions"));

            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await repository.RestoreAsync("restore_test", CancellationToken.None);

            // Verifies the expected behavior for this test scenario.
            Assert.Equal(1, await CountAsync(database.ConnectionString, "web_pages"));
            // Verifies the expected behavior for this test scenario.
            Assert.Equal(1, await CountAsync(database.ConnectionString, "system_instruction_cache"));
            // Verifies the expected behavior for this test scenario.
            Assert.Equal(1, await CountAsync(database.ConnectionString, "ready_questions"));
        }
        finally
        {
            TryDelete(database.Path);
        }
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task CleanupBackupsExceptAsync_KeepsOnlyRequestedBackupSet()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await SeedKnowledgeRowsAsync(database.ConnectionString);
            // Creates the object needed for the next step of the workflow.
            var repository = new KnowledgeStoreMaintenanceRepository(database.Options);

            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await repository.BackupAsync("old_backup", CancellationToken.None);
            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await repository.BackupAsync("last_backup", CancellationToken.None);
            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await repository.CleanupBackupsExceptAsync("last_backup", CancellationToken.None);

            // Verifies the expected behavior for this test scenario.
            Assert.False(await TableExistsAsync(database.ConnectionString, "kr_backup_old_backup_web_pages"));
            // Verifies the expected behavior for this test scenario.
            Assert.False(await TableExistsAsync(database.ConnectionString, "kr_backup_old_backup_system_instruction_cache"));
            // Verifies the expected behavior for this test scenario.
            Assert.False(await TableExistsAsync(database.ConnectionString, "kr_backup_old_backup_ready_questions"));
            // Verifies the expected behavior for this test scenario.
            Assert.True(await TableExistsAsync(database.ConnectionString, "kr_backup_last_backup_web_pages"));
            // Verifies the expected behavior for this test scenario.
            Assert.True(await TableExistsAsync(database.ConnectionString, "kr_backup_last_backup_system_instruction_cache"));
            // Verifies the expected behavior for this test scenario.
            Assert.True(await TableExistsAsync(database.ConnectionString, "kr_backup_last_backup_ready_questions"));
        }
        finally
        {
            TryDelete(database.Path);
        }
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task KnowledgeRedownloadService_WhenReadyQuestionGenerationFails_KeepsRedownloadedKnowledgeWithWarning()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await SeedKnowledgeRowsAsync(database.ConnectionString);
            // Creates the object needed for the next step of the workflow.
            var service = new KnowledgeRedownloadService(
                // Creates the object needed for the next step of the workflow.
                new SuccessfulCrawler(),
                // Creates the object needed for the next step of the workflow.
                new WebPageRepository(database.Options),
                // Creates the object needed for the next step of the workflow.
                new InMemoryCrawlRunRepository(),
                // Creates the object needed for the next step of the workflow.
                new KnowledgeStoreMaintenanceRepository(database.Options),
                // Creates the object needed for the next step of the workflow.
                new NoOpSystemInstructionCache(),
                // Creates the object needed for the next step of the workflow.
                new FailingReadyQuestionService(),
                // Creates the object needed for the next step of the workflow.
                new ReadyQuestionRepository(database.Options),
                // Creates the object needed for the next step of the workflow.
                new NoOpRuntimeStatusService(),
                // Creates the object needed for the next step of the workflow.
                new OpenKnowledgeOperationGate(),
                // Creates the object needed for the next step of the workflow.
                new NoOpLocalKnowledgeRebuildService());

            var result = await service.RedownloadAsync(CancellationToken.None);

            // Verifies the expected behavior for this test scenario.
            Assert.Equal("succeeded", result.Status);
            // Verifies the expected behavior for this test scenario.
            Assert.False(result.RestoredFromBackup);
            // Verifies the expected behavior for this test scenario.
            Assert.True(result.WarningCount > 0);
            // Verifies the expected behavior for this test scenario.
            Assert.Equal(1, await CountAsync(database.ConnectionString, "web_pages"));
            // Verifies the expected behavior for this test scenario.
            Assert.Equal("https://oyakdijital.com.tr/yeni", await FirstTextAsync(database.ConnectionString, "web_pages", "web_source_url"));
        }
        finally
        {
            TryDelete(database.Path);
        }
    }

    [Fact]
    // Verifies that knowledge refresh stores one source and one document when the crawler returns duplicate URLs.
    public async Task KnowledgeRedownloadService_WhenCrawlerReturnsDuplicateUrls_PersistsSingleSourceAndSingleDocument()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            // Creates the object needed for the next step of the workflow.
            var service = new KnowledgeRedownloadService(
                // Creates the object needed for the next step of the workflow.
                new DuplicateUrlCrawler(),
                // Creates the object needed for the next step of the workflow.
                new WebPageRepository(database.Options),
                // Creates the object needed for the next step of the workflow.
                new InMemoryCrawlRunRepository(),
                // Creates the object needed for the next step of the workflow.
                new KnowledgeStoreMaintenanceRepository(database.Options),
                // Creates the object needed for the next step of the workflow.
                new NoOpSystemInstructionCache(),
                // Creates the object needed for the next step of the workflow.
                new SuccessfulReadyQuestionService(),
                // Creates the object needed for the next step of the workflow.
                new ReadyQuestionRepository(database.Options),
                // Creates the object needed for the next step of the workflow.
                new NoOpRuntimeStatusService(),
                // Creates the object needed for the next step of the workflow.
                new OpenKnowledgeOperationGate(),
                // Creates the object needed for the next step of the workflow.
                new NoOpLocalKnowledgeRebuildService());

            var result = await service.RedownloadAsync(CancellationToken.None);

            // Verifies the expected behavior for this test scenario.
            Assert.Equal("succeeded", result.Status);
            // Verifies the expected behavior for this test scenario.
            Assert.Equal(1, await CountAsync(database.ConnectionString, "web_pages"));
            // Verifies the expected behavior for this test scenario.
            Assert.Equal(1, await CountAsync(database.ConnectionString, "knowledge_sources"));
            // Verifies the expected behavior for this test scenario.
            Assert.Equal(0, await DuplicateCountAsync(database.ConnectionString, "web_pages", "web_source_url"));
            // Verifies the expected behavior for this test scenario.
            Assert.Equal(0, await DuplicateCountAsync(database.ConnectionString, "knowledge_sources", "address"));
            // Verifies the expected behavior for this test scenario.
            Assert.Equal("İkinci duplicate belge icerigi.", await FirstTextAsync(database.ConnectionString, "web_pages", "web_content"));
            // Verifies the expected behavior for this test scenario.
            Assert.Equal("https://oyakdijital.com.tr/duplicate", await FirstTextAsync(database.ConnectionString, "web_pages", "web_source_url"));
        }
        finally
        {
            TryDelete(database.Path);
        }
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task WebPageRepository_GetAllPagesAsync_ReturnsOnlyUsableKnowledgeDocuments()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var repository = new WebPageRepository(database.Options);
            var source = await repository.AddSourceAsync("web_site", "Example", null, "https://example.com", CancellationToken.None);
            var now = DateTime.UtcNow;
            await repository.UpsertPagesAsync(
                new[]
                {
                    new WebPage
                    {
                        SourceId = source.Id,
                        WebSourceUrl = "https://example.com/ok",
                        WebTitle = "Ok",
                        WebContent = "Kullanilabilir bilgi belgesi icerigi.",
                        ContentPreview = "Kullanilabilir bilgi belgesi icerigi.",
                        ContentHash = "ok-hash",
                        StatusCode = "ok",
                        StatusLabel = "Tamam",
                        StatusMessage = "Belge kullanılabilir.",
                        FirstSeenAtUtc = now,
                        LastSeenAtUtc = now,
                        LastCrawledAtUtc = now
                    },
                    new WebPage
                    {
                        SourceId = source.Id,
                        WebSourceUrl = "https://example.com/missing",
                        WebTitle = "Missing",
                        WebContent = string.Empty,
                        ContentPreview = "HTTP 404",
                        ContentHash = "missing-hash",
                        StatusCode = "http404",
                        StatusLabel = "http404",
                        StatusMessage = "HTTP 404",
                        HttpStatusCode = 404,
                        FirstSeenAtUtc = now,
                        LastSeenAtUtc = now,
                        LastCrawledAtUtc = now
                    }
                },
                CancellationToken.None);

            var usablePages = await repository.GetAllPagesAsync(CancellationToken.None);
            var bankPages = await repository.GetKnowledgeSourcesAsync(CancellationToken.None);

            Assert.Single(usablePages);
            Assert.Equal("https://example.com/ok", usablePages[0].WebSourceUrl);
            Assert.Equal(2, bankPages.Count);
        }
        finally
        {
            TryDelete(database.Path);
        }
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task WebPageRepository_UpdateDocumentAsync_UpdatesContentPreviewAndUsableKnowledgeInput()
    {
        var database = await CreateDatabaseAsync();
        try
        {
            var repository = new WebPageRepository(database.Options);
            var source = await repository.AddSourceAsync("web_site", "Example", null, "https://example.com", CancellationToken.None);
            var now = DateTime.UtcNow;
            await repository.UpsertPagesAsync(
                new[]
                {
                    new WebPage
                    {
                        SourceId = source.Id,
                        WebSourceUrl = "https://example.com/editable",
                        WebTitle = "Editable",
                        WebContent = "Eski belge icerigi.",
                        ContentPreview = "Eski belge icerigi.",
                        ContentHash = "old-hash",
                        StatusCode = "ok",
                        StatusLabel = "Tamam",
                        StatusMessage = "Belge kullanılabilir.",
                        FirstSeenAtUtc = now,
                        LastSeenAtUtc = now,
                        LastCrawledAtUtc = now
                    }
                },
                CancellationToken.None);

            var document = (await repository.GetKnowledgeSourcesAsync(CancellationToken.None)).Single();
            var changed = await repository.UpdateDocumentAsync(
                document.Id,
                "Editable",
                "Yeni manuel belge icerigi OYAK Dijital hizmetleri icin kullanilir.",
                true,
                CancellationToken.None);

            var usablePage = (await repository.GetAllPagesAsync(CancellationToken.None)).Single();

            Assert.True(changed);
            Assert.Equal("Yeni manuel belge icerigi OYAK Dijital hizmetleri icin kullanilir.", usablePage.WebContent);
            Assert.Contains("Yeni manuel belge", usablePage.ContentPreview, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("manual", usablePage.PreviewStatus);
            Assert.Equal("ok", usablePage.StatusCode);
            Assert.NotEqual("old-hash", usablePage.ContentHash);
        }
        finally
        {
            TryDelete(database.Path);
        }
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static async Task<TestDatabase> CreateDatabaseAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"oyako-maintenance-tests-{Guid.NewGuid():N}.db");
        // Creates the object needed for the next step of the workflow.
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        // Creates the object needed for the next step of the workflow.
        var sqliteOptions = Options.Create(new SqliteOptions { ConnectionString = connectionString });
        // Creates the object needed for the next step of the workflow.
        var initializer = new SqliteDbInitializer(
            sqliteOptions,
            // Creates the object needed for the next step of the workflow.
            Options.Create(new AiOptions { DefaultProvider = "azure" }),
            // Creates the object needed for the next step of the workflow.
            Options.Create(new AzureAiOptions { DeploymentName = "DeepSeek-V4-Flash" }),
            // Creates the object needed for the next step of the workflow.
            Options.Create(new OllamaLocalOptions { Model = "gemma4:12b" }),
            Options.Create(new OllamaCloudOptions { Model = "minimax-m3:cloud" }));

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await initializer.InitializeAsync(CancellationToken.None);
        // Returns the computed result to the caller and completes this branch of the workflow.
        return new TestDatabase(path, connectionString, sqliteOptions);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static async Task SeedKnowledgeRowsAsync(string connectionString)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(connectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync();
        var timestamp = DateTime.UtcNow.ToString("O");

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await ExecuteAsync(
            connection,
            """
            INSERT INTO web_pages (
                source_id,
                web_source_url,
                web_title,
                web_content,
                content_hash,
                first_seen_at_utc,
                last_seen_at_utc,
                last_crawled_at_utc)
            VALUES (
                (SELECT id FROM knowledge_sources LIMIT 1),
                'https://www.oyakdijital.com.tr',
                'Oyak Dijital',
                'Oyak Dijital kaynak icerigi.',
                'hash-1',
                $timestamp,
                $timestamp,
                $timestamp);
            """,
            timestamp);

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await ExecuteAsync(
            connection,
            """
            INSERT INTO system_instruction_cache (
                cache_key,
                content,
                content_hash,
                source_fingerprint,
                page_count,
                built_at_utc)
            VALUES (
                'oyako-default-system-instruction-v3-markdown',
                'system instruction',
                'cache-hash',
                'fingerprint',
                1,
                $timestamp);
            """,
            timestamp);

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await ExecuteAsync(
            connection,
            """
            INSERT INTO ready_questions (
                text,
                source_fingerprint,
                served_count,
                last_served_at_utc,
                created_at_utc)
            VALUES (
                'Oyak Dijital hangi hizmetleri sunar?',
                'fingerprint',
                0,
                NULL,
                $timestamp);
            """,
            timestamp);

        await ExecuteAsync(
            connection,
            """
            INSERT INTO ready_question_documents (
                ready_question_id,
                source_id,
                document_id,
                document_content_hash,
                created_at_utc)
            VALUES (
                (SELECT id FROM ready_questions ORDER BY id DESC LIMIT 1),
                (SELECT source_id FROM web_pages WHERE web_source_url = 'https://www.oyakdijital.com.tr'),
                (SELECT id FROM web_pages WHERE web_source_url = 'https://www.oyakdijital.com.tr'),
                'hash-1',
                $timestamp);
            """,
            timestamp);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static async Task ExecuteAsync(SqliteConnection connection, string commandText, string timestamp)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        // Registers or maps application behavior into the runtime pipeline.
        command.Parameters.AddWithValue("$timestamp", timestamp);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await command.ExecuteNonQueryAsync();
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static async Task<long> CountAsync(string connectionString, string tableName)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(connectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync();
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
        // Returns the computed result to the caller and completes this branch of the workflow.
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static async Task<long> DuplicateCountAsync(string connectionString, string tableName, string columnName)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(connectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync();
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT COUNT(*)
            FROM (
                SELECT {columnName}
                FROM {tableName}
                GROUP BY {columnName}
                HAVING COUNT(*) > 1
            );
            """;
        // Returns the computed result to the caller and completes this branch of the workflow.
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static async Task<bool> TableExistsAsync(string connectionString, string tableName)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(connectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync();
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        // Registers or maps application behavior into the runtime pipeline.
        command.Parameters.AddWithValue("$tableName", tableName);
        // Returns the computed result to the caller and completes this branch of the workflow.
        return Convert.ToInt64(await command.ExecuteScalarAsync()) == 1;
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static async Task<bool> ColumnExistsAsync(string connectionString, string tableName, string columnName)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(connectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync();
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var reader = await command.ExecuteReaderAsync();
        // Iterates through the collection to process each item consistently.
        while (await reader.ReadAsync())
        {
            // Guards the following branch so the workflow handles this condition deliberately.
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                // Returns the computed result to the caller and completes this branch of the workflow.
                return true;
            }
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return false;
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static async Task<string?> FirstTextAsync(string connectionString, string tableName, string columnName)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var connection = new SqliteConnection(connectionString);
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await connection.OpenAsync();
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {columnName} FROM {tableName} LIMIT 1;";
        // Returns the computed result to the caller and completes this branch of the workflow.
        return (string?)await command.ExecuteScalarAsync();
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }

    // Executes this component behavior as part of the Oyako application flow.
    private sealed record TestDatabase(string Path, string ConnectionString, IOptions<SqliteOptions> Options);

    private sealed class DuplicateUrlCrawler : IWebCrawler
    {
        // Executes this component behavior as part of the Oyako application flow.
        public Task<CrawlerResult> CrawlAsync(CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var pages = new[]
            {
                // Creates the object needed for the next step of the workflow.
                new WebPage
                {
                    WebSourceUrl = "https://www.oyakdijital.com.tr/duplicate/",
                    WebTitle = "Duplicate",
                    WebContent = "İlk duplicate belge icerigi.",
                    ContentPreview = "İlk duplicate belge icerigi.",
                    ContentHash = "duplicate-hash-1",
                    FirstSeenAtUtc = now,
                    LastSeenAtUtc = now,
                    LastCrawledAtUtc = now
                },
                // Creates the object needed for the next step of the workflow.
                new WebPage
                {
                    WebSourceUrl = "https://oyakdijital.com.tr/duplicate",
                    WebTitle = "Duplicate",
                    WebContent = "İkinci duplicate belge icerigi.",
                    ContentPreview = "İkinci duplicate belge icerigi.",
                    ContentHash = "duplicate-hash-2",
                    FirstSeenAtUtc = now,
                    LastSeenAtUtc = now,
                    LastCrawledAtUtc = now
                }
            };

            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(new CrawlerResult(
                true,
                pages,
                Array.Empty<string>(),
                Array.Empty<string>(),
                DateTimeOffset.UtcNow));
        }

        public Task<CrawlerResult> CrawlSourceAsync(KnowledgeSource source, CancellationToken cancellationToken)
        {
            return CrawlAsync(cancellationToken);
        }

        public async Task<WebPage> CrawlDocumentAsync(WebPage document, CancellationToken cancellationToken)
        {
            return (await CrawlAsync(cancellationToken)).Pages.First();
        }
    }

    private sealed class SuccessfulCrawler : IWebCrawler
    {
        // Executes this component behavior as part of the Oyako application flow.
        public Task<CrawlerResult> CrawlAsync(CancellationToken cancellationToken)
        {
            // Creates the object needed for the next step of the workflow.
            var page = new WebPage
            {
                WebSourceUrl = "https://www.oyakdijital.com.tr/yeni",
                WebTitle = "Yeni Sayfa",
                WebContent = "Yeni ama restore edilmesi gereken test icerigi.",
                ContentHash = "new-hash"
            };

            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(new CrawlerResult(
                true,
                [page],
                Array.Empty<string>(),
                Array.Empty<string>(),
                DateTimeOffset.UtcNow));
        }

        public Task<CrawlerResult> CrawlSourceAsync(KnowledgeSource source, CancellationToken cancellationToken)
        {
            return CrawlAsync(cancellationToken);
        }

        public async Task<WebPage> CrawlDocumentAsync(WebPage document, CancellationToken cancellationToken)
        {
            return (await CrawlAsync(cancellationToken)).Pages.First();
        }
    }

    private sealed class InMemoryCrawlRunRepository : ICrawlRunRepository
    {
        // Executes this component behavior as part of the Oyako application flow.
        public Task<int> StartAsync(DateTime startedAtUtc, CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(1);
        }

        public Task SetCompletedAsync(
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
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.CompletedTask;
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<CrawlRun?> GetLatestAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult<CrawlRun?>(null);
        }
    }

    private sealed class NoOpSystemInstructionCache : ISystemInstructionCache
    {
        // Executes this component behavior as part of the Oyako application flow.
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        // Executes this component behavior as part of the Oyako application flow.
        public Task<string> GetCurrentAsync(CancellationToken cancellationToken) => Task.FromResult("system");

        // Executes this component behavior as part of the Oyako application flow.
        public Task<SystemInstructionCacheSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult<SystemInstructionCacheSnapshot?>(new SystemInstructionCacheSnapshot("hash", "fingerprint", 1, DateTime.UtcNow));
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task InvalidateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        // Executes this component behavior as part of the Oyako application flow.
        public Task ForceRefreshAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        // Executes this component behavior as part of the Oyako application flow.
        public Task<bool> RecomposeFromActiveBlocksAsync(CancellationToken cancellationToken) => Task.FromResult(false);

        // Executes this component behavior as part of the Oyako application flow.
        public Task ReloadFromStoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        // Executes this component behavior as part of the Oyako application flow.
        public Task<bool> RefreshIfChangedAsync(CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class FailingReadyQuestionService : IReadyQuestionService
    {
        // Executes this component behavior as part of the Oyako application flow.
        public Task<ReadyQuestionSet> GetNextAsync(int count, CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(new ReadyQuestionSet(Array.Empty<string>(), "generated", null, 0, null, false));
        }

        // Executes this component behavior as part of the Oyako application flow.
        public void QueueRefreshFromKnowledge()
        {
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<bool> RefreshFromKnowledgeAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(false);
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<ReadyQuestionRefreshResult> ForceRefreshFromKnowledgeAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(new ReadyQuestionRefreshResult(false, 0, null));
        }
    }

    private sealed class SuccessfulReadyQuestionService : IReadyQuestionService
    {
        // Executes this component behavior as part of the Oyako application flow.
        public Task<ReadyQuestionSet> GetNextAsync(int count, CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(new ReadyQuestionSet(Array.Empty<string>(), "generated", null, 0, null, false));
        }

        // Executes this component behavior as part of the Oyako application flow.
        public void QueueRefreshFromKnowledge()
        {
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<bool> RefreshFromKnowledgeAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(true);
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<ReadyQuestionRefreshResult> ForceRefreshFromKnowledgeAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(new ReadyQuestionRefreshResult(true, 100, "test-fingerprint"));
        }
    }

    private sealed class NoOpRuntimeStatusService : IRuntimeStatusService
    {
        // Exposes data consumed by other layers while preserving the domain or DTO shape.
        public RuntimeStatusSnapshot Current { get; } = new("app", "ready_for_question", "ready", 1, 1, true, "Uygulama Hazır", "ready", "message", null, DateTime.UtcNow);

        public Task PublishAsync(
            string operation,
            string phase,
            string stepKey,
            int stepIndex,
            int stepCount,
            bool isTerminal,
            string message,
            string severity,
            string icon,
            int? pageCount = null,
            CancellationToken cancellationToken = default)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<RuntimeStatusSnapshot> WatchAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class NoOpLocalKnowledgeRebuildService : ILocalKnowledgeRebuildService
    {
        public Task<int> RebuildMissingAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public Task<int> RedownloadSourceAsync(int sourceId, CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public Task<bool> RedownloadDocumentAsync(int documentId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class OpenKnowledgeOperationGate : IKnowledgeOperationGate
    {
        // Stores state or a dependency required by the surrounding component.
        private bool _isHeld;

        // Executes this component behavior as part of the Oyako application flow.
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            // Guards the following branch so the workflow handles this condition deliberately.
            if (_isHeld)
            {
                // Returns the computed result to the caller and completes this branch of the workflow.
                return Task.FromResult(false);
            }

            _isHeld = true;
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(true);
        }

        // Executes this component behavior as part of the Oyako application flow.
        public void Release()
        {
            _isHeld = false;
        }
    }
}
