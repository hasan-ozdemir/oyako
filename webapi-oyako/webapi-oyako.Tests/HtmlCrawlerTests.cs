// Codex developer note: Explains the purpose and flow of webapi-oyako/webapi-oyako.Tests/HtmlCrawlerTests.cs for maintainers.
using System.Net;
using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using webapi_oyako.Application.Services;
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Infrastructure.Configuration;
using webapi_oyako.Infrastructure.Crawling;
using Xunit;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Tests;

// Implements the HtmlCrawlerTests component and its responsibilities in the Oyako codebase.
public class HtmlCrawlerTests
{
    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task CrawlAsync_StoresSuccessfulPagesAndContinuesAfterBlockedUrls()
    {
        // Creates the object needed for the next step of the workflow.
        var responses = new Dictionary<string, Func<HttpResponseMessage>>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://oyakdijital.com.tr/"] = () => Html("""
                <html>
                    <head><title>Tarayıcı Başlığı</title></head>
                    <body>
                        <h2>Kullanıcı Dostu Sayfa Başlığı</h2>
                        Ana sayfa iceriği
                        <a href="/cozumler//kurumsal-uygulama/">Kurumsal</a>
                        <a href="https://oyakdijital.com.tr/engellenen">Engellenen</a>
                        <script>{"href":"/script-icinden-gelen"}</script>
                    </body>
                </html>
                """),
            ["https://oyakdijital.com.tr/robots.txt"] = () => Text("Sitemap: https://www.oyakdijital.com.tr/sitemap.xml"),
            ["https://oyakdijital.com.tr/sitemap.xml"] = () => Xml("""
                <urlset>
                    <url><loc>https://www.oyakdijital.com.tr/sitemap-icinden-gelen</loc></url>
                </urlset>
                """),
            ["https://oyakdijital.com.tr/cozumler/kurumsal-uygulama"] = () => Html("<html><body>Kurumsal uygulama sayfa metni</body></html>"),
            ["https://oyakdijital.com.tr/script-icinden-gelen"] = () => Html("<html><body>Script icinden kesfedilen sayfa metni</body></html>"),
            ["https://oyakdijital.com.tr/sitemap-icinden-gelen"] = () => Html("<html><body>Sitemap icinden kesfedilen sayfa metni</body></html>"),
            // Creates the object needed for the next step of the workflow.
            ["https://oyakdijital.com.tr/engellenen"] = () => new HttpResponseMessage((HttpStatusCode)418)
        };

        // Creates a disposable resource scoped to this operation.
        using var httpClient = new HttpClient(new StubHttpHandler(responses));
        // Creates the object needed for the next step of the workflow.
        var crawler = new HtmlCrawler(
            httpClient,
            // Creates the object needed for the next step of the workflow.
            Options.Create(new CrawlerOptions
            {
                SeedUrl = "https://oyakdijital.com.tr",
                MaxPagesToCrawl = 10,
                MaxDepth = 4,
                MinimumTextLengthToStore = 5,
                MinimumRequestDelayMilliseconds = 0,
                MaximumRequestDelayMilliseconds = 0
            }),
            // Creates the object needed for the next step of the workflow.
            new RenderedTextExtractor(),
            // Creates the object needed for the next step of the workflow.
            new KnowledgeTextCleaner(),
            // Creates the repository stub required by the multi-source crawler workflow.
            new KnowledgeFileParser(),
            // Creates the repository stub required by the multi-source crawler workflow.
            new StubWebPageRepository());

        var result = await crawler.CrawlAsync(CancellationToken.None);

        // Verifies the expected behavior for this test scenario.
        Assert.True(result.IsSuccessful, string.Join(" | ", result.Errors.Concat(result.Warnings)));
        // Verifies the expected behavior for this test scenario.
        Assert.Contains(result.Pages, page => page.WebSourceUrl == "https://oyakdijital.com.tr/");
        // Verifies the expected behavior for this test scenario.
        Assert.Contains(result.Pages, page => page.WebSourceUrl == "https://oyakdijital.com.tr/" && page.WebTitle == "Kullanıcı Dostu Sayfa Başlığı");
        // Verifies the expected behavior for this test scenario.
        Assert.Contains(result.Pages, page => page.WebSourceUrl == "https://oyakdijital.com.tr/cozumler/kurumsal-uygulama");
        // Verifies the expected behavior for this test scenario.
        Assert.Contains(result.Pages, page => page.WebSourceUrl == "https://oyakdijital.com.tr/script-icinden-gelen");
        // Verifies the expected behavior for this test scenario.
        Assert.Contains(result.Pages, page => page.WebSourceUrl == "https://oyakdijital.com.tr/sitemap-icinden-gelen");
        // Verifies the expected behavior for this test scenario.
        Assert.DoesNotContain(result.Errors, error => error.Contains("HTTP 418", StringComparison.Ordinal));
        // Verifies the expected behavior for this test scenario.
        Assert.Contains(result.Warnings, warning => warning.Contains("HTTP 418", StringComparison.Ordinal));
    }

    [Fact]
    // Verifies one slow document times out quickly and does not prevent later documents from being crawled.
    public async Task CrawlAsync_SlowDocumentTimesOutAndCrawlerContinues()
    {
        var responses = new Dictionary<string, Func<CancellationToken, Task<HttpResponseMessage>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://slow.example/"] = _ => Task.FromResult(Html("""
                <html><body>
                    Root document with enough text.
                    <a href="/slow">Slow</a>
                    <a href="/fast">Fast</a>
                </body></html>
                """)),
            ["https://slow.example/sitemap.xml"] = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)),
            ["https://slow.example/slow"] = async cancellationToken =>
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                return Html("<html><body>Slow content should not block the crawl.</body></html>");
            },
            ["https://slow.example/fast"] = _ => Task.FromResult(Html("<html><body>Fast content is crawled after timeout.</body></html>"))
        };
        using var httpClient = new HttpClient(new AsyncStubHttpHandler(responses));
        var crawler = new HtmlCrawler(
            httpClient,
            Options.Create(new CrawlerOptions
            {
                SeedUrl = "https://slow.example",
                MaxPagesToCrawl = 5,
                MaxDepth = 2,
                RequestTimeoutSeconds = 1,
                MinimumTextLengthToStore = 5,
                MinimumRequestDelayMilliseconds = 0,
                MaximumRequestDelayMilliseconds = 0
            }),
            new RenderedTextExtractor(),
            new KnowledgeTextCleaner(),
            new KnowledgeFileParser(),
            new StubWebPageRepository());

        var result = await crawler.CrawlAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.IsSuccessful, string.Join(" | ", result.Errors.Concat(result.Warnings)));
        Assert.Contains(result.Pages, page => page.WebSourceUrl == "https://slow.example/slow" && page.StatusCode == "timeout");
        Assert.Contains(result.Pages, page => page.WebSourceUrl == "https://slow.example/fast" && page.StatusCode == "ok");
    }

    [Fact]
    // Verifies HTTP Retry-After headers do not stall the crawler before moving to the next document.
    public async Task CrawlAsync_RetryAfterDoesNotDelayFailForward()
    {
        var responses = new Dictionary<string, Func<HttpResponseMessage>>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://retry.example/"] = () => Html("""
                <html><body>
                    Root document with enough text.
                    <a href="/busy">Busy</a>
                    <a href="/available">Available</a>
                </body></html>
                """),
            ["https://retry.example/sitemap.xml"] = () => new HttpResponseMessage(HttpStatusCode.NotFound),
            ["https://retry.example/busy"] = () =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                response.Headers.RetryAfter = new(TimeSpan.FromSeconds(30));
                return response;
            },
            ["https://retry.example/available"] = () => Html("<html><body>Available content is fetched without Retry-After delay.</body></html>")
        };
        using var httpClient = new HttpClient(new StubHttpHandler(responses));
        var crawler = new HtmlCrawler(
            httpClient,
            Options.Create(new CrawlerOptions
            {
                SeedUrl = "https://retry.example",
                MaxPagesToCrawl = 5,
                MaxDepth = 2,
                RequestTimeoutSeconds = 5,
                MinimumTextLengthToStore = 5,
                MinimumRequestDelayMilliseconds = 0,
                MaximumRequestDelayMilliseconds = 0
            }),
            new RenderedTextExtractor(),
            new KnowledgeTextCleaner(),
            new KnowledgeFileParser(),
            new StubWebPageRepository());

        var result = await crawler.CrawlAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(3));

        Assert.True(result.IsSuccessful, string.Join(" | ", result.Errors.Concat(result.Warnings)));
        Assert.Contains(result.Pages, page => page.WebSourceUrl == "https://retry.example/busy" && page.StatusCode == "http503");
        Assert.Contains(result.Pages, page => page.WebSourceUrl == "https://retry.example/available" && page.StatusCode == "ok");
    }

    [Fact]
    // Verifies manual web-link PDFs are downloaded and parsed instead of being opened in Playwright.
    public async Task CrawlDocumentAsync_PdfUrl_DownloadsAndParsesWithoutRenderer()
    {
        var pdfBytes = Encoding.Latin1.GetBytes("%PDF-1.4\nBT (User stories applied usable pdf knowledge text for Oyako.) Tj ET");
        var responses = new Dictionary<string, Func<HttpResponseMessage>>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://example.com/User-Stories-Applied-Mike-Cohn.pdf"] = () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(pdfBytes)
                {
                    Headers =
                    {
                        ContentType = new("application/pdf")
                    }
                }
            }
        };
        var tempRoot = Path.Combine(Path.GetTempPath(), $"oyako-web-link-pdf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            using var httpClient = new HttpClient(new StubHttpHandler(responses));
            var renderer = new ThrowingPageRenderer();
            var crawler = new HtmlCrawler(
                httpClient,
                Options.Create(new CrawlerOptions
                {
                    MinimumTextLengthToStore = 5,
                    MinimumRequestDelayMilliseconds = 0,
                    MaximumRequestDelayMilliseconds = 0
                }),
                new RenderedTextExtractor(),
                new KnowledgeTextCleaner(),
                new KnowledgeFileParser(),
                new StubWebPageRepository(),
                renderer,
                new TestHostEnvironment(tempRoot));

            var page = await crawler.CrawlDocumentAsync(
                new WebPage
                {
                    SourceId = 2,
                    SourceName = "Online Books",
                    SourceType = KnowledgeSourceTypes.WebLinks,
                    TenantGuid = "tenant-guid",
                    TenantKnowledgeGuid = "knowledge-guid",
                    KnowledgeSourceGuid = "source-guid",
                    WebSourceUrl = "https://example.com/User-Stories-Applied-Mike-Cohn.pdf",
                    WebTitle = "User Stories Appliyed"
                },
                CancellationToken.None);

            Assert.Equal("ok", page.StatusCode);
            Assert.Equal("User Stories Appliyed", page.WebTitle);
            Assert.Contains("User stories applied usable pdf knowledge text", page.WebContent);
            Assert.Equal("User-Stories-Applied-Mike-Cohn.pdf", page.OriginalFileName);
            Assert.Equal(".pdf", page.FileExtension);
            Assert.Equal("parsed", page.ParseStatus);
            Assert.Equal("not_required", page.OcrStatus);
            Assert.True(page.FileSizeBytes > 0);
            Assert.False(string.IsNullOrWhiteSpace(page.FileHash));
            Assert.False(string.IsNullOrWhiteSpace(page.StorageDirectory));
            Assert.True(File.Exists(Path.Combine(page.StorageDirectory, page.StoredFileName)));
            Assert.False(renderer.WasCalled);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    // Verifies manual web-link HTML documents use the shared parser/storage pipeline instead of browser rendering.
    public async Task CrawlDocumentAsync_WebLinkHtml_DownloadsAndParsesWithSharedFilePipeline()
    {
        var responses = new Dictionary<string, Func<HttpResponseMessage>>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://example.com/manual-guide.html"] = () => Html("<html><body><h1>Manual Guide</h1><p>Shared parser text for a manual web-link HTML document.</p></body></html>")
        };
        var tempRoot = Path.Combine(Path.GetTempPath(), $"oyako-web-link-html-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            using var httpClient = new HttpClient(new StubHttpHandler(responses));
            var renderer = new ThrowingPageRenderer();
            var crawler = new HtmlCrawler(
                httpClient,
                Options.Create(new CrawlerOptions
                {
                    MinimumTextLengthToStore = 5,
                    MinimumRequestDelayMilliseconds = 0,
                    MaximumRequestDelayMilliseconds = 0
                }),
                new RenderedTextExtractor(),
                new KnowledgeTextCleaner(),
                new KnowledgeFileParser(),
                new StubWebPageRepository(),
                renderer,
                new TestHostEnvironment(tempRoot));

            var page = await crawler.CrawlDocumentAsync(
                new WebPage
                {
                    SourceId = 2,
                    SourceName = "Online Books",
                    SourceType = KnowledgeSourceTypes.WebLinks,
                    TenantGuid = "tenant-guid",
                    TenantKnowledgeGuid = "knowledge-guid",
                    KnowledgeSourceGuid = "source-guid",
                    WebSourceUrl = "https://example.com/manual-guide.html",
                    WebTitle = "Manual Guide"
                },
                CancellationToken.None);

            Assert.Equal("ok", page.StatusCode);
            Assert.Contains("Shared parser text for a manual web-link HTML document", page.WebContent);
            Assert.Equal("manual-guide.html", page.OriginalFileName);
            Assert.Equal(".html", page.FileExtension);
            Assert.Equal("parsed", page.ParseStatus);
            Assert.False(string.IsNullOrWhiteSpace(page.StorageDirectory));
            Assert.True(File.Exists(Path.Combine(page.StorageDirectory, page.StoredFileName)));
            Assert.False(renderer.WasCalled);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    // Verifies ordinary web-site HTML documents keep using the renderer so rendered-page crawling remains intact.
    public async Task CrawlDocumentAsync_WebSiteHtml_UsesRendererForRenderedContent()
    {
        using var httpClient = new HttpClient(new StubHttpHandler(new Dictionary<string, Func<HttpResponseMessage>>(StringComparer.OrdinalIgnoreCase)));
        var renderer = new RecordingPageRenderer(new RenderedPage(
            "https://example.com/rendered-page.html",
            "Browser Title",
            "Rendered Heading",
            "Rendered browser text from a web site page.",
            Array.Empty<string>()));
        var crawler = new HtmlCrawler(
            httpClient,
            Options.Create(new CrawlerOptions
            {
                MinimumTextLengthToStore = 5,
                MinimumRequestDelayMilliseconds = 0,
                MaximumRequestDelayMilliseconds = 0
            }),
            new RenderedTextExtractor(),
            new KnowledgeTextCleaner(),
            new KnowledgeFileParser(),
            new StubWebPageRepository(),
            renderer);

        var page = await crawler.CrawlDocumentAsync(
            new WebPage
            {
                SourceId = 1,
                SourceName = "Oyak Dijital",
                SourceType = KnowledgeSourceTypes.WebSite,
                WebSourceUrl = "https://example.com/rendered-page.html",
                WebTitle = "Existing Title"
            },
            CancellationToken.None);

        Assert.True(renderer.WasCalled);
        Assert.Equal("ok", page.StatusCode);
        Assert.Equal("Rendered Heading", page.WebTitle);
        Assert.Equal("Rendered browser text from a web site page.", page.WebContent);
        Assert.True(string.IsNullOrWhiteSpace(page.OriginalFileName));
        Assert.True(string.IsNullOrWhiteSpace(page.StorageDirectory));
    }

    [Fact]
    // Verifies renderer timeouts fall back to static HTTP HTML parsing when the document is directly readable.
    public async Task CrawlDocumentAsync_RendererTimeout_UsesStaticHttpFallback()
    {
        using var httpClient = new HttpClient(new StubHttpHandler(new Dictionary<string, Func<HttpResponseMessage>>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://slow-render.example/page"] = () => Html("<html><body><h1>Static Fallback</h1><p>Static fallback product content.</p></body></html>")
        }));
        var crawler = new HtmlCrawler(
            httpClient,
            Options.Create(new CrawlerOptions
            {
                MinimumTextLengthToStore = 5,
                MinimumRequestDelayMilliseconds = 0,
                MaximumRequestDelayMilliseconds = 0
            }),
            new RenderedTextExtractor(),
            new KnowledgeTextCleaner(),
            new KnowledgeFileParser(),
            new StubWebPageRepository(),
            new TimeoutPageRenderer());

        var page = await crawler.CrawlDocumentAsync(
            new WebPage
            {
                SourceId = 1,
                SourceName = "Slow Site",
                SourceType = KnowledgeSourceTypes.WebSite,
                WebSourceUrl = "https://slow-render.example/page",
                WebTitle = "Slow Render"
            },
            CancellationToken.None);

        Assert.Equal("ok", page.StatusCode);
        Assert.Equal("Static Fallback", page.WebTitle);
        Assert.Contains("Static fallback product content", page.WebContent);
    }

    [Fact]
    // Verifies compressed PDF streams are parsed without leaking binary text into knowledge content.
    public async Task ParseAsync_CompressedPdfStream_ReturnsReadableText()
    {
        var compressed = CompressPdfStream("BT (Compressed user story text from pdf stream.) Tj ET");
        var pdfBytes = Encoding.Latin1.GetBytes("%PDF-1.4\n1 0 obj\n<< /Filter /FlateDecode /Length ")
            .Concat(Encoding.ASCII.GetBytes(compressed.Length.ToString()))
            .Concat(Encoding.Latin1.GetBytes(" >>\nstream\n"))
            .Concat(compressed)
            .Concat(Encoding.Latin1.GetBytes("\nendstream\nendobj\n%%EOF"))
            .ToArray();
        var parser = new KnowledgeFileParser();
        await using var stream = new MemoryStream(pdfBytes);

        var parsed = await parser.ParseAsync(stream, "compressed.pdf", CancellationToken.None);

        Assert.Equal("parsed", parsed.ParseStatus);
        Assert.Contains("Compressed user story text", parsed.Content);
        Assert.DoesNotContain(parsed.Content, character => char.IsControl(character) && !char.IsWhiteSpace(character));
    }

    // Compresses fake PDF stream text with zlib so the parser test matches real FlateDecode PDFs.
    private static byte[] CompressPdfStream(string content)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            var bytes = Encoding.Latin1.GetBytes(content);
            zlib.Write(bytes);
        }

        return output.ToArray();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
        }

        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Oyako.Tests";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static HttpResponseMessage Html(string content)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            // Creates the object needed for the next step of the workflow.
            Content = new StringContent(content, Encoding.UTF8, "text/html")
        };
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static HttpResponseMessage Text(string content)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            // Creates the object needed for the next step of the workflow.
            Content = new StringContent(content, Encoding.UTF8, "text/plain")
        };
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static HttpResponseMessage Xml(string content)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            // Creates the object needed for the next step of the workflow.
            Content = new StringContent(content, Encoding.UTF8, "application/xml")
        };
    }

    private sealed class StubWebPageRepository : IWebPageRepository
    {
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
            return Task.FromResult<IReadOnlyDictionary<string, WebPage>>(new Dictionary<string, WebPage>());
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<IReadOnlyList<WebPage>> GetAllPagesAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult<IReadOnlyList<WebPage>>(Array.Empty<WebPage>());
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<IReadOnlyList<WebPage>> GetKnowledgeSourcesAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult<IReadOnlyList<WebPage>>(Array.Empty<WebPage>());
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

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, Func<HttpResponseMessage>> _responses;

        // Executes this component behavior as part of the Oyako application flow.
        public StubHttpHandler(IReadOnlyDictionary<string, Func<HttpResponseMessage>> responses)
        {
            _responses = responses;
        }

        // Executes this component behavior as part of the Oyako application flow.
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Guards the following branch so the workflow handles this condition deliberately.
            if (request.RequestUri is not null && _responses.TryGetValue(request.RequestUri.ToString(), out var responseFactory))
            {
                // Returns the computed result to the caller and completes this branch of the workflow.
                return Task.FromResult(responseFactory());
            }

            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class AsyncStubHttpHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, Func<CancellationToken, Task<HttpResponseMessage>>> _responses;

        public AsyncStubHttpHandler(IReadOnlyDictionary<string, Func<CancellationToken, Task<HttpResponseMessage>>> responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null && _responses.TryGetValue(request.RequestUri.ToString(), out var responseFactory))
            {
                return responseFactory(cancellationToken);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class ThrowingPageRenderer : IPageRenderer
    {
        public bool WasCalled { get; private set; }

        public Task<RenderedPage> RenderAsync(string url, CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new InvalidOperationException("Renderer should not be used for downloadable documents.");
        }
    }

    private sealed class RecordingPageRenderer : IPageRenderer
    {
        private readonly RenderedPage _page;

        public RecordingPageRenderer(RenderedPage page)
        {
            _page = page;
        }

        public bool WasCalled { get; private set; }

        public Task<RenderedPage> RenderAsync(string url, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(_page);
        }
    }

    private sealed class TimeoutPageRenderer : IPageRenderer
    {
        public Task<RenderedPage> RenderAsync(string url, CancellationToken cancellationToken)
        {
            throw new TimeoutException("Renderer exceeded the page budget.");
        }
    }
}
