// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/DependencyInjection.cs for maintainers.
using Microsoft.Extensions.Options;
using webapi_oyako.Application.Services;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Domain.Services;
using webapi_oyako.Infrastructure.Background;
using webapi_oyako.Infrastructure.Configuration;
using webapi_oyako.Infrastructure.Crawling;
using webapi_oyako.Infrastructure.Data;
using webapi_oyako.Infrastructure.Llm;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Infrastructure;

// Implements the DependencyInjection component and its responsibilities in the Oyako codebase.
public static class DependencyInjection
{
    // Executes this component behavior as part of the Oyako application flow.
    public static IServiceCollection AddOyakoServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Registers or maps application behavior into the runtime pipeline.
        services.Configure<SqliteOptions>(configuration.GetSection(SqliteOptions.SectionName));
        // Registers or maps application behavior into the runtime pipeline.
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        // Registers or maps application behavior into the runtime pipeline.
        services.Configure<AzureAiOptions>(configuration.GetSection(AzureAiOptions.SectionName));
        // Registers or maps application behavior into the runtime pipeline.
        services.Configure<CrawlerOptions>(configuration.GetSection(CrawlerOptions.SectionName));
        // Registers or maps application behavior into the runtime pipeline.
        services.Configure<OllamaLocalOptions>(configuration.GetSection(OllamaLocalOptions.SectionName));
        // Registers or maps application behavior into the runtime pipeline.
        services.Configure<OllamaCloudOptions>(configuration.GetSection(OllamaCloudOptions.SectionName));

        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<SqliteDbInitializer>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<ITextExtractor, RenderedTextExtractor>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IKnowledgeTextCleaner, KnowledgeTextCleaner>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IPageRenderer, PlaywrightPageRenderer>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IWebPageRepository, WebPageRepository>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<ICrawlRunRepository, CrawlRunRepository>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<ISystemInstructionCacheRepository, SystemInstructionCacheRepository>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IReadyQuestionRepository, ReadyQuestionRepository>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IAiSettingsRepository, AiSettingsRepository>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IQnaExperienceSettingsRepository, QnaExperienceSettingsRepository>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IKnowledgeStoreMaintenanceRepository, KnowledgeStoreMaintenanceRepository>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IAiConfigurationService, AiConfigurationService>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IQnaExperienceSettingsService, QnaExperienceSettingsService>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IKnowledgeOperationGate, KnowledgeOperationGate>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IChatPromptBuilder, ChatPromptBuilder>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IKnowledgeFileParser, KnowledgeFileParser>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IKnowledgeUploadSettingsService, KnowledgeUploadSettingsService>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IKnowledgeFileImportService, KnowledgeFileImportService>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IKnowledgeActivationService, KnowledgeActivationService>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IKnowledgeActivationSwitchService, KnowledgeActivationSwitchService>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<ILocalKnowledgeRebuildService, LocalKnowledgeRebuildService>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<ISystemInstructionCache, SystemInstructionCache>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IAnswerActionLinkifier, AnswerActionLinkifier>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IAnswerHtmlSanitizer, AnswerHtmlSanitizer>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IRuntimeStatusService, RuntimeStatusService>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IReadyQuestionService, ReadyQuestionService>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddScoped<IKnowledgeRedownloadService, KnowledgeRedownloadService>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddScoped<IChatService, ChatService>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddScoped<IKnowledgeSourceRefreshService, KnowledgeSourceRefreshService>();

        // Registers or maps application behavior into the runtime pipeline.
        services.AddHttpClient<IWebCrawler, HtmlCrawler>((serviceProvider, httpClient) =>
        {
            var crawlerOptions = serviceProvider.GetRequiredService<IOptions<CrawlerOptions>>().Value;
            httpClient.Timeout = TimeSpan.FromSeconds(crawlerOptions.RequestTimeoutSeconds);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(crawlerOptions.UserAgent);
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,text/plain;q=0.8,*/*;q=0.7");
            httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");
        });

        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<AiProviderRouter>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IAiChatClient>(serviceProvider => serviceProvider.GetRequiredService<AiProviderRouter>());
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IAiProviderCatalog>(serviceProvider => serviceProvider.GetRequiredService<AiProviderRouter>());

        // Registers or maps application behavior into the runtime pipeline.
        services.AddHttpClient<OllamaLocalClient>((serviceProvider, httpClient) =>
        {
            var ollamaOptions = serviceProvider.GetRequiredService<IOptions<OllamaLocalOptions>>().Value;
            // Creates the object needed for the next step of the workflow.
            httpClient.BaseAddress = new Uri(ollamaOptions.BaseUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(ollamaOptions.TimeoutSeconds);
        });
        // Registers or maps application behavior into the runtime pipeline.
        services.AddTransient<IAiProviderClient>(serviceProvider => serviceProvider.GetRequiredService<OllamaLocalClient>());

        // Registers or maps application behavior into the runtime pipeline.
        services.AddHttpClient<OllamaCloudClient>((serviceProvider, httpClient) =>
        {
            var ollamaOptions = serviceProvider.GetRequiredService<IOptions<OllamaCloudOptions>>().Value;
            // Creates the object needed for the next step of the workflow.
            httpClient.BaseAddress = new Uri(ollamaOptions.BaseUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(ollamaOptions.TimeoutSeconds);
        });
        // Registers or maps application behavior into the runtime pipeline.
        services.AddTransient<IAiProviderClient>(serviceProvider => serviceProvider.GetRequiredService<OllamaCloudClient>());

        // Registers or maps application behavior into the runtime pipeline.
        services.AddHttpClient<AzureAiClient>((serviceProvider, httpClient) =>
        {
            var azureOptions = serviceProvider.GetRequiredService<IOptions<AzureAiOptions>>().Value;
            // Creates the object needed for the next step of the workflow.
            httpClient.BaseAddress = new Uri(azureOptions.Endpoint);
            httpClient.Timeout = TimeSpan.FromSeconds(azureOptions.TimeoutSeconds);
        });
        // Registers or maps application behavior into the runtime pipeline.
        services.AddTransient<IAiProviderClient>(serviceProvider => serviceProvider.GetRequiredService<AzureAiClient>());

        var crawlerOptions = configuration.GetSection(CrawlerOptions.SectionName).Get<CrawlerOptions>() ?? new CrawlerOptions();
        if (crawlerOptions.LocalKnowledgeRebuildOnStartupEnabled)
        {
            // Registers or maps application behavior into the runtime pipeline.
            services.AddHostedService<LocalKnowledgeRebuildHostedService>();
        }
        // Registers or maps application behavior into the runtime pipeline.
        services.AddHostedService<KnowledgeSourceRefreshHostedService>();

        // Returns the computed result to the caller and completes this branch of the workflow.
        return services;
    }
}
