// Codex developer note: Explains the purpose and flow of webapi-oyako/Program.cs for maintainers.
using Dapper;
using webapi_oyako.Domain.Services;
using webapi_oyako.Infrastructure;
using webapi_oyako.Infrastructure.Configuration;
using webapi_oyako.Infrastructure.Crawling;
using webapi_oyako.Infrastructure.Data;
using webapi_oyako.Presentation;

if (args.Any(arg => string.Equals(arg, "--install-playwright", StringComparison.OrdinalIgnoreCase)))
{
    Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", AppContext.BaseDirectory);
    return Microsoft.Playwright.Program.Main(["install", "chromium"]);
}

if (args.Any(arg => string.Equals(arg, "--install-playwright-deps", StringComparison.OrdinalIgnoreCase)))
{
    Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", AppContext.BaseDirectory);
    return Microsoft.Playwright.Program.Main(["install-deps", "chromium"]);
}

if (args.Any(arg => string.Equals(arg, "--verify-playwright", StringComparison.OrdinalIgnoreCase)))
{
    Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", AppContext.BaseDirectory);

    using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
    await using var browser = await playwright.Chromium.LaunchAsync(new Microsoft.Playwright.BrowserTypeLaunchOptions
    {
        Headless = true,
        Timeout = 60000,
        Args = new[]
        {
            "--disable-dev-shm-usage",
            "--disable-background-networking",
            "--disable-background-timer-throttling"
        }
    });

    var page = await browser.NewPageAsync();
    await page.SetContentAsync(
        "<html><head><title>oyako-browser-health</title></head><body>oyako-browser-ok</body></html>",
        new Microsoft.Playwright.PageSetContentOptions
        {
            WaitUntil = Microsoft.Playwright.WaitUntilState.Load,
            Timeout = 60000
        });
    var text = await page.InnerTextAsync("body", new Microsoft.Playwright.PageInnerTextOptions
    {
        Timeout = 60000
    });
    await page.CloseAsync();

    if (!text.Contains("oyako-browser-ok", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("Playwright Chromium verification rendered an unexpected payload.");
        return 95;
    }

    Console.WriteLine("OK: Playwright Chromium verified.");
    return 0;
}

EnvFileLoader.LoadMany(["oyako.env", "azure-cloud.env", "ollama-cloud.env"], Directory.GetCurrentDirectory());
EnvFileLoader.LoadTenant(Directory.GetCurrentDirectory());
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
var isDockerRuntime = string.Equals(Environment.GetEnvironmentVariable("OYAKO_DOCKER"), "1", StringComparison.OrdinalIgnoreCase)
    || string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
var isAzureWebAppRuntime = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
var isDeployedRuntime = isDockerRuntime || isAzureWebAppRuntime;
// Resolves the repository-local HTTPS certificate so local development does not depend on the OS certificate store.
var portableHttpsCertificatePath = isDeployedRuntime
    ? null
    : PortableHttpsCertificateBootstrap.EnsureCertificate(builder.Environment.ContentRootPath);

if (!isDeployedRuntime)
{
    // Binds the local API ports directly and loads HTTPS from the repository-local PFX file.
    builder.WebHost.ConfigureKestrel(options =>
    {
        // Registers the portable HTTP endpoint used by the web app and test suite.
        options.ListenLocalhost(5000);
        // Registers the portable HTTPS endpoint without reading from the operating system key-chain or certificate store.
        options.ListenLocalhost(5001, listenOptions => listenOptions.UseHttps(portableHttpsCertificatePath!));
    });
}

// Registers or maps application behavior into the runtime pipeline.
builder.Services.AddEndpointsApiExplorer();
// Registers or maps application behavior into the runtime pipeline.
builder.Services.AddOpenApi();

// Registers or maps application behavior into the runtime pipeline.
builder.Services.AddOyakoServices(builder.Configuration);

// Registers or maps application behavior into the runtime pipeline.
builder.Services.AddCors(options =>
{
    // Registers or maps application behavior into the runtime pipeline.
    options.AddPolicy(
        "webapp",
        policy =>
            policy.WithOrigins(
                    "http://localhost:3000",
                    "https://localhost:3000",
                    "http://localhost:5173",
                    "https://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod());
});

var app = builder.Build();

// Guards the following branch so the workflow handles this condition deliberately.
if (app.Environment.IsDevelopment())
{
    // Registers or maps application behavior into the runtime pipeline.
    app.MapOpenApi();
}

if (app.Environment.IsDevelopment())
{
    app.UseCors("webapp");
}

// Guards the following branch so the workflow handles this condition deliberately.
if (!app.Environment.IsDevelopment() && !isDeployedRuntime)
{
    app.UseHttpsRedirection();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "oyako"
}));

app.MapGet("/health/browser", async (IPageRenderer pageRenderer, CancellationToken cancellationToken) =>
{
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeout.CancelAfter(TimeSpan.FromSeconds(15));

    try
    {
        var rendered = await pageRenderer.RenderAsync(
            "data:text/html,<html><head><title>Oyako Browser Health</title></head><body>oyako-browser-ok</body></html>",
            timeout.Token);
        return rendered.Text.Contains("oyako-browser-ok", StringComparison.OrdinalIgnoreCase)
            ? Results.Ok(new
            {
                status = "ok",
                service = "oyako",
                browser = "chromium"
            })
            : Results.Problem("Chromium rendered an unexpected health payload.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
    {
        return Results.Problem(
            $"Chromium health check failed: {ex.GetType().Name}: {ex.Message}",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

// Registers or maps application behavior into the runtime pipeline.
app.MapOyakoEndpoints();
app.Map("/api/{**catchAll}", () => Results.NotFound(new
{
    status = "not_found",
    service = "oyako"
}));
app.MapFallbackToFile("index.html");

// Creates a disposable resource scoped to this operation.
using var appScope = app.Services.CreateScope();
var dbInitializer = appScope.ServiceProvider.GetRequiredService<SqliteDbInitializer>();
// Awaits the asynchronous operation so the workflow continues only after the dependency completes.
await dbInitializer.InitializeAsync(app.Lifetime.ApplicationStopping);
var aiConfigurationService = appScope.ServiceProvider.GetRequiredService<IAiConfigurationService>();
// Awaits the asynchronous operation so the workflow continues only after the dependency completes.
await aiConfigurationService.InitializeAsync(app.Lifetime.ApplicationStopping);
var systemInstructionCache = appScope.ServiceProvider.GetRequiredService<ISystemInstructionCache>();
// Awaits the asynchronous operation so the workflow continues only after the dependency completes.
await systemInstructionCache.InitializeAsync(app.Lifetime.ApplicationStopping);
var readyQuestionService = appScope.ServiceProvider.GetRequiredService<IReadyQuestionService>();
readyQuestionService.QueueRefreshFromKnowledge();

// Awaits the asynchronous operation so the workflow continues only after the dependency completes.
await app.RunAsync();
return 0;
