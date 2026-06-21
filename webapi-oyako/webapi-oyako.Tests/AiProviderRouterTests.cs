// Codex developer note: Explains the purpose and flow of webapi-oyako/webapi-oyako.Tests/AiProviderRouterTests.cs for maintainers.
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Services;
using webapi_oyako.Infrastructure.Configuration;
using webapi_oyako.Infrastructure.Llm;
using Microsoft.Extensions.Options;
using Xunit;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Tests;

// Verifies routing across configured AI providers.
public class AiProviderRouterTests
{
    [Fact]
    // Verifies the router uses the configured default provider.
    public async Task CompleteChatAsync_UsesConfiguredDefaultProvider()
    {
        var azure = new StubProvider("azure", "azure answer");
        var ollamaLocal = new StubProvider("ollama-local", "local answer");
        var ollamaCloud = new StubProvider("ollama-cloud", "cloud answer");
        var router = new AiProviderRouter(
            new IAiProviderClient[] { ollamaLocal, ollamaCloud, azure },
            new StubAiConfigurationService("ollama-cloud", "azure-model", "local-model", "cloud-model"));

        var answer = await router.CompleteChatAsync("system", "user", CancellationToken.None);

        Assert.Equal("cloud answer", answer);
        Assert.Equal(0, azure.CompleteCalls);
        Assert.Equal(0, ollamaLocal.CompleteCalls);
        Assert.Equal(1, ollamaCloud.CompleteCalls);
    }

    [Fact]
    // Verifies quota or transport failures on the active provider fail over to the next available provider.
    public async Task CompleteChatAsync_WhenActiveProviderFails_FallsBackToAzure()
    {
        var azure = new StubProvider("azure", "azure answer");
        var ollamaCloud = new FailingProvider("ollama-cloud", "HTTP 429 quota");
        var router = new AiProviderRouter(
            new IAiProviderClient[] { ollamaCloud, azure },
            new StubAiConfigurationService("ollama-cloud", "azure-model", "local-model", "cloud-model"));

        var answer = await router.CompleteChatAsync("system", "user", CancellationToken.None);

        Assert.Equal("azure answer", answer);
        Assert.Equal(1, ollamaCloud.CompleteCalls);
        Assert.Equal(1, azure.CompleteCalls);
    }

    [Fact]
    // Verifies streaming failover happens only before any token reaches the caller.
    public async Task StreamChatAsync_WhenActiveProviderFailsBeforeToken_FallsBackToAzure()
    {
        var azure = new StubProvider("azure", "azure stream");
        var ollamaCloud = new FailingProvider("ollama-cloud", "HTTP 429 quota");
        var router = new AiProviderRouter(
            new IAiProviderClient[] { ollamaCloud, azure },
            new StubAiConfigurationService("ollama-cloud", "azure-model", "local-model", "cloud-model"));

        var tokens = new List<string>();
        await foreach (var token in router.StreamChatAsync("system", "user", CancellationToken.None))
        {
            tokens.Add(token);
        }

        Assert.Equal(new[] { "azure stream" }, tokens);
    }

    [Fact]
    public async Task CompleteChatAsync_WhenAzureFallbackIsMissing_DoesNotFallbackToLocalProvider()
    {
        var ollamaLocal = new StubProvider("ollama-local", "local answer");
        var ollamaCloud = new FailingProvider("ollama-cloud", "HTTP 429 quota");
        var router = new AiProviderRouter(
            new IAiProviderClient[] { ollamaCloud, ollamaLocal },
            new StubAiConfigurationService("ollama-cloud", "azure-model", "local-model", "cloud-model"),
            Options.Create(new AiOptions { FallbackProviders = ["azure"] }));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => router.CompleteChatAsync("system", "user", CancellationToken.None));

        Assert.Contains("ollama-cloud", error.Message);
        Assert.Equal(1, ollamaCloud.CompleteCalls);
        Assert.Equal(0, ollamaLocal.CompleteCalls);
    }

    [Fact]
    // Verifies provider health marks the configured active provider.
    public async Task GetProviderStatusesAsync_MarksActiveProvider()
    {
        var router = new AiProviderRouter(
            new IAiProviderClient[]
            {
                new StubProvider("ollama-local", "ok"),
                new StubProvider("ollama-cloud", "ok"),
                new StubProvider("azure", "ok")
            },
            new StubAiConfigurationService("ollama-local", "azure-model", "local-model", "cloud-model"));

        var statuses = await router.GetProviderStatusesAsync(CancellationToken.None);

        Assert.Contains(statuses, status => status.Provider == "ollama-local" && status.IsActive && status.Status == "ok");
        Assert.Contains(statuses, status => status.Provider == "ollama-cloud" && !status.IsActive && status.Status == "ok");
        Assert.Contains(statuses, status => status.Provider == "azure" && !status.IsActive && status.Status == "ok");
    }

    private sealed class StubProvider : IAiProviderClient
    {
        private readonly string _answer;

        public StubProvider(string providerName, string answer)
        {
            ProviderName = providerName;
            _answer = answer;
        }

        public string ProviderName { get; }
        public int CompleteCalls { get; private set; }

        public async IAsyncEnumerable<string> StreamChatAsync(
            string systemInstruction,
            string userMessage,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield return _answer;
        }

        public Task<string> CompleteChatAsync(
            string systemInstruction,
            string userMessage,
            CancellationToken cancellationToken)
        {
            CompleteCalls++;
            return Task.FromResult(_answer);
        }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<AiModelDescriptor>> GetModelsAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<AiModelDescriptor> models =
            [
                new AiModelDescriptor($"{ProviderName}-model", $"{ProviderName} model", true)
            ];

            return Task.FromResult(models);
        }
    }

    private sealed class FailingProvider : IAiProviderClient
    {
        private readonly string _message;

        public FailingProvider(string providerName, string message)
        {
            ProviderName = providerName;
            _message = message;
        }

        public string ProviderName { get; }
        public int CompleteCalls { get; private set; }

        public async IAsyncEnumerable<string> StreamChatAsync(
            string systemInstruction,
            string userMessage,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            if (DateTime.UtcNow.Ticks >= 0)
            {
                throw new InvalidOperationException(_message);
            }

            yield return string.Empty;
        }

        public Task<string> CompleteChatAsync(
            string systemInstruction,
            string userMessage,
            CancellationToken cancellationToken)
        {
            CompleteCalls++;
            throw new InvalidOperationException(_message);
        }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<AiModelDescriptor>> GetModelsAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<AiModelDescriptor> models =
            [
                new AiModelDescriptor($"{ProviderName}-model", $"{ProviderName} model", true)
            ];

            return Task.FromResult(models);
        }
    }

    private sealed class StubAiConfigurationService : IAiConfigurationService
    {
        private AiSettingsSnapshot _current;

        public StubAiConfigurationService(string provider, string azureModel, string ollamaLocalModel, string ollamaCloudModel)
        {
            _current = new AiSettingsSnapshot(provider, azureModel, ollamaLocalModel, ollamaCloudModel, DateTime.UtcNow);
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
}
