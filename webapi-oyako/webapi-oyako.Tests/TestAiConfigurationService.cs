// Codex developer note: Explains the purpose and flow of webapi-oyako/webapi-oyako.Tests/TestAiConfigurationService.cs for maintainers.
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Services;

namespace webapi_oyako.Tests;

// Provides an in-memory AI configuration service for tests.
public sealed class TestAiConfigurationService : IAiConfigurationService
{
    private AiSettingsSnapshot _current;

    public TestAiConfigurationService(
        string activeProvider = "ollama-cloud",
        string azureModel = "DeepSeek-V4-Flash",
        string ollamaLocalModel = "gemma4:12b",
        string ollamaCloudModel = "minimax-m3:cloud")
    {
        _current = new AiSettingsSnapshot(activeProvider, azureModel, ollamaLocalModel, ollamaCloudModel, DateTime.UtcNow);
    }

    public AiSettingsSnapshot Current => _current;

    public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<AiSettingsSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_current);
    }

    public Task<AiSettingsSnapshot> UpdateAsync(string provider, string model, CancellationToken cancellationToken)
    {
        _current = provider.ToLowerInvariant() switch
        {
            "ollama-local" => _current with { ActiveProvider = "ollama-local", OllamaLocalModel = model, UpdatedAtUtc = DateTime.UtcNow },
            "ollama-cloud" => _current with { ActiveProvider = "ollama-cloud", OllamaCloudModel = model, UpdatedAtUtc = DateTime.UtcNow },
            _ => _current with { ActiveProvider = "azure", AzureModel = model, UpdatedAtUtc = DateTime.UtcNow }
        };

        return Task.FromResult(_current);
    }

    public Task<string> GetSelectedModelAsync(string provider, CancellationToken cancellationToken)
    {
        return Task.FromResult(_current.GetModel(provider));
    }
}
