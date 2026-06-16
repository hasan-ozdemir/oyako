// Codex developer note: Explains the purpose and flow of webapi-oyako/webapi-oyako.Tests/AiConfigurationServiceTests.cs for maintainers.
using Microsoft.Extensions.Options;
using webapi_oyako.Application.Services;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Infrastructure.Configuration;
using Xunit;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Tests;

// Verifies persisted AI provider and model selection behavior.
public sealed class AiConfigurationServiceTests
{
    [Fact]
    // Verifies empty repositories seed the new Ollama Cloud default.
    public async Task GetAsync_WhenRepositoryIsEmpty_SeedsConfiguredDefaults()
    {
        var repository = new InMemoryAiSettingsRepository(null);
        var service = CreateService(repository);

        var settings = await service.GetAsync(CancellationToken.None);

        Assert.Equal("ollama-cloud", settings.ActiveProvider);
        Assert.Equal("DeepSeek-V4-Flash", settings.AzureModel);
        Assert.Equal("gemma4:12b", settings.OllamaLocalModel);
        Assert.Equal("minimax-m3:cloud", settings.OllamaCloudModel);
        Assert.Equal("minimax-m3:cloud", settings.ActiveModel);
        Assert.Equal(settings, repository.Saved);
    }

    [Fact]
    // Verifies each supported provider persists its own selected model.
    public async Task UpdateAsync_PersistsSelectedProviderAndModel()
    {
        var repository = new InMemoryAiSettingsRepository(null);
        var service = CreateService(repository);

        var localSettings = await service.UpdateAsync("ollama-local", "gemma4:12b", CancellationToken.None);
        var cloudSettings = await service.UpdateAsync("ollama-cloud", "minimax-m3:cloud", CancellationToken.None);

        Assert.Equal("ollama-local", localSettings.ActiveProvider);
        Assert.Equal("gemma4:12b", await service.GetSelectedModelAsync("ollama-local", CancellationToken.None));
        Assert.Equal("ollama-cloud", cloudSettings.ActiveProvider);
        Assert.Equal("minimax-m3:cloud", cloudSettings.ActiveModel);
        Assert.Equal("minimax-m3:cloud", await service.GetSelectedModelAsync("ollama-cloud", CancellationToken.None));
        Assert.Equal(cloudSettings, repository.Saved);
    }

    // Creates the service with deterministic provider defaults.
    private static AiConfigurationService CreateService(IAiSettingsRepository repository)
    {
        return new AiConfigurationService(
            repository,
            Options.Create(new AiOptions { DefaultProvider = "ollama-cloud" }),
            Options.Create(new AzureAiOptions { DeploymentName = "DeepSeek-V4-Flash" }),
            Options.Create(new OllamaLocalOptions { Model = "gemma4:12b" }),
            Options.Create(new OllamaCloudOptions { Model = "minimax-m3:cloud" }));
    }

    private sealed class InMemoryAiSettingsRepository : IAiSettingsRepository
    {
        private AiSettingsSnapshot? _settings;

        public InMemoryAiSettingsRepository(AiSettingsSnapshot? settings)
        {
            _settings = settings;
        }

        public AiSettingsSnapshot? Saved { get; private set; }

        public Task<AiSettingsSnapshot?> GetAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_settings);
        }

        public Task UpsertAsync(AiSettingsSnapshot settings, CancellationToken cancellationToken)
        {
            _settings = settings;
            Saved = settings;
            return Task.CompletedTask;
        }
    }
}
