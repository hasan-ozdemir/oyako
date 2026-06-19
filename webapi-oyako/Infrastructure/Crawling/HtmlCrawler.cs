// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Crawling/HtmlCrawler.cs for maintainers.
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using webapi_oyako.Application.Services;
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Domain.Services;
using webapi_oyako.Infrastructure.Configuration;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Infrastructure.Crawling;

// Implements the HtmlCrawler component and its responsibilities in the Oyako codebase.
public sealed class HtmlCrawler : IWebCrawler
{
    // Executes this component behavior as part of the Oyako application flow.
    private static readonly Regex SitemapLocPattern = new(@"<loc>\s*(?<url>.*?)\s*</loc>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    // Executes this component behavior as part of the Oyako application flow.
    private static readonly Regex EncodedHrefPattern = new(@"(?:""href""|""url"")\s*:\s*""(?<url>[^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    // Executes this component behavior as part of the Oyako application flow.
    private static readonly Regex QuotedPathPattern = new(@"""(?<url>/[a-zA-Z0-9][^""<>\s]*)""", RegexOptions.Compiled);
    // Extracts the first visible HTML heading so crawled documents use user-friendly page titles.
    private static readonly Regex FirstHeadingPattern = new(@"<h[1-6]\b[^>]*>(?<text>.*?)</h[1-6]>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    // Extracts the HTML document title as the fallback after heading-based title discovery.
    private static readonly Regex HtmlTitlePattern = new(@"<title\b[^>]*>(?<text>.*?)</title>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    // Removes nested markup from heading and title candidates before storing them as document titles.
    private static readonly Regex HtmlTagPattern = new(@"<[^>]+>", RegexOptions.Compiled | RegexOptions.Singleline);
    // Lists document file types that must be downloaded and parsed instead of opened in the browser renderer.
    private static readonly HashSet<string> DownloadableDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".docx",
        ".pdf",
        ".pptx",
        ".rtf",
        ".epub",
        ".md"
    };
    // Lists every document type that the shared file parser can normalize for manual web-link documents and local files.
    private static readonly HashSet<string> ParserDocumentExtensions = new(DownloadableDocumentExtensions, StringComparer.OrdinalIgnoreCase)
    {
        ".htm",
        ".html"
    };

    // Stores state or a dependency required by the surrounding component.
    private readonly HttpClient _httpClient;
    // Stores state or a dependency required by the surrounding component.
    private readonly CrawlerOptions _options;
    // Stores state or a dependency required by the surrounding component.
    private readonly ITextExtractor _textExtractor;
    // Stores state or a dependency required by the surrounding component.
    private readonly IKnowledgeTextCleaner _knowledgeTextCleaner;
    // Stores the parser used to convert downloaded documents such as PDFs into normalized knowledge text.
    private readonly IKnowledgeFileParser _knowledgeFileParser;
    // Stores the repository-local data root used for archived raw web-link documents.
    private readonly string _dataRoot;
    // Stores state or a dependency required by the surrounding component.
    private readonly IPageRenderer? _pageRenderer;
    // Stores state or a dependency required by the surrounding component.
    private readonly IWebPageRepository _webPageRepository;

    // Creates a new instance and captures the dependencies needed by this component.
    public HtmlCrawler(
        HttpClient httpClient,
        IOptions<CrawlerOptions> options,
        ITextExtractor textExtractor,
        IKnowledgeTextCleaner knowledgeTextCleaner,
        IKnowledgeFileParser knowledgeFileParser,
        IWebPageRepository webPageRepository,
        IPageRenderer? pageRenderer = null,
        IHostEnvironment? environment = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _textExtractor = textExtractor;
        _knowledgeTextCleaner = knowledgeTextCleaner;
        _knowledgeFileParser = knowledgeFileParser;
        _webPageRepository = webPageRepository;
        _pageRenderer = pageRenderer;
        _dataRoot = KnowledgeRawFileStorage.ResolveDataRoot(environment?.ContentRootPath ?? Directory.GetCurrentDirectory());
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<CrawlerResult> CrawlAsync(CancellationToken cancellationToken)
    {
        var sources = await _webPageRepository.GetActiveSourcesAsync(cancellationToken);
        if (sources.Count == 0)
        {
            sources = new[]
            {
                new KnowledgeSource
                {
                    Id = 0,
                    Name = "Oyak Dijital",
                    Address = _options.SeedUrl,
                    Protocol = new Uri(_options.SeedUrl).Scheme,
                    IsEnabled = true
                }
            };
        }

        var pages = new List<WebPage>();
        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var source in sources)
        {
            try
            {
                var uri = new Uri(source.Address);
                if (uri.IsFile)
                {
                    pages.Add(await BuildFileDocumentAsync(source, uri, cancellationToken));
                    continue;
                }

                if (uri.Scheme is "http" or "https")
                {
                    var result = await CrawlHttpSourceAsync(source, uri, cancellationToken);
                    pages.AddRange(result.Pages);
                    errors.AddRange(result.Errors);
                    warnings.AddRange(result.Warnings);
                    continue;
                }

                pages.Add(BuildStatusDocument(source, source.Address, source.Name, "unsupported_protocol", "Desteklenmeyen Protokol", $"{uri.Scheme} protokolü bu ortamda içerik okumak için desteklenmiyor.", null));
                warnings.Add($"[{source.Address}] Desteklenmeyen protokol: {uri.Scheme}");
            }
            catch (Exception ex)
            {
                pages.Add(BuildStatusDocument(source, source.Address, source.Name, "error", "Hata", ex.Message, null));
                errors.Add($"[{source.Address}] {ex.Message}");
            }
        }

        return new CrawlerResult(
            pages.Any(page => page.StatusCode == "ok"),
            pages,
            errors,
            warnings,
            DateTimeOffset.UtcNow);
    }

    // Crawls one configured source without reading unrelated Knowledge Bank sources.
    public async Task<CrawlerResult> CrawlSourceAsync(KnowledgeSource source, CancellationToken cancellationToken)
    {
        var pages = new List<WebPage>();
        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            var uri = new Uri(source.Address);
            if (uri.IsFile)
            {
                pages.Add(await BuildFileDocumentAsync(source, uri, cancellationToken));
            }
            else if (uri.Scheme is "http" or "https")
            {
                return await CrawlHttpSourceAsync(source, uri, cancellationToken);
            }
            else
            {
                pages.Add(BuildStatusDocument(source, source.Address, source.Name, "unsupported_protocol", "Desteklenmeyen Protokol", $"{uri.Scheme} protokolü bu ortamda içerik okumak için desteklenmiyor.", null));
                warnings.Add($"[{source.Address}] Desteklenmeyen protokol: {uri.Scheme}");
            }
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            pages.Add(BuildStatusDocument(source, source.Address, source.Name, "timeout", "Zaman Aşımı", ex.Message, null));
            errors.Add($"[{source.Address}] {ex.Message}");
        }
        catch (TimeoutException ex)
        {
            pages.Add(BuildStatusDocument(source, source.Address, source.Name, "timeout", "Zaman Aşımı", ex.Message, null));
            errors.Add($"[{source.Address}] {ex.Message}");
        }
        catch (Exception ex)
        {
            pages.Add(BuildStatusDocument(source, source.Address, source.Name, "error", "Hata", ex.Message, null));
            errors.Add($"[{source.Address}] {ex.Message}");
        }

        return new CrawlerResult(
            pages.Any(page => page.StatusCode == "ok"),
            pages,
            errors,
            warnings,
            DateTimeOffset.UtcNow);
    }

    // Crawls one exact document URL and returns a replacement WebPage payload for the same document.
    public async Task<WebPage> CrawlDocumentAsync(WebPage document, CancellationToken cancellationToken)
    {
        var documentTitle = GuessTitle(document.WebSourceUrl, document.WebTitle);
        var source = new KnowledgeSource
        {
            Id = document.SourceId ?? 0,
            Name = string.IsNullOrWhiteSpace(document.SourceName) ? documentTitle : document.SourceName.Trim(),
            TenantGuid = document.TenantGuid,
            TenantKnowledgeGuid = document.TenantKnowledgeGuid,
            KnowledgeSourceGuid = document.KnowledgeSourceGuid,
            SourceType = document.SourceType,
            Address = document.WebSourceUrl,
            Protocol = Uri.TryCreate(document.WebSourceUrl, UriKind.Absolute, out var parsedUri) ? parsedUri.Scheme : string.Empty,
            IsEnabled = true
        };

        if (!Uri.TryCreate(document.WebSourceUrl, UriKind.Absolute, out var uri))
        {
            return BuildStatusDocument(source, document.WebSourceUrl, documentTitle, "invalid_url", "Geçersiz", "Belge adresi geçerli bir URL değil.", null);
        }

        try
        {
            if (uri.IsFile)
            {
                return await BuildFileDocumentAsync(source, uri, cancellationToken);
            }

            if (uri.Scheme is "http" or "https")
            {
                return await CrawlHttpDocumentAsync(source, uri, documentTitle, document, cancellationToken);
            }

            return BuildStatusDocument(source, document.WebSourceUrl, documentTitle, "unsupported_protocol", "Desteklenmeyen Protokol", $"{uri.Scheme} protokolü bu ortamda içerik okumak için desteklenmiyor.", null);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return BuildStatusDocument(source, document.WebSourceUrl, documentTitle, "timeout", "Zaman Aşımı", ex.Message, null);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return BuildStatusDocument(source, document.WebSourceUrl, documentTitle, "timeout", "Zaman Aşımı", ex.Message, null);
        }
        catch (TimeoutException ex)
        {
            return BuildStatusDocument(source, document.WebSourceUrl, documentTitle, "timeout", "Zaman Aşımı", ex.Message, null);
        }
        catch (Exception ex)
        {
            return BuildStatusDocument(source, document.WebSourceUrl, documentTitle, "error", "Hata", ex.Message, null);
        }
    }

    // Executes this component behavior as part of the Oyako application flow.
    private async Task<CrawlerResult> CrawlHttpSourceAsync(KnowledgeSource source, Uri siteRootUri, CancellationToken cancellationToken)
    {
        var visitedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queuedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var discoveredPages = new Dictionary<string, WebPage>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<CrawlCandidate>();
        var errors = new List<string>();
        var warnings = new List<string>();
        var maxFetchAttempts = Math.Max(_options.MaxPagesToCrawl * 4, _options.MaxPagesToCrawl);

        foreach (var seedUrl in BuildSeedUrls(siteRootUri))
        {
            Enqueue(seedUrl, siteRootUri, 0, IsUtilityUrl(seedUrl));
        }

        while (queue.Count > 0
            && discoveredPages.Count < _options.MaxPagesToCrawl
            && visitedUrls.Count < maxFetchAttempts)
        {
            var current = queue.Dequeue();
            if (!visitedUrls.Add(current.Url))
            {
                continue;
            }

            try
            {
                await DelayBeforeRequestAsync(cancellationToken);
                if (!current.IsUtilityDocument && _pageRenderer is not null && ShouldRenderBeforeStaticHttp(current.Url))
                {
                    try
                    {
                        var rendered = await _pageRenderer.RenderAsync(current.Url, cancellationToken);
                        foreach (var link in rendered.Links)
                        {
                            Enqueue(link, new Uri(current.Url), current.Depth + 1, false);
                        }

                        var renderedText = _knowledgeTextCleaner.Clean(rendered.Text);
                        if (renderedText.Length >= _options.MinimumTextLengthToStore)
                        {
                            discoveredPages[current.Url] = BuildContentDocument(source, current.Url, GuessTitle(current.Url, rendered.FirstHeadingTitle, rendered.Title), renderedText, 200);
                        }

                        continue;
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        warnings.Add($"[{current.Url}] Render başarısız, HTTP metin fallback deneniyor: {ex.Message}");
                    }
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, current.Url);
                request.Headers.Referrer = siteRootUri;
                using var timeout = CreateDocumentTimeout(cancellationToken);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                var contentType = response.Content?.Headers.ContentType?.MediaType ?? string.Empty;

                if (!response.IsSuccessStatusCode)
                {
                    var statusCode = (int)response.StatusCode;
                    var issue = $"[{current.Url}] HTTP {statusCode} {response.ReasonPhrase}";
                    warnings.Add(issue);
                    if (!current.IsUtilityDocument)
                    {
                        discoveredPages[current.Url] = BuildStatusDocument(source, current.Url, GuessTitle(current.Url), $"http{statusCode}", $"http{statusCode}", issue, statusCode);
                    }

                    continue;
                }

                var html = response.Content is null
                    ? string.Empty
                    : await response.Content.ReadAsStringAsync(timeout.Token);

                foreach (var link in ExtractDiscoveryLinks(html, contentType))
                {
                    Enqueue(link, new Uri(current.Url), current.Depth + 1, false);
                }

                if (!current.IsUtilityDocument && IsHtmlDocument(contentType))
                {
                    var text = _knowledgeTextCleaner.Clean(_textExtractor.ExtractText(html));
                    if (text.Length >= _options.MinimumTextLengthToStore)
                    {
                        discoveredPages[current.Url] = BuildContentDocument(source, current.Url, GuessTitle(current.Url, ExtractFirstHeadingTitle(html), ExtractHtmlTitle(html)), text, 200);
                    }
                    else
                    {
                        discoveredPages[current.Url] = BuildStatusDocument(source, current.Url, GuessTitle(current.Url, ExtractFirstHeadingTitle(html), ExtractHtmlTitle(html)), "empty_content", "İçerik Yok", "Sayfada kullanılabilir metinsel içerik bulunamadı.", 200);
                    }
                }
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                discoveredPages[current.Url] = BuildStatusDocument(source, current.Url, GuessTitle(current.Url), "timeout", "Zaman Aşımı", ex.Message, null);
                warnings.Add($"[{current.Url}] Zaman aşımı: {ex.Message}");
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                discoveredPages[current.Url] = BuildStatusDocument(source, current.Url, GuessTitle(current.Url), "timeout", "Zaman Aşımı", ex.Message, null);
                warnings.Add($"[{current.Url}] Zaman aşımı: {ex.Message}");
            }
            catch (TimeoutException ex)
            {
                discoveredPages[current.Url] = BuildStatusDocument(source, current.Url, GuessTitle(current.Url), "timeout", "Zaman Aşımı", ex.Message, null);
                warnings.Add($"[{current.Url}] Zaman aşımı: {ex.Message}");
            }
            catch (Exception ex)
            {
                discoveredPages[current.Url] = BuildStatusDocument(source, current.Url, GuessTitle(current.Url), "error", "Hata", ex.Message, null);
                errors.Add($"[{current.Url}] {ex.Message}");
            }
        }

        return new CrawlerResult(
            discoveredPages.Values.Any(page => page.StatusCode == "ok"),
            discoveredPages.Values.ToList(),
            errors,
            warnings,
            DateTimeOffset.UtcNow);

        void Enqueue(string rawUrl, Uri sourceUri, int depth, bool isUtilityDocument)
        {
            if (depth > _options.MaxDepth)
            {
                return;
            }

            if (!UrlNormalizer.TryNormalize(siteRootUri, sourceUri, rawUrl, out var normalized, isUtilityDocument))
            {
                return;
            }

            if (queuedUrls.Add(normalized))
            {
                queue.Enqueue(new CrawlCandidate(normalized, depth, isUtilityDocument || IsUtilityUrl(normalized)));
            }
        }
    }

    // Renders or downloads one exact HTTP document and converts it into normalized knowledge text.
    private async Task<WebPage> CrawlHttpDocumentAsync(KnowledgeSource source, Uri documentUri, string existingTitle, WebPage document, CancellationToken cancellationToken)
    {
        await DelayBeforeRequestAsync(cancellationToken);
        if (ShouldDownloadBeforeRendering(documentUri, source.SourceType))
        {
            return await CrawlDownloadableHttpDocumentAsync(source, documentUri, existingTitle, document, cancellationToken);
        }

        if (_pageRenderer is not null)
        {
            try
            {
                var rendered = await _pageRenderer.RenderAsync(documentUri.AbsoluteUri, cancellationToken);
                var renderedText = _knowledgeTextCleaner.Clean(rendered.Text);
                return renderedText.Length >= _options.MinimumTextLengthToStore
                    ? BuildContentDocument(source, documentUri.AbsoluteUri, GuessTitle(documentUri.AbsoluteUri, rendered.FirstHeadingTitle, rendered.Title), renderedText, 200)
                    : BuildStatusDocument(source, documentUri.AbsoluteUri, GuessTitle(documentUri.AbsoluteUri, rendered.FirstHeadingTitle, rendered.Title), "empty_content", "İçerik Yok", "Sayfada kullanılabilir metinsel içerik bulunamadı.", 200);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Renderer failures should not make an otherwise readable static HTML document unusable.
            }
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, documentUri.AbsoluteUri);
        request.Headers.Referrer = new Uri($"{documentUri.Scheme}://{documentUri.Host}");
        using var timeout = CreateDocumentTimeout(cancellationToken);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        var contentType = response.Content?.Headers.ContentType?.MediaType ?? string.Empty;
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            return BuildStatusDocument(source, documentUri.AbsoluteUri, GuessTitle(documentUri.AbsoluteUri, existingTitle), $"http{statusCode}", $"http{statusCode}", $"HTTP {statusCode} {response.ReasonPhrase}", statusCode);
        }

        if (!IsHtmlDocument(contentType))
        {
            return BuildStatusDocument(source, documentUri.AbsoluteUri, GuessTitle(documentUri.AbsoluteUri, existingTitle), "unsupported_content", "Desteklenmeyen İçerik", "Belge HTML/XML metni olarak okunamadı.", (int)response.StatusCode);
        }

        var body = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(timeout.Token);

        var text = _knowledgeTextCleaner.Clean(_textExtractor.ExtractText(body));
        return text.Length >= _options.MinimumTextLengthToStore
            ? BuildContentDocument(source, documentUri.AbsoluteUri, GuessTitle(documentUri.AbsoluteUri, ExtractFirstHeadingTitle(body), ExtractHtmlTitle(body), existingTitle), text, (int)response.StatusCode)
            : BuildStatusDocument(source, documentUri.AbsoluteUri, GuessTitle(documentUri.AbsoluteUri, ExtractFirstHeadingTitle(body), ExtractHtmlTitle(body), existingTitle), "empty_content", "İçerik Yok", "Sayfada kullanılabilir metinsel içerik bulunamadı.", (int)response.StatusCode);
    }

    // Keeps full-source crawling inside the per-document HTTP timeout by preferring static HTML over browser rendering.
    private static bool ShouldRenderBeforeStaticHttp(string url)
    {
        return false;
    }

    // Downloads a non-HTML web document and parses it with the same parser used by local-file imports.
    private async Task<WebPage> CrawlDownloadableHttpDocumentAsync(KnowledgeSource source, Uri documentUri, string existingTitle, WebPage document, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, documentUri.AbsoluteUri);
        request.Headers.Referrer = new Uri($"{documentUri.Scheme}://{documentUri.Host}");
        using var timeout = CreateDocumentTimeout(cancellationToken);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            return BuildStatusDocument(source, documentUri.AbsoluteUri, GuessTitle(documentUri.AbsoluteUri, existingTitle), $"http{statusCode}", $"http{statusCode}", $"HTTP {statusCode} {response.ReasonPhrase}", statusCode);
        }

