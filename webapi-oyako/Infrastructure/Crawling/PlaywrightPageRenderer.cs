// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Crawling/PlaywrightPageRenderer.cs for maintainers.
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using webapi_oyako.Infrastructure.Configuration;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Infrastructure.Crawling;

// Implements the PlaywrightPageRenderer component and its responsibilities in the Oyako codebase.
public sealed class PlaywrightPageRenderer : IPageRenderer, IAsyncDisposable
{
    // Stores state or a dependency required by the surrounding component.
    private readonly CrawlerOptions _options;
    // Stores state or a dependency required by the surrounding component.
    private readonly SemaphoreSlim _gate = new(1, 1);
    // Stores state or a dependency required by the surrounding component.
    private IPlaywright? _playwright;
    // Stores state or a dependency required by the surrounding component.
    private IBrowser? _browser;
    // Stores state or a dependency required by the surrounding component.
    private IBrowserContext? _context;

    // Creates a new instance and captures the dependencies needed by this component.
    public PlaywrightPageRenderer(IOptions<CrawlerOptions> options)
    {
        _options = options.Value;
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<RenderedPage> RenderAsync(string url, CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var timeout = BuildRenderTimeout();
            var deadline = DateTimeOffset.UtcNow.Add(timeout);
            var context = await GetContextAsync(deadline, cancellationToken);
            var page = await context.NewPageAsync();
            page.SetDefaultNavigationTimeout((float)timeout.TotalMilliseconds);
            page.SetDefaultTimeout((float)timeout.TotalMilliseconds);
            try
            {
                // Creates the object needed for the next step of the workflow.
                var response = await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = (float)timeout.TotalMilliseconds
                });

                // Guards the following branch so the workflow handles this condition deliberately.
                if (response is not null && response.Status >= 400)
                {
                    // Stops the current workflow with an explicit failure that upstream handlers can report.
                    throw new InvalidOperationException($"HTTP {response.Status} {response.StatusText}");
                }

                // Guards the following branch so the workflow handles this condition deliberately.
                if (_options.RenderExtraWaitMilliseconds > 0)
                {
                    var extraWait = Math.Min(_options.RenderExtraWaitMilliseconds, Math.Max(0, RemainingMilliseconds(deadline)));
                    // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                    if (extraWait > 0)
                    {
                        await page.WaitForTimeoutAsync(extraWait);
                    }
                }

                var title = await RunWithinRemainingAsync(
                    () => page.EvaluateAsync<string?>("() => document.title || document.querySelector('h1')?.innerText || null"),
                    deadline,
                    cancellationToken);
                var firstHeadingTitle = await RunWithinRemainingAsync(
                    () => page.EvaluateAsync<string?>("() => Array.from(document.querySelectorAll('h1,h2,h3,h4,h5,h6')).map(h => (h.innerText || h.textContent || '').replace(/\\s+/g, ' ').trim()).find(Boolean) || null"),
                    deadline,
                    cancellationToken);
                var text = await RunWithinRemainingAsync(
                    () => page.EvaluateAsync<string>("() => document.body?.innerText || ''"),
                    deadline,
                    cancellationToken);
                var links = await RunWithinRemainingAsync(
                    () => page.EvaluateAsync<string[]>("() => Array.from(document.querySelectorAll('a[href]')).map(a => a.href).filter(Boolean)"),
                    deadline,
                    cancellationToken);

                // Returns the computed result to the caller and completes this branch of the workflow.
                return new RenderedPage(url, title, firstHeadingTitle, NormalizeText(text), links);
            }
            catch (PlaywrightException ex) when (ex.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            {
                throw new System.TimeoutException($"Sayfa {timeout.TotalSeconds:0} saniye içinde render edilemedi: {url}", ex);
            }
            finally
            {
                // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                await page.CloseAsync();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    // Executes this component behavior as part of the Oyako application flow.
    private async Task<IBrowserContext> GetContextAsync(DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        // Guards the following branch so the workflow handles this condition deliberately.
        if (_context is not null)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return _context;
        }

        _playwright = await RunWithinRemainingAsync(
            Playwright.CreateAsync,
            deadline,
            cancellationToken);
        // Creates the object needed for the next step of the workflow.
        _browser = await RunWithinRemainingAsync(
            () => _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Timeout = Math.Max(1, RemainingMilliseconds(deadline)),
                Args = new[]
                {
                    "--disable-dev-shm-usage",
                    "--disable-background-networking",
                    "--disable-background-timer-throttling"
                }
            }),
            deadline,
            cancellationToken);

        // Creates the object needed for the next step of the workflow.
        _context = await RunWithinRemainingAsync(
            () => _browser.NewContextAsync(new BrowserNewContextOptions
            {
                Locale = "tr-TR",
                TimezoneId = "Europe/Istanbul",
                UserAgent = _options.UserAgent,
                // Creates the object needed for the next step of the workflow.
                ViewportSize = new ViewportSize { Width = 1440, Height = 1000 },
                // Creates the object needed for the next step of the workflow.
                ExtraHTTPHeaders = new Dictionary<string, string>
                {
                    ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
                    ["Accept-Language"] = "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7"
                },
                ServiceWorkers = ServiceWorkerPolicy.Block
            }),
            deadline,
            cancellationToken);

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await RunWithinRemainingAsync(
            () => _context.RouteAsync("**/*", async route =>
            {
                var request = route.Request;
                // Creates the object needed for the next step of the workflow.
                var resourceType = request.ResourceType;
                var block = resourceType is "image" or "media" or "font" or "websocket" or "eventsource";

                // Guards the following branch so the workflow handles this condition deliberately.
                if (block)
                {
                    // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                    await route.AbortAsync();
                    // Returns the computed result to the caller and completes this branch of the workflow.
                    return;
                }

                // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                await route.ContinueAsync();
            }),
            deadline,
            cancellationToken);

        // Returns the computed result to the caller and completes this branch of the workflow.
        return _context;
    }

    // Executes a Playwright operation without allowing it to consume more than the current page budget.
    private static async Task<T> RunWithinRemainingAsync<T>(Func<Task<T>> action, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        var remaining = deadline - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            throw new System.TimeoutException("Sayfa render zaman bütçesi doldu.");
        }

        return await action().WaitAsync(remaining, cancellationToken);
    }

    // Executes a Playwright setup action within the current render budget.
    private static async Task RunWithinRemainingAsync(Func<Task> action, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        var remaining = deadline - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            throw new System.TimeoutException("Sayfa render zaman bütçesi doldu.");
        }

        await action().WaitAsync(remaining, cancellationToken);
    }

    // Calculates the hard upper bound for one rendered document.
    private TimeSpan BuildRenderTimeout()
    {
        return TimeSpan.FromSeconds(Math.Max(1, _options.RenderTimeoutSeconds));
    }

    // Calculates how much of the current render budget remains.
    private static int RemainingMilliseconds(DateTimeOffset deadline)
    {
        return (int)Math.Floor((deadline - DateTimeOffset.UtcNow).TotalMilliseconds);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string NormalizeText(string text)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return string.Join(
            "\n",
            text
                .Replace("\r", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => string.Join(" ", line.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim())
                .Where(line => line.Length > 0));
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async ValueTask DisposeAsync()
    {
        // Guards the following branch so the workflow handles this condition deliberately.
        if (_context is not null)
        {
            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await _context.CloseAsync();
        }

        // Guards the following branch so the workflow handles this condition deliberately.
        if (_browser is not null)
        {
            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
        _gate.Dispose();
    }
}
