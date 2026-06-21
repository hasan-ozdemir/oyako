// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Data/WebPageRepository.cs for maintainers.
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Infrastructure.Configuration;

namespace webapi_oyako.Infrastructure.Data;

// Stores and retrieves all knowledge-bank source, document, folder, raw-file, and setting data.
public sealed class WebPageRepository : IWebPageRepository
{
    private readonly SqliteOptions _options;

    public WebPageRepository(IOptions<SqliteOptions> options)
    {
        _options = options.Value;
    }

    public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task<IReadOnlyList<KnowledgeSource>> GetSourcesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<KnowledgeSource>(SourceSelectSql("GROUP BY s.id ORDER BY s.name ASC, s.address ASC"));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<KnowledgeSource>> GetActiveSourcesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<KnowledgeSource>(SourceSelectSql("WHERE s.is_enabled = 1 AND s.is_archived = 0 AND s.source_type = 'web_site' GROUP BY s.id ORDER BY s.name ASC, s.address ASC"));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<KnowledgeSource>> GetDueSeedSourcesAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<KnowledgeSource>(SourceSelectSql(@"
            WHERE s.is_enabled = 1
              AND s.is_archived = 0
              AND s.source_type = 'web_site'
              AND s.is_seed_managed = 1
              AND s.auto_refresh_enabled = 1
            GROUP BY s.id
            ORDER BY s.next_refresh_at_utc ASC, s.name ASC, s.address ASC"));
        return rows
            .Where(source => source.NextRefreshAtUtc is null || source.NextRefreshAtUtc <= utcNow)
            .ToList();
    }

    public async Task<KnowledgeSource?> GetSourceByIdAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<KnowledgeSource>(SourceSelectSql("WHERE s.id = @Id GROUP BY s.id"), new { Id = id });
    }

