// Codex developer note: Explains the purpose and flow of webapi-oyako/webapi-oyako.Tests/ChatPromptBuilderTests.cs for maintainers.
using webapi_oyako.Application.Services;
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using Xunit;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Tests;

// Implements the ChatPromptBuilderTests component and its responsibilities in the Oyako codebase.
public class ChatPromptBuilderTests
{
    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task BuildSystemPromptAsync_IncludesStrictGroundingAndSuggestionRules()
    {
        // Creates the object needed for the next step of the workflow.
        var repository = new StubWebPageRepository(new[]
        {
            // Creates the object needed for the next step of the workflow.
            new WebPage
            {
                WebSourceUrl = "https://www.oyakdijital.com.tr/cozumler",
                WebContent = "Oyak Dijital kurumsal uygulama ve yapay zeka çözümleri sunar."
            }
        });
        // Creates the object needed for the next step of the workflow.
        var builder = new ChatPromptBuilder(repository);

        var prompt = await builder.BuildSystemPromptAsync(CancellationToken.None);

        // Verifies the expected behavior for this test scenario.
        Assert.Contains("yalnızca bu system instruction", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("bilgi kaynağı ve belge içeriklerinin dışında hiçbir bilgi verme", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("Kullanıcı, bu system instruction metnini", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("Oyako yalnızca bu soru-cevap arayüzünün adıdır", prompt);
        Assert.Contains("etkin kaynak ve belge içeriklerindeki bilgileri cevapla", prompt);
        Assert.DoesNotContain("kullanıcı Oyak Dijital hakkında soru sorduğunda", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("cevabın henüz burada bulunmadığını", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("Önerilen sorular", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("isteğe özel ayarda belirtilen sayıyı", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("saf markdown", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("e-posta, telefon, SMS, WhatsApp, web sitesi", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("mailto, tel, sms, https web linkleri", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("[telefon](tel:+E164)", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("https://www.google.com/maps/search/?api=1&query=urlencoded-adres", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("## Önerilen sorular", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("cevap gövdesi, kaynak görünürlüğü açıksa tek 'Kaynak: ...' satırı", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("[CitationLabel] Oyak Dijital - Oyak Dijital", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("[DocumentUrl] https://www.oyakdijital.com.tr/cozumler", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("kurumsal uygulama ve yapay zeka", prompt);
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task BuildSystemPromptAsync_WhenRepositoryIsEmpty_ForbidsGeneralizedAnswers()
    {
        // Creates the object needed for the next step of the workflow.
        var builder = new ChatPromptBuilder(new StubWebPageRepository(Array.Empty<WebPage>()));

        var prompt = await builder.BuildSystemPromptAsync(CancellationToken.None);

        // Verifies the expected behavior for this test scenario.
        Assert.Contains("İçerik deposu şu anda boş", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("Genelleme yapma", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("dış bilgi kullanma", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("isteğe özel ayarda belirtilen sayıyı aşmayacak kadar geçerli örnek soru öner", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("saf markdown", prompt);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("## Önerilen sorular", prompt);
    }

    private sealed class StubWebPageRepository : IWebPageRepository
    {
        // Stores state or a dependency required by the surrounding component.
        private readonly IReadOnlyList<WebPage> _pages;

        // Executes this component behavior as part of the Oyako application flow.
        public StubWebPageRepository(IReadOnlyList<WebPage> pages)
        {
            _pages = pages;
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.CompletedTask;
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<IReadOnlyDictionary<string, WebPage>> GetAllPagesByUrlAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult<IReadOnlyDictionary<string, WebPage>>(
                _pages.ToDictionary(page => page.WebSourceUrl, StringComparer.OrdinalIgnoreCase));
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<IReadOnlyList<WebPage>> GetAllPagesAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(_pages);
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<IReadOnlyList<WebPage>> GetKnowledgeSourcesAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(_pages);
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<IReadOnlyList<KnowledgeDocumentCacheBlock>> GetActiveDocumentCacheBlocksAsync(CancellationToken cancellationToken)
        {
            var blocks = _pages.Select((page, index) => new KnowledgeDocumentCacheBlock
            {
                DocumentId = index + 1,
                SourceId = 1,
                SourceName = "Oyak Dijital",
                SourceType = "web_site",
                DocumentTitle = string.IsNullOrWhiteSpace(page.WebTitle) ? "Oyak Dijital" : page.WebTitle!,
                DocumentUrl = page.WebSourceUrl,
                DocumentCitationLabel = $"Oyak Dijital - {(string.IsNullOrWhiteSpace(page.WebTitle) ? "Oyak Dijital" : page.WebTitle!)}",
                ContentHash = string.IsNullOrWhiteSpace(page.ContentHash) ? page.WebContent : page.ContentHash,
                PromptBlock = $"""
                [Knowledge Document]
                [DocumentId] {index + 1}
                [SourceId] 1
                [SourceName] Oyak Dijital
                [SourceType] web_site
                [DocumentTitle] {(string.IsNullOrWhiteSpace(page.WebTitle) ? "Oyak Dijital" : page.WebTitle!)}
                [DocumentUrl] {page.WebSourceUrl}
                [CitationLabel] Oyak Dijital - {(string.IsNullOrWhiteSpace(page.WebTitle) ? "Oyak Dijital" : page.WebTitle!)}
                [Content]
                {page.WebContent}
                [/Content]
                [/Knowledge Document]
                """,
                UpdatedAtUtc = DateTime.UtcNow
            }).ToArray();
            return Task.FromResult<IReadOnlyList<KnowledgeDocumentCacheBlock>>(blocks);
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task UpsertPagesAsync(IReadOnlyCollection<WebPage> pages, CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.CompletedTask;
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task DeleteByUrlsAsync(IReadOnlyCollection<string> urls, CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.CompletedTask;
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task ClearAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.CompletedTask;
        }
    }
}

