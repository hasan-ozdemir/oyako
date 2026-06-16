using System.Text;
using webapi_oyako.Application.Services;
using Xunit;

namespace webapi_oyako.Tests;

// Verifies local knowledge file parsing behavior for upload/import workflows.
public sealed class KnowledgeFileParserTests
{
    [Fact]
    // Ensures markdown-like local files become normalized text and usable previews.
    public async Task ParseAsync_MarkdownFile_ReturnsNormalizedContentAndPreview()
    {
        var parser = new KnowledgeFileParser();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("# Baslik\r\n\r\nOyak Dijital hizmetleri hakkinda temiz icerik."));

        var parsed = await parser.ParseAsync(stream, "bilgi.md", CancellationToken.None);

        Assert.Equal("bilgi.md", parsed.FileName);
        Assert.Equal(".md", parsed.Extension);
        Assert.Equal("parsed", parsed.ParseStatus);
        Assert.Contains("Oyak Dijital hizmetleri", parsed.Content);
        Assert.Contains("Oyak Dijital hizmetleri", parsed.ContentPreview);
        Assert.False(string.IsNullOrWhiteSpace(parsed.FileHash));
        Assert.False(string.IsNullOrWhiteSpace(parsed.ContentHash));
    }
}
