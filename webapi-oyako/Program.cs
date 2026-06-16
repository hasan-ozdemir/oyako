// Codex developer note: Explains the purpose and flow of webapi-oyako/Program.cs for maintainers.
using Dapper;
using webapi_oyako.Domain.Services;
using webapi_oyako.Infrastructure;
using webapi_oyako.Infrastructure.Configuration;
using webapi_oyako.Infrastructure.Data;
using webapi_oyako.Presentation;

EnvFileLoader.LoadMany(["azure-cloud.env", "ollama-cloud.env"], Directory.GetCurrentDirectory());
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
var isDockerRuntime = string.Equals(Environment.GetEnvironmentVariable("OYAKO_DOCKER"), "1", StringComparison.OrdinalIgnoreCase)
    || string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
// Resolves the repository-local HTTPS certificate so local development does not depend on the OS certificate store.
var portableHttpsCertificatePath = isDockerRuntime
    ? null
    : PortableHttpsCertificateBootstrap.EnsureCertificate(builder.Environment.ContentRootPath);

// Binds the local API ports directly and loads HTTPS from the repository-local PFX file.
builder.WebHost.ConfigureKestrel(options =>
{
    if (isDockerRuntime)
    {
        // Registers the Docker HTTP endpoint on all container interfaces.
        options.ListenAnyIP(5000);
    }
    else
    {
        // Registers the portable HTTP endpoint used by the web app and test suite.
        options.ListenLocalhost(5000);
        // Registers the portable HTTPS endpoint without reading from the operating system key-chain or certificate store.
        options.ListenLocalhost(5001, listenOptions => listenOptions.UseHttps(portableHttpsCertificatePath!));
    }
});

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

app.UseCors("webapp");
// Guards the following branch so the workflow handles this condition deliberately.
if (!app.Environment.IsDevelopment() && !isDockerRuntime)
{
    app.UseHttpsRedirection();
}
// Registers or maps application behavior into the runtime pipeline.
app.MapOyakoEndpoints();

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