        var fileName = BuildDownloadFileName(documentUri, response.Content?.Headers.ContentType?.MediaType, existingTitle);
        var rawBytes = response.Content is null
            ? Array.Empty<byte>()
            : await response.Content.ReadAsByteArrayAsync(timeout.Token);
        await using var stream = new MemoryStream(rawBytes);
        var parsed = await _knowledgeFileParser.ParseAsync(stream, fileName, cancellationToken);
        var title = GuessTitle(documentUri.AbsoluteUri, existingTitle, Path.GetFileNameWithoutExtension(parsed.FileName));
        if (parsed.Content.Length < _options.MinimumTextLengthToStore)
        {
            var message = parsed.OcrStatus == "ocr_not_available"
                ? "Belgeden yeterli metin çıkarılamadı; PDF metin katmanı içermiyor olabilir."
                : "Belgeden kullanılabilir metinsel içerik alınamadı.";
            return BuildStatusDocument(source, documentUri.AbsoluteUri, title, "empty_content", "İçerik Yok", message, (int)response.StatusCode);
        }

        var page = BuildContentDocument(source, documentUri.AbsoluteUri, title, parsed.Content, (int)response.StatusCode);
        page.OriginalFileName = parsed.FileName;
        page.FileExtension = parsed.Extension;
        page.FileSizeBytes = parsed.FileSizeBytes;
        page.FileHash = parsed.FileHash;
        page.ParseStatus = parsed.ParseStatus;
        page.OcrStatus = parsed.OcrStatus;
        page.Origin = "manual_web_link";
        await ArchiveDownloadedDocumentAsync(source, document, parsed.FileName, rawBytes, page, cancellationToken);
        return page;
    }

    // Archives a downloaded web-link document under the same GUID-based data hierarchy used by local files.
    private async Task ArchiveDownloadedDocumentAsync(KnowledgeSource source, WebPage document, string fileName, byte[] rawBytes, WebPage page, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.TenantGuid)
            || string.IsNullOrWhiteSpace(source.TenantKnowledgeGuid)
            || string.IsNullOrWhiteSpace(source.KnowledgeSourceGuid))
        {
            return;
        }

        var sourceFolderGuid = string.IsNullOrWhiteSpace(document.SourceFolderGuid) ? Guid.NewGuid().ToString("D") : document.SourceFolderGuid;
        var folderDocumentGuid = string.IsNullOrWhiteSpace(document.FolderDocumentGuid) ? Guid.NewGuid().ToString("D") : document.FolderDocumentGuid;
        var storageDirectory = string.IsNullOrWhiteSpace(document.StorageDirectory)
            ? KnowledgeRawFileStorage.BuildStorageDirectory(_dataRoot, source, sourceFolderGuid, folderDocumentGuid)
            : document.StorageDirectory;
        var storedFileName = await KnowledgeRawFileStorage.ReplaceRawFileAsync(storageDirectory, fileName, rawBytes, cancellationToken);

        page.TenantGuid = source.TenantGuid;
        page.TenantKnowledgeGuid = source.TenantKnowledgeGuid;
        page.KnowledgeSourceGuid = source.KnowledgeSourceGuid;
        page.SourceFolderGuid = sourceFolderGuid;
        page.FolderDocumentGuid = folderDocumentGuid;
        page.NormalizedFolderPath = "/";
        page.NormalizedRelativePath = storedFileName;
        page.StorageDirectory = storageDirectory;
        page.StoredFileName = storedFileName;
    }

    // Executes this component behavior as part of the Oyako application flow.
    private async Task<WebPage> BuildFileDocumentAsync(KnowledgeSource source, Uri uri, CancellationToken cancellationToken)
    {
        if (!File.Exists(uri.LocalPath))
        {
            return BuildStatusDocument(source, uri.AbsoluteUri, source.Name, "not_found", "Dosya Bulunamadı", "Yerel dosya bulunamadı.", null);
        }

        var text = await File.ReadAllTextAsync(uri.LocalPath, cancellationToken);
        var cleaned = _knowledgeTextCleaner.Clean(text);
        return cleaned.Length == 0
            ? BuildStatusDocument(source, uri.AbsoluteUri, Path.GetFileName(uri.LocalPath), "empty_content", "İçerik Yok", "Dosyada kullanılabilir metinsel içerik bulunamadı.", null)
            : BuildContentDocument(source, uri.AbsoluteUri, Path.GetFileName(uri.LocalPath), cleaned, null);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private IReadOnlyCollection<string> ExtractDiscoveryLinks(string content, string contentType)
    {
        var links = new List<string>();
        if (IsHtmlDocument(contentType) || string.IsNullOrWhiteSpace(contentType))
        {
            links.AddRange(_textExtractor.ExtractLinks(content));
            links.AddRange(EncodedHrefPattern.Matches(content).Select(match => match.Groups["url"].Value));
            links.AddRange(QuotedPathPattern.Matches(content).Select(match => match.Groups["url"].Value));
        }

        links.AddRange(SitemapLocPattern.Matches(content).Select(match => match.Groups["url"].Value));
        return links;
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static IReadOnlyCollection<string> BuildSeedUrls(Uri siteRootUri)
    {
        var root = $"{siteRootUri.Scheme}://{siteRootUri.Host}";
        return new[]
        {
            $"{root}/sitemap.xml",
            root
        };
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static bool IsHtmlDocument(string contentType)
    {
        return string.IsNullOrWhiteSpace(contentType)
            || contentType.Contains("html", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("xml", StringComparison.OrdinalIgnoreCase);
    }

    // Determines whether a URL represents a document that browser engines commonly treat as a download.
    private static bool ShouldDownloadBeforeRendering(Uri documentUri, string sourceType)
    {
        var extension = Path.GetExtension(documentUri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        var supportedExtensions = string.Equals(sourceType, KnowledgeSourceTypes.WebLinks, StringComparison.OrdinalIgnoreCase)
            ? ParserDocumentExtensions
            : DownloadableDocumentExtensions;

        return supportedExtensions.Contains(extension);
    }

    // Builds a parser-friendly file name when the URL or content type identifies a downloadable document.
    private static string BuildDownloadFileName(Uri documentUri, string? contentType, string existingTitle)
    {
        var fileName = Path.GetFileName(documentUri.AbsolutePath);
        if (!string.IsNullOrWhiteSpace(fileName) && ParserDocumentExtensions.Contains(Path.GetExtension(fileName)))
        {
            return fileName;
        }

        var extension = contentType?.ToLowerInvariant() switch
        {
            string value when value.Contains("pdf", StringComparison.Ordinal) => ".pdf",
            string value when value.Contains("wordprocessingml", StringComparison.Ordinal) => ".docx",
            string value when value.Contains("presentationml", StringComparison.Ordinal) => ".pptx",
            string value when value.Contains("rtf", StringComparison.Ordinal) => ".rtf",
            string value when value.Contains("epub", StringComparison.Ordinal) => ".epub",
            string value when value.Contains("markdown", StringComparison.Ordinal) => ".md",
            string value when value.Contains("html", StringComparison.Ordinal) => ".html",
            string value when value.Contains("text/plain", StringComparison.Ordinal) => ".txt",
            _ => Path.GetExtension(fileName)
        };

        if (string.IsNullOrWhiteSpace(extension) || !ParserDocumentExtensions.Contains(extension))
        {
            extension = ".txt";
        }

        var safeTitle = string.Join("-", existingTitle.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        return $"{(string.IsNullOrWhiteSpace(safeTitle) ? "web-document" : safeTitle)}{extension}";
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static bool IsUtilityUrl(string url)
    {
        var path = new Uri(url).AbsolutePath;
        return path.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/sitemap.xml", StringComparison.OrdinalIgnoreCase);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private async Task DelayBeforeRequestAsync(CancellationToken cancellationToken)
    {
        var min = Math.Max(0, _options.MinimumRequestDelayMilliseconds);
        var max = Math.Max(min, _options.MaximumRequestDelayMilliseconds);
        if (max == 0)
        {
            return;
        }

        await Task.Delay(Random.Shared.Next(min, max + 1), cancellationToken);
    }

    // Creates a hard per-document timeout so one slow web page cannot block the whole crawl.
    private CancellationTokenSource CreateDocumentTimeout(CancellationToken cancellationToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.RequestTimeoutSeconds)));
        return timeout;
    }

    // Executes this component behavior as part of the Oyako application flow.
    private WebPage BuildContentDocument(KnowledgeSource source, string url, string title, string text, int? httpStatusCode)
    {
        var now = DateTime.UtcNow;
        var preview = _knowledgeTextCleaner.BuildPreview(text, title, 260);
        return new WebPage
        {
            SourceId = source.Id == 0 ? null : source.Id,
            SourceName = source.Name,
            WebSourceUrl = url,
            WebTitle = title,
            WebContent = text,
            ContentPreview = preview,
            ContentHash = CalculateHash(text),
            IsEnabled = true,
            IsArchived = false,
            StatusCode = "ok",
            StatusLabel = "Tamam",
            StatusMessage = "Belge kullanılabilir.",
            HttpStatusCode = httpStatusCode,
            PreviewStatus = "deterministic",
            PreviewGeneratedAtUtc = now,
            LastCheckedAtUtc = now
        };
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static WebPage BuildStatusDocument(KnowledgeSource source, string url, string title, string statusCode, string statusLabel, string statusMessage, int? httpStatusCode)
    {
        var now = DateTime.UtcNow;
        return new WebPage
        {
            SourceId = source.Id == 0 ? null : source.Id,
            SourceName = source.Name,
            WebSourceUrl = url,
            WebTitle = title,
            WebContent = string.Empty,
            ContentPreview = statusMessage,
            ContentHash = CalculateHash($"{url}|{statusCode}|{statusMessage}"),
            IsEnabled = true,
            IsArchived = false,
            StatusCode = statusCode,
            StatusLabel = statusLabel,
            StatusMessage = statusMessage,
            HttpStatusCode = httpStatusCode,
            PreviewStatus = "status",
            PreviewGeneratedAtUtc = now,
            LastCheckedAtUtc = now
        };
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string GuessTitle(string url, params string?[] titleCandidates)
    {
        foreach (var titleCandidate in titleCandidates)
        {
            if (!string.IsNullOrWhiteSpace(titleCandidate))
            {
                return titleCandidate.Trim();
            }
        }

        var uri = new Uri(url);
        if (uri.IsFile)
        {
            return Path.GetFileName(uri.LocalPath);
        }

        var path = uri.AbsolutePath.Trim('/');
        if (string.IsNullOrWhiteSpace(path))
        {
            return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
        }

        return string.Join(
            " ",
            path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Last()
                .Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    // Extracts the first h1-h6 heading from raw HTML to mirror the title users see on the rendered page.
    private static string? ExtractFirstHeadingTitle(string html)
    {
        return ExtractHtmlTextCandidate(FirstHeadingPattern, html);
    }

    // Extracts the document title from raw HTML as a secondary title candidate after headings.
    private static string? ExtractHtmlTitle(string html)
    {
        return ExtractHtmlTextCandidate(HtmlTitlePattern, html);
    }

    // Normalizes one regex text candidate by stripping tags, decoding entities, and compacting whitespace.
    private static string? ExtractHtmlTextCandidate(Regex pattern, string html)
    {
        var match = pattern.Match(html);
        if (!match.Success)
        {
            return null;
        }

        var withoutTags = HtmlTagPattern.Replace(match.Groups["text"].Value, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        var normalized = string.Join(" ", decoded.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized.Trim();
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string CalculateHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }

    // Defines the immutable CrawlCandidate data shape exchanged between Oyako components.
    private sealed record CrawlCandidate(string Url, int Depth, bool IsUtilityDocument);
}