    public async Task<KnowledgeSource> AddSourceAsync(string sourceType, string name, string? description, string? address, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var metadata = await GetKnowledgeIdentityAsync(cancellationToken);
        var normalizedType = NormalizeSourceType(sourceType);
        var sourceGuid = Guid.NewGuid().ToString("D");
        var sourceName = string.IsNullOrWhiteSpace(name) ? BuildDefaultSourceName(normalizedType, address) : name.Trim();
        var sourceDescription = description?.Trim() ?? string.Empty;
        var normalizedAddress = normalizedType switch
        {
            KnowledgeSourceTypes.LocalFiles => $"local://source/{sourceGuid}",
            KnowledgeSourceTypes.WebLinks => $"web-links://source/{sourceGuid}",
            _ => NormalizeAddress(address ?? string.Empty)
        };
        var protocol = normalizedType switch
        {
            KnowledgeSourceTypes.LocalFiles => "local",
            KnowledgeSourceTypes.WebLinks => "web-links",
            _ => new Uri(normalizedAddress).Scheme
        };
        var statusCode = normalizedType == KnowledgeSourceTypes.WebSite ? "pending" : "ok";
        var statusLabel = normalizedType == KnowledgeSourceTypes.WebSite ? "Bekliyor" : "Tamam";
        var statusMessage = normalizedType switch
        {
            KnowledgeSourceTypes.LocalFiles => "Yerel dosya kaynağı hazır.",
            KnowledgeSourceTypes.WebLinks => "Web bağlantıları kullanıcı tarafından eklenecek.",
            _ => "Kaynak tarama için sıraya alındı."
        };

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            @"INSERT INTO knowledge_sources (
                tenant_guid, tenant_knowledge_guid, knowledge_source_guid, source_type, name, description, address, protocol,
                is_enabled, is_archived, status_code, status_label, status_message, created_at_utc, updated_at_utc)
              VALUES (
                @TenantGuid, @TenantKnowledgeGuid, @KnowledgeSourceGuid, @SourceType, @Name, @Description, @Address, @Protocol,
                1, 0, @StatusCode, @StatusLabel, @StatusMessage, @Now, @Now)
              ON CONFLICT(address) DO UPDATE SET
                name = excluded.name,
                description = excluded.description,
                source_type = excluded.source_type,
                protocol = excluded.protocol,
                is_enabled = 1,
                is_archived = 0,
                status_code = excluded.status_code,
                status_label = excluded.status_label,
                status_message = excluded.status_message,
                updated_at_utc = excluded.updated_at_utc;",
            new
            {
                metadata.TenantGuid,
                metadata.TenantKnowledgeGuid,
                KnowledgeSourceGuid = sourceGuid,
                SourceType = normalizedType,
                Name = sourceName,
                Description = sourceDescription,
                Address = normalizedAddress,
                Protocol = protocol,
                StatusCode = statusCode,
                StatusLabel = statusLabel,
                StatusMessage = statusMessage,
                Now = now
            });

        return await GetSourceByAddressAsync(connection, normalizedAddress);
    }

    public async Task<KnowledgeSource> EnsureLocalSourceAsync(
        string tenantGuid,
        string tenantKnowledgeGuid,
        string knowledgeSourceGuid,
        string name,
        string? description,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var normalizedAddress = $"local://source/{knowledgeSourceGuid}";
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var existing = await connection.QuerySingleOrDefaultAsync<KnowledgeSource>(
            SourceSelectSql("WHERE s.knowledge_source_guid = @KnowledgeSourceGuid GROUP BY s.id"),
            new { KnowledgeSourceGuid = knowledgeSourceGuid });
        if (existing is not null)
        {
            return existing;
        }

        await connection.ExecuteAsync(
            @"INSERT INTO knowledge_sources (
                tenant_guid, tenant_knowledge_guid, knowledge_source_guid, source_type, name, description, address, protocol,
                is_enabled, is_archived, status_code, status_label, status_message, created_at_utc, updated_at_utc)
              VALUES (
                @TenantGuid, @TenantKnowledgeGuid, @KnowledgeSourceGuid, 'local_files', @Name, @Description, @Address, 'local',
                1, 0, 'ok', 'Tamam', 'Yerel dosya kaynağı manifestten yeniden oluşturuldu.', @Now, @Now)
              ON CONFLICT(address) DO UPDATE SET
                tenant_guid = excluded.tenant_guid,
                tenant_knowledge_guid = excluded.tenant_knowledge_guid,
                knowledge_source_guid = excluded.knowledge_source_guid,
                source_type = 'local_files',
                name = excluded.name,
                description = excluded.description,
                is_enabled = 1,
                is_archived = 0,
                status_code = 'ok',
                status_label = 'Tamam',
                status_message = excluded.status_message,
                updated_at_utc = excluded.updated_at_utc;",
            new
            {
                TenantGuid = tenantGuid,
                TenantKnowledgeGuid = tenantKnowledgeGuid,
                KnowledgeSourceGuid = knowledgeSourceGuid,
                Name = string.IsNullOrWhiteSpace(name) ? "Yerel Dosyalar" : name.Trim(),
                Description = description?.Trim() ?? string.Empty,
                Address = normalizedAddress,
                Now = now
            });

        return await GetSourceByAddressAsync(connection, normalizedAddress);
    }

    public async Task<bool> UpdateSourceAsync(int id, string sourceType, string name, string? description, string? address, bool isEnabled, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var existing = await connection.QuerySingleOrDefaultAsync<KnowledgeSource>(SourceSelectSql("WHERE s.id = @Id GROUP BY s.id"), new { Id = id });
        if (existing is null)
        {
            return false;
        }

        var normalizedType = NormalizeSourceType(sourceType);
        if (!existing.SourceType.Equals(normalizedType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Kaynak türü oluşturulduktan sonra değiştirilemez.");
        }

        var normalizedAddress = normalizedType switch
        {
            KnowledgeSourceTypes.LocalFiles => $"local://source/{existing.KnowledgeSourceGuid}",
            KnowledgeSourceTypes.WebLinks => $"web-links://source/{existing.KnowledgeSourceGuid}",
            _ => NormalizeAddress(address ?? existing.Address)
        };
        if (existing.IsSeedManaged && !string.Equals(existing.Address, normalizedAddress, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Seed bilgi kaynağı adresi tenant env dosyasından yönetilir.");
        }

        var protocol = normalizedType switch
        {
            KnowledgeSourceTypes.LocalFiles => "local",
            KnowledgeSourceTypes.WebLinks => "web-links",
            _ => new Uri(normalizedAddress).Scheme
        };
        var changed = await connection.ExecuteAsync(
            @"UPDATE knowledge_sources
              SET source_type = @SourceType,
                  name = @Name,
                  description = @Description,
                  address = @Address,
                  protocol = @Protocol,
                  is_enabled = @IsEnabled,
                  status_code = @StatusCode,
                  status_label = @StatusLabel,
                  status_message = @StatusMessage,
                  updated_at_utc = @Now
              WHERE id = @Id;",
            new
            {
                Id = id,
                SourceType = normalizedType,
                Name = string.IsNullOrWhiteSpace(name) ? existing.Name : name.Trim(),
                Description = description?.Trim() ?? string.Empty,
                Address = normalizedAddress,
                Protocol = protocol,
                IsEnabled = isEnabled ? 1 : 0,
                StatusCode = normalizedType == KnowledgeSourceTypes.WebSite ? "pending" : "ok",
                StatusLabel = normalizedType == KnowledgeSourceTypes.WebSite ? "Bekliyor" : "Tamam",
                StatusMessage = normalizedType switch
                {
                    KnowledgeSourceTypes.LocalFiles => "Yerel dosya kaynağı güncellendi.",
                    KnowledgeSourceTypes.WebLinks => "Web bağlantıları kaynağı güncellendi.",
                    _ => "Kaynak güncellendi; yeniden tarama bekleniyor."
                },
                Now = DateTime.UtcNow
            });
        return changed > 0;
    }

    public async Task<bool> SetSourceEnabledAsync(int id, bool isEnabled, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var changed = await connection.ExecuteAsync("UPDATE knowledge_sources SET is_enabled = @IsEnabled, updated_at_utc = @Now WHERE id = @Id;", new { Id = id, IsEnabled = isEnabled ? 1 : 0, Now = DateTime.UtcNow });
        return changed > 0;
    }

    public async Task<bool> SetSourceArchivedAsync(int id, bool isArchived, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var changed = await connection.ExecuteAsync("UPDATE knowledge_sources SET is_archived = @IsArchived, updated_at_utc = @Now WHERE id = @Id;", new { Id = id, IsArchived = isArchived ? 1 : 0, Now = DateTime.UtcNow });
        return changed > 0;
    }

    public async Task<bool> DeleteSourceAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync("DELETE FROM raw_files WHERE source_id = @Id;", new { Id = id }, transaction);
        await connection.ExecuteAsync("DELETE FROM web_pages WHERE source_id = @Id;", new { Id = id }, transaction);
        await connection.ExecuteAsync("DELETE FROM knowledge_folders WHERE knowledge_source_guid IN (SELECT knowledge_source_guid FROM knowledge_sources WHERE id = @Id);", new { Id = id }, transaction);
        var changed = await connection.ExecuteAsync("DELETE FROM knowledge_sources WHERE id = @Id;", new { Id = id }, transaction);
        await transaction.CommitAsync(cancellationToken);
        return changed > 0;
    }

    public async Task<bool> SetDocumentEnabledAsync(int id, bool isEnabled, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var changed = await connection.ExecuteAsync("UPDATE web_pages SET is_enabled = @IsEnabled, last_checked_at_utc = @Now WHERE id = @Id;", new { Id = id, IsEnabled = isEnabled ? 1 : 0, Now = DateTime.UtcNow });
        return changed > 0;
    }

    public async Task<bool> SetDocumentArchivedAsync(int id, bool isArchived, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var changed = await connection.ExecuteAsync("UPDATE web_pages SET is_archived = @IsArchived, last_checked_at_utc = @Now WHERE id = @Id;", new { Id = id, IsArchived = isArchived ? 1 : 0, Now = DateTime.UtcNow });
        return changed > 0;
    }

    public async Task<bool> UpdateDocumentAsync(int id, string title, string content, bool isEnabled, CancellationToken cancellationToken)
    {
        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? "Başlıksız belge" : title.Trim();
        var normalizedContent = NormalizeText(content);
        if (normalizedContent.Length == 0)
        {
            return false;
        }

        var preview = EnsurePreview(normalizedContent, normalizedTitle);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var changed = await connection.ExecuteAsync(
            @"UPDATE web_pages
              SET web_title = @Title,
                  web_content = @Content,
                  content_preview = @Preview,
                  is_enabled = @IsEnabled,
                  status_code = 'ok',
                  status_label = 'Tamam',
                  status_message = 'Belge kullanıcı tarafından düzenlendi.',
                  http_status_code = COALESCE(http_status_code, 200),
                  preview_status = 'manual',
                  preview_generated_at_utc = @Now,
                  last_checked_at_utc = @Now,
                  content_hash = @ContentHash,
                  last_seen_at_utc = @Now,
                  last_crawled_at_utc = @Now,
                  origin = CASE WHEN origin = 'web_crawl' THEN 'manual_upload' ELSE origin END
              WHERE id = @Id;",
            new
            {
                Id = id,
                Title = normalizedTitle,
                Content = normalizedContent,
                Preview = preview,
                IsEnabled = isEnabled ? 1 : 0,
                ContentHash = CalculateHash(normalizedContent),
                Now = DateTime.UtcNow
            });
        return changed > 0;
    }

    public async Task<WebPage?> GetDocumentByUrlAsync(string url, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<WebPage>(
            $"{PageSelectSql} WHERE p.web_source_url = @Url;",
            new { Url = NormalizeDocumentUrl(url) });
    }

    public async Task<int> AddManualWebDocumentAsync(int sourceId, WebPage page, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var source = await connection.QuerySingleOrDefaultAsync<KnowledgeSource>(SourceSelectSql("WHERE s.id = @Id GROUP BY s.id"), new { Id = sourceId });
        if (source is null)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        var normalizedContent = NormalizeText(page.WebContent);
        if (normalizedContent.Length == 0)
        {
            throw new InvalidOperationException("Web belgesinden kullanılabilir metin alınamadı.");
        }

        var normalizedUrl = NormalizeDocumentUrl(page.WebSourceUrl);
        var existingId = await connection.ExecuteScalarAsync<int?>("SELECT id FROM web_pages WHERE web_source_url = @Url;", new { Url = normalizedUrl });
        if (existingId is not null)
        {
            throw new InvalidOperationException("Bu web bağlantısı Bilgi Bankası'nda zaten var.");
        }

        var title = string.IsNullOrWhiteSpace(page.WebTitle) ? BuildTitleFromUrl(normalizedUrl) : page.WebTitle!.Trim();
        var changed = await connection.ExecuteAsync(
            @"INSERT INTO web_pages (
                source_id, tenant_guid, tenant_knowledge_guid, knowledge_source_guid, source_folder_guid, folder_document_guid,
                web_source_url, web_title, web_content, content_preview, is_enabled, is_archived,
                status_code, status_label, status_message, http_status_code, preview_status, preview_generated_at_utc,
                last_checked_at_utc, content_hash, first_seen_at_utc, last_seen_at_utc, last_crawled_at_utc,
                original_file_name, normalized_relative_path, normalized_folder_path, storage_directory, stored_file_name,
                file_extension, file_size_bytes, file_hash, parse_status, ocr_status, origin
              )
              VALUES (
                @SourceId, @TenantGuid, @TenantKnowledgeGuid, @KnowledgeSourceGuid, @SourceFolderGuid, @FolderDocumentGuid,
                @WebSourceUrl, @WebTitle, @WebContent, @ContentPreview, @IsEnabled, 0,
                @StatusCode, @StatusLabel, @StatusMessage, @HttpStatusCode, @PreviewStatus, @Now,
                @Now, @ContentHash, @Now, @Now, @Now,
                @OriginalFileName, @NormalizedRelativePath, @NormalizedFolderPath, @StorageDirectory, @StoredFileName,
                @FileExtension, @FileSizeBytes, @FileHash, @ParseStatus, @OcrStatus, 'manual_web_link'
              );",
            new
            {
                SourceId = source.Id,
                source.TenantGuid,
                source.TenantKnowledgeGuid,
                source.KnowledgeSourceGuid,
                SourceFolderGuid = page.SourceFolderGuid,
                FolderDocumentGuid = page.FolderDocumentGuid,
                WebSourceUrl = normalizedUrl,
                WebTitle = title,
                WebContent = normalizedContent,
                ContentPreview = EnsurePreview(normalizedContent, title),
                IsEnabled = page.IsEnabled ? 1 : 0,
                page.StatusCode,
                page.StatusLabel,
                page.StatusMessage,
                page.HttpStatusCode,
                PreviewStatus = string.IsNullOrWhiteSpace(page.PreviewStatus) ? "scraped" : page.PreviewStatus,
                ContentHash = CalculateHash(normalizedContent),
                OriginalFileName = page.OriginalFileName,
                FileExtension = page.FileExtension,
                FileSizeBytes = page.FileSizeBytes,
                FileHash = page.FileHash,
                ParseStatus = page.ParseStatus,
                OcrStatus = page.OcrStatus,
                NormalizedRelativePath = page.NormalizedRelativePath,
                NormalizedFolderPath = page.NormalizedFolderPath,
                StorageDirectory = page.StorageDirectory,
                StoredFileName = page.StoredFileName,
                Now = now
            });
        return changed;
    }

    public async Task<bool> UpdateWebDocumentLinkAsync(int id, WebPage page, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var existing = await connection.QuerySingleOrDefaultAsync<WebPage>($"{PageSelectSql} WHERE p.id = @Id;", new { Id = id });
        if (existing is null)
        {
            return false;
        }

        var normalizedUrl = NormalizeDocumentUrl(page.WebSourceUrl);
        var duplicateId = await connection.ExecuteScalarAsync<int?>(
            "SELECT id FROM web_pages WHERE web_source_url = @Url AND id <> @Id;",
            new { Url = normalizedUrl, Id = id });
        if (duplicateId is not null)
        {
            throw new InvalidOperationException("Bu web bağlantısı Bilgi Bankası'nda başka bir belge tarafından kullanılıyor.");
        }

        var now = DateTime.UtcNow;
        var normalizedContent = NormalizeText(page.WebContent);
        if (normalizedContent.Length == 0)
        {
            throw new InvalidOperationException("Web belgesinden kullanılabilir metin alınamadı.");
        }

        var title = string.IsNullOrWhiteSpace(page.WebTitle) ? BuildTitleFromUrl(normalizedUrl) : page.WebTitle!.Trim();
        var origin = existing.SourceType.Equals(KnowledgeSourceTypes.WebLinks, StringComparison.OrdinalIgnoreCase)
            ? "manual_web_link"
            : existing.Origin;
        var changed = await connection.ExecuteAsync(
            @"UPDATE web_pages
              SET web_source_url = @WebSourceUrl,
                  web_title = @WebTitle,
                  web_content = @WebContent,
                  content_preview = @ContentPreview,
                  status_code = @StatusCode,
                  status_label = @StatusLabel,
                  status_message = @StatusMessage,
                  http_status_code = @HttpStatusCode,
                  preview_status = @PreviewStatus,
                  preview_generated_at_utc = @Now,
                  last_checked_at_utc = @Now,
                  content_hash = @ContentHash,
                  last_seen_at_utc = @Now,
                  last_crawled_at_utc = @Now,
                  source_folder_guid = @SourceFolderGuid,
                  folder_document_guid = @FolderDocumentGuid,
                  normalized_relative_path = @NormalizedRelativePath,
                  normalized_folder_path = @NormalizedFolderPath,
                  storage_directory = @StorageDirectory,
                  stored_file_name = @StoredFileName,
                  original_file_name = @OriginalFileName,
                  file_extension = @FileExtension,
                  file_size_bytes = @FileSizeBytes,
                  file_hash = @FileHash,
                  parse_status = @ParseStatus,
                  ocr_status = @OcrStatus,
                  origin = @Origin
              WHERE id = @Id;",
            new
            {
                Id = id,
                WebSourceUrl = normalizedUrl,
                WebTitle = title,
                WebContent = normalizedContent,
                ContentPreview = EnsurePreview(normalizedContent, title),
                page.StatusCode,
                page.StatusLabel,
                page.StatusMessage,
                page.HttpStatusCode,
                PreviewStatus = string.IsNullOrWhiteSpace(page.PreviewStatus) ? "scraped" : page.PreviewStatus,
                ContentHash = CalculateHash(normalizedContent),
                SourceFolderGuid = page.SourceFolderGuid,
                FolderDocumentGuid = page.FolderDocumentGuid,
                NormalizedRelativePath = page.NormalizedRelativePath,
                NormalizedFolderPath = page.NormalizedFolderPath,
                StorageDirectory = page.StorageDirectory,
                StoredFileName = page.StoredFileName,
                OriginalFileName = page.OriginalFileName,
                FileExtension = page.FileExtension,
                FileSizeBytes = page.FileSizeBytes,
                FileHash = page.FileHash,
                ParseStatus = page.ParseStatus,
                OcrStatus = page.OcrStatus,
                Origin = origin,
                Now = now
            });
        return changed > 0;
    }

    public async Task<bool> DeleteDocumentAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync("DELETE FROM raw_files WHERE document_id = @Id;", new { Id = id }, transaction);
        var changed = await connection.ExecuteAsync("DELETE FROM web_pages WHERE id = @Id;", new { Id = id }, transaction);
        await transaction.CommitAsync(cancellationToken);
        return changed > 0;
    }

    public async Task<IReadOnlyDictionary<string, WebPage>> GetAllPagesByUrlAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<WebPage>(PageSelectSql);
        return rows.ToDictionary(x => x.WebSourceUrl, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<WebPage>> GetAllPagesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<WebPage>($@"
            {PageSelectSql}
            WHERE p.is_enabled = 1
              AND p.is_archived = 0
              AND p.status_code = 'ok'
              AND length(trim(p.web_content)) > 0
              AND (s.id IS NULL OR (s.is_enabled = 1 AND s.is_archived = 0))
            ORDER BY p.last_crawled_at_utc DESC, p.web_source_url ASC;");
        return rows.ToList();
    }

    public async Task<IReadOnlyList<WebPage>> GetKnowledgeSourcesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<WebPage>($@"
            {PageSelectSql}
            ORDER BY s.name ASC, p.web_title ASC, p.web_source_url ASC;");
        return rows.ToList();
    }

    public async Task<IReadOnlyList<KnowledgeDocumentCacheBlock>> RebuildDocumentCacheBlocksAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var pages = (await connection.QueryAsync<WebPage>($@"
            {PageSelectSql}
            WHERE p.is_archived = 0
              AND p.status_code = 'ok'
              AND length(trim(p.web_content)) > 0
              AND p.source_id IS NOT NULL
              AND (s.id IS NULL OR s.is_archived = 0)
            ORDER BY s.name ASC, p.web_title ASC, p.web_source_url ASC;", transaction: transaction)).ToList();

        var blocks = pages.Select(BuildDocumentCacheBlock).ToList();
        if (blocks.Count == 0)
        {
            await connection.ExecuteAsync("DELETE FROM knowledge_document_cache_blocks;", transaction: transaction);
            await transaction.CommitAsync(cancellationToken);
            return blocks;
        }

        foreach (var block in blocks)
        {
            await connection.ExecuteAsync(
                @"INSERT INTO knowledge_document_cache_blocks (
                    document_id, source_id, source_name, source_type, document_title, document_url,
                    document_citation_label, content_hash, prompt_block, updated_at_utc
                  ) VALUES (
                    @DocumentId, @SourceId, @SourceName, @SourceType, @DocumentTitle, @DocumentUrl,
                    @DocumentCitationLabel, @ContentHash, @PromptBlock, @UpdatedAtUtc
                  )
                  ON CONFLICT(document_id) DO UPDATE SET
                    source_id = excluded.source_id,
                    source_name = excluded.source_name,
                    source_type = excluded.source_type,
                    document_title = excluded.document_title,
                    document_url = excluded.document_url,
                    document_citation_label = excluded.document_citation_label,
                    content_hash = excluded.content_hash,
                    prompt_block = excluded.prompt_block,
                    updated_at_utc = excluded.updated_at_utc;",
                block,
                transaction);
        }

        await connection.ExecuteAsync(
            "DELETE FROM knowledge_document_cache_blocks WHERE document_id NOT IN @Ids;",
            new { Ids = blocks.Select(block => block.DocumentId).ToArray() },
            transaction);
        await transaction.CommitAsync(cancellationToken);
        return blocks;
    }

    public async Task<IReadOnlyList<KnowledgeDocumentCacheBlock>> GetActiveDocumentCacheBlocksAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<KnowledgeDocumentCacheBlock>(
            @"SELECT
                b.document_id AS DocumentId,
                b.source_id AS SourceId,
                b.source_name AS SourceName,
                b.source_type AS SourceType,
                b.document_title AS DocumentTitle,
                b.document_url AS DocumentUrl,
                b.document_citation_label AS DocumentCitationLabel,
                b.content_hash AS ContentHash,
                b.prompt_block AS PromptBlock,
                b.updated_at_utc AS UpdatedAtUtc
              FROM knowledge_document_cache_blocks b
              INNER JOIN web_pages p ON p.id = b.document_id
              INNER JOIN knowledge_sources s ON s.id = b.source_id
              WHERE p.is_enabled = 1
                AND p.is_archived = 0
                AND p.status_code = 'ok'
                AND s.is_enabled = 1
                AND s.is_archived = 0
              ORDER BY b.source_name ASC, b.document_title ASC, b.document_url ASC;");
        return rows.ToList();
    }

    public async Task<KnowledgeDocumentContent?> GetDisplayableDocumentContentAsync(int documentId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<KnowledgeDocumentContent>(
            @"SELECT
                p.id AS Id,
                p.source_id AS SourceId,
                COALESCE(s.name, 'Bilgi Bankası') AS SourceName,
                COALESCE(s.source_type, 'web_site') AS SourceType,
                COALESCE(NULLIF(p.web_title, ''), NULLIF(p.original_file_name, ''), p.web_source_url) AS Title,
                p.web_source_url AS Url,
                p.web_content AS Content,
                p.original_file_name AS OriginalFileName,
                p.last_checked_at_utc AS LastCheckedAtUtc,
                p.last_crawled_at_utc AS LastCrawledAtUtc
              FROM web_pages p
              INNER JOIN knowledge_sources s ON s.id = p.source_id
              WHERE p.id = @DocumentId
                AND p.is_enabled = 1
                AND p.is_archived = 0
                AND p.status_code = 'ok'
                AND s.is_enabled = 1
                AND s.is_archived = 0;",
            new { DocumentId = documentId });
    }

    public async Task<IReadOnlyList<KnowledgeFolder>> GetFoldersAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<KnowledgeFolder>(@"
            SELECT id AS Id,
                   tenant_guid AS TenantGuid,
                   tenant_knowledge_guid AS TenantKnowledgeGuid,
                   knowledge_source_guid AS KnowledgeSourceGuid,
                   source_folder_guid AS SourceFolderGuid,
                   folder_name AS FolderName,
                   normalized_folder_path AS NormalizedFolderPath,
                   created_at_utc AS CreatedAtUtc,
                   updated_at_utc AS UpdatedAtUtc
            FROM knowledge_folders
            ORDER BY normalized_folder_path ASC;");
        return rows.ToList();
    }

    public async Task UpsertPagesAsync(IReadOnlyCollection<WebPage> pages, CancellationToken cancellationToken)
    {
        if (pages.Count == 0)
        {
            return;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var page in pages)
        {
            page.WebSourceUrl = NormalizeDocumentUrl(page.WebSourceUrl);
            var source = page.SourceId is null
                ? await EnsureWebSourceAsync(connection, transaction, page.WebSourceUrl, cancellationToken)
                : await GetSourceByIdAsync(connection, page.SourceId.Value, transaction);
            var now = DateTime.UtcNow;
            page.TenantGuid = source.TenantGuid;
            page.TenantKnowledgeGuid = source.TenantKnowledgeGuid;
            page.KnowledgeSourceGuid = source.KnowledgeSourceGuid;
            page.ContentPreview = string.IsNullOrWhiteSpace(page.ContentPreview) ? EnsurePreview(page.WebContent, page.WebTitle ?? source.Name) : page.ContentPreview.Trim();
            page.PreviewGeneratedAtUtc ??= now;
            page.LastCheckedAtUtc ??= now;
            page.StatusCode = string.IsNullOrWhiteSpace(page.StatusCode) ? "ok" : page.StatusCode;
            page.StatusLabel = string.IsNullOrWhiteSpace(page.StatusLabel) ? "Tamam" : page.StatusLabel;
            page.StatusMessage = string.IsNullOrWhiteSpace(page.StatusMessage) ? "Belge kullanılabilir." : page.StatusMessage;
            page.ContentHash = string.IsNullOrWhiteSpace(page.ContentHash) ? CalculateHash(page.WebContent) : page.ContentHash;
            page.Origin = string.IsNullOrWhiteSpace(page.Origin) ? "web_crawl" : page.Origin.Trim();

            await connection.ExecuteAsync(
                @"INSERT INTO web_pages (
                    source_id, tenant_guid, tenant_knowledge_guid, knowledge_source_guid, source_folder_guid, folder_document_guid,
                    web_source_url, web_title, web_content, content_preview, is_enabled, is_archived,
                    status_code, status_label, status_message, http_status_code, preview_status, preview_generated_at_utc,
                    last_checked_at_utc, content_hash, first_seen_at_utc, last_seen_at_utc, last_crawled_at_utc, origin
                  )
                  VALUES (
                    @SourceId, @TenantGuid, @TenantKnowledgeGuid, @KnowledgeSourceGuid, '', '',
                    @WebSourceUrl, @WebTitle, @WebContent, @ContentPreview, @IsEnabled, @IsArchived,
                    @StatusCode, @StatusLabel, @StatusMessage, @HttpStatusCode, @PreviewStatus, @PreviewGeneratedAtUtc,
                    @LastCheckedAtUtc, @ContentHash, @FirstSeenAtUtc, @LastSeenAtUtc, @LastCrawledAtUtc, @Origin
                  )
                  ON CONFLICT(web_source_url) DO UPDATE SET
                    source_id = excluded.source_id,
                    tenant_guid = excluded.tenant_guid,
                    tenant_knowledge_guid = excluded.tenant_knowledge_guid,
                    knowledge_source_guid = excluded.knowledge_source_guid,
                    web_title = excluded.web_title,
                    web_content = excluded.web_content,
                    content_preview = excluded.content_preview,
                    status_code = excluded.status_code,
                    status_label = excluded.status_label,
                    status_message = excluded.status_message,
                    http_status_code = excluded.http_status_code,
                    preview_status = excluded.preview_status,
                    preview_generated_at_utc = excluded.preview_generated_at_utc,
                    last_checked_at_utc = excluded.last_checked_at_utc,
                    content_hash = excluded.content_hash,
                    last_seen_at_utc = excluded.last_seen_at_utc,
                    last_crawled_at_utc = excluded.last_crawled_at_utc,
                    origin = excluded.origin
                  WHERE web_pages.origin = excluded.origin;",
                new
                {
                    SourceId = source.Id,
                    page.TenantGuid,
                    page.TenantKnowledgeGuid,
                    page.KnowledgeSourceGuid,
                    page.WebSourceUrl,
                    page.WebTitle,
                    page.WebContent,
                    page.ContentPreview,
                    IsEnabled = page.IsEnabled ? 1 : 0,
                    IsArchived = page.IsArchived ? 1 : 0,
                    page.StatusCode,
                    page.StatusLabel,
                    page.StatusMessage,
                    page.HttpStatusCode,
                    page.PreviewStatus,
                    page.PreviewGeneratedAtUtc,
                    page.LastCheckedAtUtc,
                    page.ContentHash,
                    page.FirstSeenAtUtc,
                    page.LastSeenAtUtc,
                    page.LastCrawledAtUtc,
                    page.Origin
                },
                transaction);

            await connection.ExecuteAsync(
                @"UPDATE knowledge_sources
                  SET status_code = @StatusCode,
                      status_label = @StatusLabel,
                      status_message = @StatusMessage,
                      last_checked_at_utc = @Now,
                      updated_at_utc = @Now
                  WHERE id = @SourceId;",
                new { SourceId = source.Id, page.StatusCode, page.StatusLabel, page.StatusMessage, Now = now },
                transaction);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<(int Added, int Updated, int Deleted, int Unchanged)> ReplaceWebCrawlDocumentsForSourceAsync(
        int sourceId,
        IReadOnlyCollection<WebPage> pages,
        DateTime refreshedAtUtc,
        CancellationToken cancellationToken)
    {
        if (pages.Count == 0)
        {
            throw new InvalidOperationException("Seed source refresh produced no documents.");
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var source = await GetSourceByIdAsync(connection, sourceId, transaction);
        var existingDocuments = (await connection.QueryAsync<WebPage>(
            $"{PageSelectSql} WHERE p.source_id = @SourceId AND p.origin = 'web_crawl';",
            new { SourceId = sourceId },
            transaction)).ToList();
        var existingByUrl = existingDocuments.ToDictionary(document => NormalizeDocumentUrl(document.WebSourceUrl), StringComparer.OrdinalIgnoreCase);
        var normalizedPages = new List<WebPage>();

        await connection.ExecuteAsync("DROP TABLE IF EXISTS temp.oyako_refresh_stage_documents;", transaction: transaction);
        await connection.ExecuteAsync(@"
            CREATE TEMP TABLE oyako_refresh_stage_documents (
                source_id INTEGER NOT NULL,
                tenant_guid TEXT NOT NULL,
                tenant_knowledge_guid TEXT NOT NULL,
                knowledge_source_guid TEXT NOT NULL,
                source_folder_guid TEXT NOT NULL,
                folder_document_guid TEXT NOT NULL,
                web_source_url TEXT NOT NULL,
                web_title TEXT NULL,
                web_content TEXT NOT NULL,
                content_preview TEXT NOT NULL,
                is_enabled INTEGER NOT NULL,
                is_archived INTEGER NOT NULL,
                status_code TEXT NOT NULL,
                status_label TEXT NOT NULL,
                status_message TEXT NOT NULL,
                http_status_code INTEGER NULL,
                preview_status TEXT NOT NULL,
                preview_generated_at_utc TEXT NULL,
                last_checked_at_utc TEXT NULL,
                content_hash TEXT NOT NULL,
                first_seen_at_utc TEXT NOT NULL,
                last_seen_at_utc TEXT NOT NULL,
                last_crawled_at_utc TEXT NOT NULL,
                original_file_name TEXT NOT NULL,
                normalized_relative_path TEXT NOT NULL,
                normalized_folder_path TEXT NOT NULL,
                storage_directory TEXT NOT NULL,
                stored_file_name TEXT NOT NULL,
                file_extension TEXT NOT NULL,
                file_size_bytes INTEGER NOT NULL,
                file_hash TEXT NOT NULL,
                parse_status TEXT NOT NULL,
                ocr_status TEXT NOT NULL,
                origin TEXT NOT NULL
            );", transaction: transaction);

        foreach (var page in pages)
        {
            var normalizedUrl = NormalizeDocumentUrl(page.WebSourceUrl);
            var title = string.IsNullOrWhiteSpace(page.WebTitle) ? BuildTitleFromUrl(normalizedUrl) : page.WebTitle!.Trim();
            var content = NormalizeText(page.WebContent);
            if (content.Length == 0)
            {
                continue;
            }

            var normalized = new WebPage
            {
                SourceId = source.Id,
                SourceName = source.Name,
                SourceType = source.SourceType,
                TenantGuid = source.TenantGuid,
                TenantKnowledgeGuid = source.TenantKnowledgeGuid,
                KnowledgeSourceGuid = source.KnowledgeSourceGuid,
                SourceFolderGuid = page.SourceFolderGuid,
                FolderDocumentGuid = page.FolderDocumentGuid,
                WebSourceUrl = normalizedUrl,
                WebTitle = title,
                WebContent = content,
                ContentPreview = string.IsNullOrWhiteSpace(page.ContentPreview) ? EnsurePreview(content, title) : page.ContentPreview.Trim(),
                IsEnabled = page.IsEnabled,
                IsArchived = page.IsArchived,
                StatusCode = string.IsNullOrWhiteSpace(page.StatusCode) ? "ok" : page.StatusCode,
                StatusLabel = string.IsNullOrWhiteSpace(page.StatusLabel) ? "Tamam" : page.StatusLabel,
                StatusMessage = string.IsNullOrWhiteSpace(page.StatusMessage) ? "Belge kullanılabilir." : page.StatusMessage,
                HttpStatusCode = page.HttpStatusCode,
                PreviewStatus = string.IsNullOrWhiteSpace(page.PreviewStatus) ? "scraped" : page.PreviewStatus,
                PreviewGeneratedAtUtc = page.PreviewGeneratedAtUtc ?? refreshedAtUtc,
                LastCheckedAtUtc = refreshedAtUtc,
                ContentHash = string.IsNullOrWhiteSpace(page.ContentHash) ? CalculateHash(content) : page.ContentHash,
                FirstSeenAtUtc = existingByUrl.TryGetValue(normalizedUrl, out var existing) ? existing.FirstSeenAtUtc : refreshedAtUtc,
                LastSeenAtUtc = refreshedAtUtc,
                LastCrawledAtUtc = refreshedAtUtc,
                OriginalFileName = page.OriginalFileName,
                NormalizedRelativePath = page.NormalizedRelativePath,
                NormalizedFolderPath = page.NormalizedFolderPath,
                StorageDirectory = page.StorageDirectory,
                StoredFileName = page.StoredFileName,
                FileExtension = page.FileExtension,
                FileSizeBytes = page.FileSizeBytes,
                FileHash = page.FileHash,
                ParseStatus = page.ParseStatus,
                OcrStatus = page.OcrStatus,
                Origin = "web_crawl"
            };

            normalizedPages.Add(normalized);
            await connection.ExecuteAsync(
                @"INSERT INTO oyako_refresh_stage_documents (
                    source_id, tenant_guid, tenant_knowledge_guid, knowledge_source_guid, source_folder_guid, folder_document_guid,
                    web_source_url, web_title, web_content, content_preview, is_enabled, is_archived,
                    status_code, status_label, status_message, http_status_code, preview_status, preview_generated_at_utc,
                    last_checked_at_utc, content_hash, first_seen_at_utc, last_seen_at_utc, last_crawled_at_utc,
                    original_file_name, normalized_relative_path, normalized_folder_path, storage_directory, stored_file_name,
                    file_extension, file_size_bytes, file_hash, parse_status, ocr_status, origin)
                  VALUES (
                    @SourceId, @TenantGuid, @TenantKnowledgeGuid, @KnowledgeSourceGuid, @SourceFolderGuid, @FolderDocumentGuid,
                    @WebSourceUrl, @WebTitle, @WebContent, @ContentPreview, @IsEnabled, @IsArchived,
                    @StatusCode, @StatusLabel, @StatusMessage, @HttpStatusCode, @PreviewStatus, @PreviewGeneratedAtUtc,
                    @LastCheckedAtUtc, @ContentHash, @FirstSeenAtUtc, @LastSeenAtUtc, @LastCrawledAtUtc,
                    @OriginalFileName, @NormalizedRelativePath, @NormalizedFolderPath, @StorageDirectory, @StoredFileName,
                    @FileExtension, @FileSizeBytes, @FileHash, @ParseStatus, @OcrStatus, @Origin);",
                new
                {
                    SourceId = normalized.SourceId,
                    normalized.TenantGuid,
                    normalized.TenantKnowledgeGuid,
                    normalized.KnowledgeSourceGuid,
                    normalized.SourceFolderGuid,
                    normalized.FolderDocumentGuid,
                    normalized.WebSourceUrl,
                    normalized.WebTitle,
                    normalized.WebContent,
                    normalized.ContentPreview,
                    IsEnabled = normalized.IsEnabled ? 1 : 0,
                    IsArchived = normalized.IsArchived ? 1 : 0,
                    normalized.StatusCode,
                    normalized.StatusLabel,
                    normalized.StatusMessage,
                    normalized.HttpStatusCode,
                    normalized.PreviewStatus,
                    normalized.PreviewGeneratedAtUtc,
                    normalized.LastCheckedAtUtc,
                    normalized.ContentHash,
                    normalized.FirstSeenAtUtc,
                    normalized.LastSeenAtUtc,
                    normalized.LastCrawledAtUtc,
                    normalized.OriginalFileName,
                    normalized.NormalizedRelativePath,
                    normalized.NormalizedFolderPath,
                    normalized.StorageDirectory,
                    normalized.StoredFileName,
                    normalized.FileExtension,
                    normalized.FileSizeBytes,
                    normalized.FileHash,
                    normalized.ParseStatus,
                    normalized.OcrStatus,
                    normalized.Origin
                },
                transaction);
        }

        if (normalizedPages.Count == 0)
        {
            throw new InvalidOperationException("Seed source refresh produced no usable documents.");
        }

        var normalizedUrls = normalizedPages.Select(page => page.WebSourceUrl).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = normalizedPages.Count(page => !existingByUrl.ContainsKey(page.WebSourceUrl));
        var updated = normalizedPages.Count(page =>
            existingByUrl.TryGetValue(page.WebSourceUrl, out var existing)
            && (!string.Equals(existing.ContentHash, page.ContentHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(existing.StatusCode, page.StatusCode, StringComparison.OrdinalIgnoreCase)
                || existing.HttpStatusCode != page.HttpStatusCode));
        var unchanged = normalizedPages.Count(page =>
            existingByUrl.TryGetValue(page.WebSourceUrl, out var existing)
            && string.Equals(existing.ContentHash, page.ContentHash, StringComparison.OrdinalIgnoreCase)
            && string.Equals(existing.StatusCode, page.StatusCode, StringComparison.OrdinalIgnoreCase)
            && existing.HttpStatusCode == page.HttpStatusCode);
        var deleted = existingDocuments.Count(document => !normalizedUrls.Contains(NormalizeDocumentUrl(document.WebSourceUrl)));

        await connection.ExecuteAsync(
            "DELETE FROM web_pages WHERE source_id = @SourceId AND origin = 'web_crawl';",
            new { SourceId = sourceId },
            transaction);
        await connection.ExecuteAsync(@"
            INSERT INTO web_pages (
                source_id, tenant_guid, tenant_knowledge_guid, knowledge_source_guid, source_folder_guid, folder_document_guid,
                web_source_url, web_title, web_content, content_preview, is_enabled, is_archived,
                status_code, status_label, status_message, http_status_code, preview_status, preview_generated_at_utc,
                last_checked_at_utc, content_hash, first_seen_at_utc, last_seen_at_utc, last_crawled_at_utc,
                original_file_name, normalized_relative_path, normalized_folder_path, storage_directory, stored_file_name,
                file_extension, file_size_bytes, file_hash, parse_status, ocr_status, origin)
            SELECT
                source_id, tenant_guid, tenant_knowledge_guid, knowledge_source_guid, source_folder_guid, folder_document_guid,
                web_source_url, web_title, web_content, content_preview, is_enabled, is_archived,
                status_code, status_label, status_message, http_status_code, preview_status, preview_generated_at_utc,
                last_checked_at_utc, content_hash, first_seen_at_utc, last_seen_at_utc, last_crawled_at_utc,
                original_file_name, normalized_relative_path, normalized_folder_path, storage_directory, stored_file_name,
                file_extension, file_size_bytes, file_hash, parse_status, ocr_status, origin
            FROM oyako_refresh_stage_documents;",
            transaction: transaction);

        var nextRefreshAtUtc = refreshedAtUtc.AddMinutes(Math.Max(1, source.RefreshPeriodMinutes));
        await connection.ExecuteAsync(
            @"UPDATE knowledge_sources
              SET status_code = 'ok',
                  status_label = 'Tamam',
                  status_message = 'Kaynak yenilendi.',
                  last_checked_at_utc = @RefreshedAtUtc,
                  last_refresh_at_utc = @RefreshedAtUtc,
                  next_refresh_at_utc = @NextRefreshAtUtc,
                  updated_at_utc = @RefreshedAtUtc
              WHERE id = @SourceId;",
            new { SourceId = sourceId, RefreshedAtUtc = refreshedAtUtc, NextRefreshAtUtc = nextRefreshAtUtc },
            transaction);
        await connection.ExecuteAsync("DROP TABLE IF EXISTS temp.oyako_refresh_stage_documents;", transaction: transaction);
        await transaction.CommitAsync(cancellationToken);
        return (added, updated, deleted, unchanged);
    }

    public async Task UpdateSeedSourceRefreshStatusAsync(
        int sourceId,
        string statusCode,
        string statusLabel,
        string statusMessage,
        DateTime checkedAtUtc,
        DateTime nextRefreshAtUtc,
        bool markSuccessfulRefresh,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            @"UPDATE knowledge_sources
              SET status_code = @StatusCode,
                  status_label = @StatusLabel,
                  status_message = @StatusMessage,
                  last_checked_at_utc = @CheckedAtUtc,
                  last_refresh_at_utc = CASE WHEN @MarkSuccessfulRefresh = 1 THEN @CheckedAtUtc ELSE last_refresh_at_utc END,
                  next_refresh_at_utc = @NextRefreshAtUtc,
                  updated_at_utc = @CheckedAtUtc
              WHERE id = @SourceId AND is_seed_managed = 1;",
            new
            {
                SourceId = sourceId,
                StatusCode = statusCode,
                StatusLabel = statusLabel,
                StatusMessage = statusMessage,
                CheckedAtUtc = checkedAtUtc,
                NextRefreshAtUtc = nextRefreshAtUtc,
                MarkSuccessfulRefresh = markSuccessfulRefresh ? 1 : 0
            });
    }

    public async Task<int> MarkMissingWebDocumentsForSourceAsync(int sourceId, IReadOnlyCollection<string> discoveredUrls, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var normalizedUrls = discoveredUrls.Select(NormalizeDocumentUrl).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingDocuments = (await connection.QueryAsync<WebPage>(
            $"{PageSelectSql} WHERE p.source_id = @SourceId AND p.origin = 'web_crawl';",
            new { SourceId = sourceId })).ToList();
        var missingDocuments = existingDocuments
            .Where(document => !normalizedUrls.Contains(NormalizeDocumentUrl(document.WebSourceUrl)))
            .ToList();
        if (missingDocuments.Count == 0)
        {
            return 0;
        }

        var deleted = await connection.ExecuteAsync(
            "DELETE FROM web_pages WHERE id IN @Ids AND source_id = @SourceId AND origin = 'web_crawl';",
            new { Ids = missingDocuments.Select(document => document.Id).ToArray(), SourceId = sourceId });
        return deleted;
    }

    public async Task<bool> MarkDocumentInvalidAsync(int documentId, string statusCode, string statusLabel, string statusMessage, int? httpStatusCode, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var changed = await connection.ExecuteAsync(
            @"UPDATE web_pages
              SET status_code = @StatusCode,
                  status_label = @StatusLabel,
                  status_message = @StatusMessage,
                  http_status_code = @HttpStatusCode,
                  content_preview = @StatusMessage,
                  preview_status = 'status',
                  preview_generated_at_utc = @Now,
                  last_checked_at_utc = @Now,
                  last_crawled_at_utc = @Now
              WHERE id = @DocumentId;",
            new
            {
                DocumentId = documentId,
                StatusCode = string.IsNullOrWhiteSpace(statusCode) ? "invalid" : statusCode.Trim(),
                StatusLabel = string.IsNullOrWhiteSpace(statusLabel) ? "Geçersiz" : statusLabel.Trim(),
                StatusMessage = string.IsNullOrWhiteSpace(statusMessage) ? "Belge yeniden indirilemedi." : statusMessage.Trim(),
                HttpStatusCode = httpStatusCode,
                Now = now
            });
        return changed > 0;
    }

    public async Task DeleteByUrlsAsync(IReadOnlyCollection<string> urls, CancellationToken cancellationToken)
    {
        if (urls.Count == 0)
        {
            return;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync("DELETE FROM web_pages WHERE web_source_url IN @Urls AND origin = 'web_crawl';", new { Urls = urls.Select(NormalizeDocumentUrl).ToArray() });
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync("DELETE FROM web_pages WHERE origin = 'web_crawl';");
    }

    public async Task<KnowledgeFolder> GetOrCreateFolderAsync(KnowledgeSource source, string folderName, string normalizedFolderPath, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var name = string.IsNullOrWhiteSpace(folderName) ? "Kök Klasör" : folderName.Trim();
        var path = string.IsNullOrWhiteSpace(normalizedFolderPath) ? "/" : normalizedFolderPath;
        await connection.ExecuteAsync(
            @"INSERT INTO knowledge_folders (
                tenant_guid, tenant_knowledge_guid, knowledge_source_guid, source_folder_guid, folder_name, normalized_folder_path, created_at_utc, updated_at_utc)
              VALUES (@TenantGuid, @TenantKnowledgeGuid, @KnowledgeSourceGuid, @SourceFolderGuid, @FolderName, @NormalizedFolderPath, @Now, @Now)
              ON CONFLICT(tenant_guid, tenant_knowledge_guid, knowledge_source_guid, normalized_folder_path)
              DO UPDATE SET folder_name = excluded.folder_name, updated_at_utc = excluded.updated_at_utc;",
            new
            {
                source.TenantGuid,
                source.TenantKnowledgeGuid,
                source.KnowledgeSourceGuid,
                SourceFolderGuid = Guid.NewGuid().ToString("D"),
                FolderName = name,
                NormalizedFolderPath = path,
                Now = now
            });

        return await connection.QuerySingleAsync<KnowledgeFolder>(@"
            SELECT id AS Id,
                   tenant_guid AS TenantGuid,
                   tenant_knowledge_guid AS TenantKnowledgeGuid,
                   knowledge_source_guid AS KnowledgeSourceGuid,
                   source_folder_guid AS SourceFolderGuid,
                   folder_name AS FolderName,
                   normalized_folder_path AS NormalizedFolderPath,
                   created_at_utc AS CreatedAtUtc,
                   updated_at_utc AS UpdatedAtUtc
            FROM knowledge_folders
            WHERE tenant_guid = @TenantGuid
              AND tenant_knowledge_guid = @TenantKnowledgeGuid
              AND knowledge_source_guid = @KnowledgeSourceGuid
              AND normalized_folder_path = @NormalizedFolderPath;",
            new { source.TenantGuid, source.TenantKnowledgeGuid, source.KnowledgeSourceGuid, NormalizedFolderPath = path });
    }

    public async Task<KnowledgeFolder> EnsureFolderAsync(KnowledgeSource source, string sourceFolderGuid, string folderName, string normalizedFolderPath, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var name = string.IsNullOrWhiteSpace(folderName) ? "Kök Klasör" : folderName.Trim();
        var path = string.IsNullOrWhiteSpace(normalizedFolderPath) ? "/" : normalizedFolderPath;
        await connection.ExecuteAsync(
            @"INSERT INTO knowledge_folders (
                tenant_guid, tenant_knowledge_guid, knowledge_source_guid, source_folder_guid, folder_name, normalized_folder_path, created_at_utc, updated_at_utc)
              VALUES (@TenantGuid, @TenantKnowledgeGuid, @KnowledgeSourceGuid, @SourceFolderGuid, @FolderName, @NormalizedFolderPath, @Now, @Now)
              ON CONFLICT(tenant_guid, tenant_knowledge_guid, knowledge_source_guid, normalized_folder_path)
              DO UPDATE SET
                source_folder_guid = excluded.source_folder_guid,
                folder_name = excluded.folder_name,
                updated_at_utc = excluded.updated_at_utc;",
            new
            {
                source.TenantGuid,
                source.TenantKnowledgeGuid,
                source.KnowledgeSourceGuid,
                SourceFolderGuid = string.IsNullOrWhiteSpace(sourceFolderGuid) ? Guid.NewGuid().ToString("D") : sourceFolderGuid,
                FolderName = name,
                NormalizedFolderPath = path,
                Now = now
            });

        return await connection.QuerySingleAsync<KnowledgeFolder>(@"
            SELECT id AS Id,
                   tenant_guid AS TenantGuid,
                   tenant_knowledge_guid AS TenantKnowledgeGuid,
                   knowledge_source_guid AS KnowledgeSourceGuid,
                   source_folder_guid AS SourceFolderGuid,
                   folder_name AS FolderName,
                   normalized_folder_path AS NormalizedFolderPath,
                   created_at_utc AS CreatedAtUtc,
                   updated_at_utc AS UpdatedAtUtc
            FROM knowledge_folders
            WHERE tenant_guid = @TenantGuid
              AND tenant_knowledge_guid = @TenantKnowledgeGuid
              AND knowledge_source_guid = @KnowledgeSourceGuid
              AND normalized_folder_path = @NormalizedFolderPath;",
            new { source.TenantGuid, source.TenantKnowledgeGuid, source.KnowledgeSourceGuid, NormalizedFolderPath = path });
    }

    public async Task<LocalDocumentIdentity?> FindLocalDocumentIdentityAsync(int sourceId, string normalizedFolderPath, string normalizedRelativePath, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<LocalDocumentIdentity>(@"
            SELECT rf.document_id AS DocumentId,
                   rf.tenant_guid AS TenantGuid,
                   rf.tenant_knowledge_guid AS TenantKnowledgeGuid,
                   rf.knowledge_source_guid AS KnowledgeSourceGuid,
                   rf.source_folder_guid AS SourceFolderGuid,
                   rf.folder_document_guid AS FolderDocumentGuid,
                   rf.storage_directory AS StorageDirectory,
                   rf.stored_file_name AS StoredFileName
            FROM raw_files rf
            WHERE rf.source_id = @SourceId
              AND rf.normalized_folder_path = @NormalizedFolderPath
              AND rf.normalized_relative_path = @NormalizedRelativePath;",
            new { SourceId = sourceId, NormalizedFolderPath = normalizedFolderPath, NormalizedRelativePath = normalizedRelativePath });
    }

    public async Task<int> UpsertLocalDocumentAsync(LocalDocumentUpsert document, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(
            @"INSERT INTO web_pages (
                source_id, tenant_guid, tenant_knowledge_guid, knowledge_source_guid, source_folder_guid, folder_document_guid,
                web_source_url, web_title, web_content, content_preview, is_enabled, is_archived,
                status_code, status_label, status_message, http_status_code, preview_status, preview_generated_at_utc,
                last_checked_at_utc, content_hash, first_seen_at_utc, last_seen_at_utc, last_crawled_at_utc,
                original_file_name, normalized_relative_path, normalized_folder_path, storage_directory, stored_file_name,
                file_extension, file_size_bytes, file_hash, parse_status, ocr_status, origin)
              VALUES (
                @SourceId, @TenantGuid, @TenantKnowledgeGuid, @KnowledgeSourceGuid, @SourceFolderGuid, @FolderDocumentGuid,
                @WebSourceUrl, @Title, @Content, @ContentPreview, 1, 0,
                'ok', 'Tamam', 'Belge kullanılabilir.', 200, 'parsed', @Now,
                @Now, @ContentHash, @Now, @Now, @Now,
                @OriginalFileName, @NormalizedRelativePath, @NormalizedFolderPath, @StorageDirectory, @StoredFileName,
                @FileExtension, @FileSizeBytes, @FileHash, @ParseStatus, @OcrStatus, @Origin)
              ON CONFLICT(web_source_url) DO UPDATE SET
                web_title = excluded.web_title,
                web_content = excluded.web_content,
                content_preview = excluded.content_preview,
                status_code = 'ok',
                status_label = 'Tamam',
                status_message = 'Belge güncellendi.',
                preview_status = 'parsed',
                preview_generated_at_utc = excluded.preview_generated_at_utc,
                last_checked_at_utc = excluded.last_checked_at_utc,
                content_hash = excluded.content_hash,
                last_seen_at_utc = excluded.last_seen_at_utc,
                last_crawled_at_utc = excluded.last_crawled_at_utc,
                original_file_name = excluded.original_file_name,
                normalized_relative_path = excluded.normalized_relative_path,
                normalized_folder_path = excluded.normalized_folder_path,
                storage_directory = excluded.storage_directory,
                stored_file_name = excluded.stored_file_name,
                file_extension = excluded.file_extension,
                file_size_bytes = excluded.file_size_bytes,
                file_hash = excluded.file_hash,
                parse_status = excluded.parse_status,
                ocr_status = excluded.ocr_status,
                origin = excluded.origin;",
            new
            {
                document.SourceId,
                document.TenantGuid,
                document.TenantKnowledgeGuid,
                document.KnowledgeSourceGuid,
                document.SourceFolderGuid,
                document.FolderDocumentGuid,
                document.WebSourceUrl,
                document.Title,
                document.Content,
                document.ContentPreview,
                document.ContentHash,
                document.OriginalFileName,
                document.NormalizedRelativePath,
                document.NormalizedFolderPath,
                document.StorageDirectory,
                document.StoredFileName,
                document.FileExtension,
                document.FileSizeBytes,
                document.FileHash,
                document.ParseStatus,
                document.OcrStatus,
                document.Origin,
                Now = now
            },
            transaction);

        var documentId = await connection.ExecuteScalarAsync<int>("SELECT id FROM web_pages WHERE web_source_url = @Url;", new { Url = document.WebSourceUrl }, transaction);
        await connection.ExecuteAsync(
            @"INSERT INTO raw_files (
                raw_file_id, source_id, document_id, tenant_guid, tenant_knowledge_guid, knowledge_source_guid, source_folder_guid,
                folder_document_guid, original_file_name, normalized_folder_path, normalized_relative_path, storage_directory,
                stored_file_name, extension, mime_type, file_size_bytes, file_hash, content_hash, parse_status, ocr_status,
                created_at_utc, updated_at_utc)
              VALUES (
                @RawFileId, @SourceId, @DocumentId, @TenantGuid, @TenantKnowledgeGuid, @KnowledgeSourceGuid, @SourceFolderGuid,
                @FolderDocumentGuid, @OriginalFileName, @NormalizedFolderPath, @NormalizedRelativePath, @StorageDirectory,
                @StoredFileName, @FileExtension, '', @FileSizeBytes, @FileHash, @ContentHash, @ParseStatus, @OcrStatus,
                @Now, @Now)
              ON CONFLICT(source_id, normalized_folder_path, normalized_relative_path) DO UPDATE SET
                document_id = excluded.document_id,
                folder_document_guid = excluded.folder_document_guid,
                original_file_name = excluded.original_file_name,
                storage_directory = excluded.storage_directory,
                stored_file_name = excluded.stored_file_name,
                extension = excluded.extension,
                file_size_bytes = excluded.file_size_bytes,
                file_hash = excluded.file_hash,
                content_hash = excluded.content_hash,
                parse_status = excluded.parse_status,
                ocr_status = excluded.ocr_status,
                updated_at_utc = excluded.updated_at_utc;",
            new
            {
                RawFileId = document.FolderDocumentGuid,
                document.SourceId,
                DocumentId = documentId,
                document.TenantGuid,
                document.TenantKnowledgeGuid,
                document.KnowledgeSourceGuid,
                document.SourceFolderGuid,
                document.FolderDocumentGuid,
                document.OriginalFileName,
                document.NormalizedFolderPath,
                document.NormalizedRelativePath,
                document.StorageDirectory,
                document.StoredFileName,
                document.FileExtension,
                document.FileSizeBytes,
                document.FileHash,
                document.ContentHash,
                document.ParseStatus,
                document.OcrStatus,
                Now = now
            },
            transaction);

        await transaction.CommitAsync(cancellationToken);
        return documentId;
    }

    public async Task<IReadOnlyList<WebPage>> GetDocumentsBySourceAsync(int sourceId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<WebPage>($"{PageSelectSql} WHERE p.source_id = @SourceId;", new { SourceId = sourceId });
        return rows.ToList();
    }

    public async Task<WebPage?> GetDocumentByIdAsync(int documentId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<WebPage>($"{PageSelectSql} WHERE p.id = @Id;", new { Id = documentId });
    }

    public async Task<KnowledgeUploadSettings> GetUploadSettingsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<KnowledgeUploadSettings>(@"
            SELECT max_file_size_mb AS MaxFileSizeMb,
                   max_batch_file_count AS MaxBatchFileCount,
                   max_batch_size_mb AS MaxBatchSizeMb,
                   updated_at_utc AS UpdatedAtUtc
            FROM knowledge_upload_settings
            WHERE id = 1;");
    }

    public async Task<KnowledgeUploadSettings> UpdateUploadSettingsAsync(KnowledgeUploadSettings settings, CancellationToken cancellationToken)
    {
        var normalized = new KnowledgeUploadSettings
        {
            MaxFileSizeMb = Math.Clamp(settings.MaxFileSizeMb, 1, 250),
            MaxBatchFileCount = Math.Clamp(settings.MaxBatchFileCount, 1, 1000),
            MaxBatchSizeMb = Math.Clamp(settings.MaxBatchSizeMb, 1, 2048),
            UpdatedAtUtc = DateTime.UtcNow
        };
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            @"UPDATE knowledge_upload_settings
              SET max_file_size_mb = @MaxFileSizeMb,
                  max_batch_file_count = @MaxBatchFileCount,
                  max_batch_size_mb = @MaxBatchSizeMb,
                  updated_at_utc = @UpdatedAtUtc
              WHERE id = 1;",
            normalized);
        return normalized;
    }

    public async Task<KnowledgeRefreshSettings> GetRefreshSettingsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleAsync<(int RefreshPeriodMinutes, DateTime UpdatedAtUtc)>(@"
            SELECT refresh_period_minutes AS RefreshPeriodMinutes,
                   updated_at_utc AS UpdatedAtUtc
            FROM knowledge_refresh_settings
            WHERE id = 1;");
        var (value, unit) = TenantRefreshPeriodParser.ToValueUnit(row.RefreshPeriodMinutes);
        return new KnowledgeRefreshSettings(value, unit, row.RefreshPeriodMinutes, row.UpdatedAtUtc);
    }

    public async Task<KnowledgeRefreshSettings> UpdateRefreshSettingsAsync(int refreshPeriodMinutes, CancellationToken cancellationToken)
    {
        var (value, unit) = TenantRefreshPeriodParser.ToValueUnit(refreshPeriodMinutes);
        var normalizedMinutes = unit switch
        {
            "minute" => value,
            "hour" => value * 60,
            "day" => value * 24 * 60,
            "week" => value * 7 * 24 * 60,
            _ => 60
        };
        var now = DateTime.UtcNow;
        var nextRefresh = now.AddMinutes(normalizedMinutes);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(
            @"INSERT INTO knowledge_refresh_settings (id, refresh_period_minutes, updated_at_utc)
              VALUES (1, @RefreshPeriodMinutes, @UpdatedAtUtc)
              ON CONFLICT(id) DO UPDATE SET
                refresh_period_minutes = excluded.refresh_period_minutes,
                updated_at_utc = excluded.updated_at_utc;",
            new { RefreshPeriodMinutes = normalizedMinutes, UpdatedAtUtc = now },
            transaction);
        await connection.ExecuteAsync(
            @"UPDATE knowledge_sources
              SET refresh_period_minutes = @RefreshPeriodMinutes,
                  next_refresh_at_utc = @NextRefreshAtUtc,
                  updated_at_utc = @UpdatedAtUtc
              WHERE is_seed_managed = 1;",
            new { RefreshPeriodMinutes = normalizedMinutes, NextRefreshAtUtc = nextRefresh, UpdatedAtUtc = now },
            transaction);
        await transaction.CommitAsync(cancellationToken);
        return new KnowledgeRefreshSettings(value, unit, normalizedMinutes, now);
    }

    public async Task<(string TenantGuid, string TenantKnowledgeGuid)> GetKnowledgeIdentityAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<(string TenantGuid, string TenantKnowledgeGuid)>(@"
            SELECT t.tenant_guid AS TenantGuid,
                   k.tenant_knowledge_guid AS TenantKnowledgeGuid
            FROM tenant_metadata t
            CROSS JOIN knowledge_bank_metadata k
            WHERE t.id = 1 AND k.id = 1;");
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync("PRAGMA foreign_keys = ON;");
        return connection;
    }

    private static string SourceSelectSql(string suffix)
    {
        return $@"SELECT
                s.id AS Id,
                s.tenant_guid AS TenantGuid,
                s.tenant_knowledge_guid AS TenantKnowledgeGuid,
                s.knowledge_source_guid AS KnowledgeSourceGuid,
                s.source_type AS SourceType,
                s.name AS Name,
                s.description AS Description,
                s.address AS Address,
                s.protocol AS Protocol,
                s.is_enabled AS IsEnabled,
                s.is_archived AS IsArchived,
                s.status_code AS StatusCode,
                s.status_label AS StatusLabel,
                s.status_message AS StatusMessage,
                s.last_checked_at_utc AS LastCheckedAtUtc,
                s.is_seed_managed AS IsSeedManaged,
                s.seed_key AS SeedKey,
                s.refresh_period_minutes AS RefreshPeriodMinutes,
                s.auto_refresh_enabled AS AutoRefreshEnabled,
                s.last_refresh_at_utc AS LastRefreshAtUtc,
                s.next_refresh_at_utc AS NextRefreshAtUtc,
                s.created_at_utc AS CreatedAtUtc,
                s.updated_at_utc AS UpdatedAtUtc,
                COUNT(p.id) AS DocumentCount,
                COALESCE(SUM(CASE WHEN p.is_enabled = 1 AND p.is_archived = 0 AND p.status_code = 'ok' THEN 1 ELSE 0 END), 0) AS ActiveDocumentCount
            FROM knowledge_sources s
            LEFT JOIN web_pages p ON p.source_id = s.id
            {suffix};";
    }

    private const string PageSelectSql = @"SELECT
                p.id AS Id,
                p.source_id AS SourceId,
                s.name AS SourceName,
                s.source_type AS SourceType,
                p.tenant_guid AS TenantGuid,
                p.tenant_knowledge_guid AS TenantKnowledgeGuid,
                p.knowledge_source_guid AS KnowledgeSourceGuid,
                p.source_folder_guid AS SourceFolderGuid,
                p.folder_document_guid AS FolderDocumentGuid,
                p.web_source_url AS WebSourceUrl,
                p.web_title AS WebTitle,
                p.web_content AS WebContent,
                p.content_preview AS ContentPreview,
                p.is_enabled AS IsEnabled,
                p.is_archived AS IsArchived,
                p.status_code AS StatusCode,
                p.status_label AS StatusLabel,
                p.status_message AS StatusMessage,
                p.http_status_code AS HttpStatusCode,
                p.preview_status AS PreviewStatus,
                p.preview_generated_at_utc AS PreviewGeneratedAtUtc,
                p.last_checked_at_utc AS LastCheckedAtUtc,
                p.content_hash AS ContentHash,
                p.first_seen_at_utc AS FirstSeenAtUtc,
                p.last_seen_at_utc AS LastSeenAtUtc,
                p.last_crawled_at_utc AS LastCrawledAtUtc,
                p.original_file_name AS OriginalFileName,
                p.normalized_relative_path AS NormalizedRelativePath,
                p.normalized_folder_path AS NormalizedFolderPath,
                p.storage_directory AS StorageDirectory,
                p.stored_file_name AS StoredFileName,
                p.file_extension AS FileExtension,
                p.file_size_bytes AS FileSizeBytes,
                p.file_hash AS FileHash,
                p.parse_status AS ParseStatus,
                p.ocr_status AS OcrStatus,
                p.origin AS Origin
            FROM web_pages p
            LEFT JOIN knowledge_sources s ON s.id = p.source_id";

    private static async Task<KnowledgeSource> GetSourceByAddressAsync(SqliteConnection connection, string address)
    {
        return await connection.QuerySingleAsync<KnowledgeSource>(SourceSelectSql("WHERE s.address = @Address GROUP BY s.id"), new { Address = address });
    }

    private static KnowledgeDocumentCacheBlock BuildDocumentCacheBlock(WebPage page)
    {
        var sourceName = string.IsNullOrWhiteSpace(page.SourceName) ? "Bilgi Bankası" : page.SourceName!.Trim();
        var title = BuildCitationTitle(page);
        var label = $"{sourceName} - {title}";
        var prompt = $"""
            [Knowledge Document]
            [DocumentId] {page.Id}
            [SourceId] {page.SourceId ?? 0}
            [SourceName] {sourceName}
            [SourceType] {page.SourceType}
            [DocumentTitle] {title}
            [DocumentUrl] {page.WebSourceUrl}
            [CitationLabel] {label}
            [Content]
            {page.WebContent}
            [/Content]
            [/Knowledge Document]
            """;

        return new KnowledgeDocumentCacheBlock
        {
            DocumentId = page.Id,
            SourceId = page.SourceId ?? 0,
            SourceName = sourceName,
            SourceType = page.SourceType,
            DocumentTitle = title,
            DocumentUrl = page.WebSourceUrl,
            DocumentCitationLabel = label,
            ContentHash = string.IsNullOrWhiteSpace(page.ContentHash) ? CalculateHash(page.WebContent) : page.ContentHash,
            PromptBlock = prompt.Trim(),
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static string BuildCitationTitle(WebPage page)
    {
        if (!string.IsNullOrWhiteSpace(page.WebTitle))
        {
            return page.WebTitle!.Trim();
        }

        if (!string.IsNullOrWhiteSpace(page.OriginalFileName))
        {
            return page.OriginalFileName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(page.NormalizedRelativePath))
        {
            return page.NormalizedRelativePath.Trim();
        }

        return BuildTitleFromUrl(page.WebSourceUrl);
    }

    private static async Task<KnowledgeSource> GetSourceByIdAsync(SqliteConnection connection, int id, DbTransaction transaction)
    {
        return await connection.QuerySingleAsync<KnowledgeSource>(SourceSelectSql("WHERE s.id = @Id GROUP BY s.id"), new { Id = id }, transaction);
    }

    private static async Task<KnowledgeSource> EnsureWebSourceAsync(SqliteConnection connection, DbTransaction transaction, string url, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var address = BuildSourceAddress(url);
        var existing = await connection.QuerySingleOrDefaultAsync<KnowledgeSource>(SourceSelectSql("WHERE s.address = @Address GROUP BY s.id"), new { Address = address }, transaction);
        if (existing is not null)
        {
            return existing;
        }

        var identity = await connection.QuerySingleAsync<(string TenantGuid, string TenantKnowledgeGuid)>(@"
            SELECT t.tenant_guid AS TenantGuid,
                   k.tenant_knowledge_guid AS TenantKnowledgeGuid
            FROM tenant_metadata t
            CROSS JOIN knowledge_bank_metadata k
            WHERE t.id = 1 AND k.id = 1;", transaction: transaction);
        var now = DateTime.UtcNow;
        await connection.ExecuteAsync(
            @"INSERT INTO knowledge_sources (
                tenant_guid, tenant_knowledge_guid, knowledge_source_guid, source_type, name, description, address, protocol,
                is_enabled, is_archived, status_code, status_label, status_message, created_at_utc, updated_at_utc)
              VALUES (@TenantGuid, @TenantKnowledgeGuid, @KnowledgeSourceGuid, @SourceType, @Name, '', @Address, @Protocol,
                1, 0, 'ok', 'Tamam', 'Kaynak kullanılabilir.', @Now, @Now);",
            new
            {
                identity.TenantGuid,
                identity.TenantKnowledgeGuid,
                KnowledgeSourceGuid = Guid.NewGuid().ToString("D"),
                SourceType = KnowledgeSourceTypes.WebSite,
                Name = BuildSourceName(address),
                Address = address,
                Protocol = new Uri(address).Scheme,
                Now = now
            },
            transaction);
        return await connection.QuerySingleAsync<KnowledgeSource>(SourceSelectSql("WHERE s.address = @Address GROUP BY s.id"), new { Address = address }, transaction);
    }

    private static string NormalizeSourceType(string sourceType)
    {
        if (sourceType.Equals(KnowledgeSourceTypes.LocalFiles, StringComparison.OrdinalIgnoreCase) || sourceType.Equals("Yerel Dosyalar", StringComparison.OrdinalIgnoreCase))
        {
            return KnowledgeSourceTypes.LocalFiles;
        }

        if (sourceType.Equals(KnowledgeSourceTypes.WebLinks, StringComparison.OrdinalIgnoreCase) || sourceType.Equals("Web Bağlantıları", StringComparison.OrdinalIgnoreCase))
        {
            return KnowledgeSourceTypes.WebLinks;
        }

        if (sourceType.Equals(KnowledgeSourceTypes.WebSite, StringComparison.OrdinalIgnoreCase) || sourceType.Equals("Web Sitesi", StringComparison.OrdinalIgnoreCase))
        {
            return KnowledgeSourceTypes.WebSite;
        }

        throw new ArgumentException("Geçerli bir kaynak türü seçin.");
    }

    private static string NormalizeAddress(string address)
    {
        if (!Uri.TryCreate(address.Trim(), UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Geçerli bir kaynak adresi girin.");
        }

        if (uri.Scheme is not "http" and not "https")
        {
            throw new ArgumentException("Web sitesi kaynağı için geçerli bir http/https adresi girin.");
        }

        return BuildCanonicalHttpUrl(uri, includePath: true);
    }

    private static string BuildSourceAddress(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        return uri.Scheme is "http" or "https" ? BuildCanonicalHttpUrl(uri, includePath: false) : uri.AbsoluteUri;
    }

    private static string NormalizeDocumentUrl(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return url.Trim();
        }

        return uri.Scheme is "http" or "https" ? BuildCanonicalHttpUrl(uri, includePath: true) : uri.AbsoluteUri;
    }

    private static string BuildCanonicalHttpUrl(Uri uri, bool includePath)
    {
        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        if (!includePath)
        {
            return $"{uri.Scheme.ToLowerInvariant()}://{host.ToLowerInvariant()}{port}";
        }

        var path = uri.AbsolutePath.Replace('\\', '/').TrimEnd('/');
        while (path.Contains("//", StringComparison.Ordinal))
        {
            path = path.Replace("//", "/", StringComparison.Ordinal);
        }

        path = string.IsNullOrWhiteSpace(path) ? string.Empty : path;
        return $"{uri.Scheme.ToLowerInvariant()}://{host.ToLowerInvariant()}{port}{path}";
    }

    private static string BuildDefaultSourceName(string sourceType, string? address)
    {
        if (sourceType == KnowledgeSourceTypes.LocalFiles)
        {
            return "Yerel Dosyalar";
        }

        if (sourceType == KnowledgeSourceTypes.WebLinks)
        {
            return "Web Bağlantıları";
        }

        return string.IsNullOrWhiteSpace(address) ? "Web Sitesi" : BuildSourceName(NormalizeAddress(address));
    }

    private static string BuildSourceName(string address)
    {
        var uri = new Uri(address);
        return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
    }

    private static string BuildTitleFromUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return string.IsNullOrWhiteSpace(url) ? "Belge" : url.Trim();
        }

        var lastSegment = uri.Segments.LastOrDefault()?.Trim('/').Trim();
        if (!string.IsNullOrWhiteSpace(lastSegment))
        {
            return Uri.UnescapeDataString(lastSegment.Replace('-', ' ').Replace('_', ' ')).Trim();
        }

        return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
    }

    private static string EnsurePreview(string content, string title)
    {
        var normalized = NormalizeText(content);
        if (normalized.Length == 0)
        {
            normalized = string.IsNullOrWhiteSpace(title) ? "Bu belge için içerik önizlemesi henüz alınamadı." : title.Trim();
        }

        return normalized.Length <= 260 ? normalized : $"{normalized[..260].TrimEnd()}...";
    }

    private static string NormalizeText(string text)
    {
        return string.Join(" ", text.Replace('\r', ' ').Replace('\n', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    private static string CalculateHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
