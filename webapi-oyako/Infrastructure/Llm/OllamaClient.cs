// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Llm/OllamaClient.cs for maintainers.
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Services;
using webapi_oyako.Infrastructure.Configuration;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Infrastructure.Llm;

// Provides the shared native Ollama /api/chat protocol implementation used by local and cloud providers.
public abstract class OllamaNativeClientBase : IAiProviderClient
{
    // Stores the HTTP client configured for the concrete Ollama endpoint.
    private readonly HttpClient _httpClient;
    // Stores the AI settings service used to resolve the selected model for this provider.
    private readonly IAiConfigurationService _aiConfigurationService;

    // Creates a new shared Ollama protocol client.
    protected OllamaNativeClientBase(HttpClient httpClient, IAiConfigurationService aiConfigurationService)
    {
        _httpClient = httpClient;
        _aiConfigurationService = aiConfigurationService;
    }

    // Identifies the provider handled by the concrete client.
    public abstract string ProviderName { get; }
    // Provides the configured temperature for the concrete provider.
    protected abstract double Temperature { get; }

    // Streams a one-shot system/user chat request through the native Ollama chat endpoint.
    public async IAsyncEnumerable<string> StreamChatAsync(
        string systemInstruction,
        string userMessage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var request = await BuildChatRequestAsync(stream: true, systemInstruction, userMessage, cancellationToken);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await BuildFailureMessageAsync(response, cancellationToken));
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            OllamaResponseChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<OllamaResponseChunk>(line);
            }
            catch
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(chunk?.Error))
            {
                throw new InvalidOperationException(BuildProviderErrorMessage(chunk.Error));
            }

            if (chunk?.Message?.Content is { Length: > 0 } content)
            {
                yield return content;
            }
        }
    }

    // Completes a one-shot system/user chat request through the native Ollama chat endpoint.
    public async Task<string> CompleteChatAsync(
        string systemInstruction,
        string userMessage,
        CancellationToken cancellationToken)
    {
        using var request = await BuildChatRequestAsync(stream: false, systemInstruction, userMessage, cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await BuildFailureMessageAsync(response, cancellationToken));
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(content);
        if (document.RootElement.TryGetProperty("error", out var errorElement))
        {
            throw new InvalidOperationException(BuildProviderErrorMessage(errorElement.GetString() ?? "Ollama provider hatası."));
        }

        return document.RootElement
            .GetProperty("message")
            .GetProperty("content")
            .GetString()
            ?? string.Empty;
    }

    // Verifies whether the provider can serve its selected model.
    public abstract Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    // Lists models exposed by the concrete provider.
    public abstract Task<IReadOnlyList<AiModelDescriptor>> GetModelsAsync(CancellationToken cancellationToken);

    // Lets concrete providers add headers or fail fast before the request is sent.
    protected virtual void PrepareRequest(HttpRequestMessage request)
    {
    }

    // Resolves the model and builds the native Ollama chat request body.
    private async Task<HttpRequestMessage> BuildChatRequestAsync(
        bool stream,
        string systemInstruction,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var model = await _aiConfigurationService.GetSelectedModelAsync(ProviderName, cancellationToken);
        var requestBody = new
        {
            model,
            stream,
            messages = new[]
            {
                new { role = "system", content = systemInstruction },
                new { role = "user", content = userMessage }
            },
            temperature = Temperature
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(requestBody)
        };
        PrepareRequest(request);
        return request;
    }

    // Builds a provider-specific but secret-safe HTTP failure message.
    private async Task<string> BuildFailureMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync(cancellationToken);
        var providerMessage = ExtractErrorMessage(body);
        return string.IsNullOrWhiteSpace(providerMessage)
            ? $"{ProviderName} yanıtı başarısız: HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
            : $"{ProviderName} yanıtı başarısız: HTTP {(int)response.StatusCode} {providerMessage}";
    }

    // Extracts the standard Ollama error property from a response body when present.
    private static string ExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var errorElement)
                ? errorElement.GetString() ?? string.Empty
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    // Normalizes provider stream errors without exposing credentials.
    private string BuildProviderErrorMessage(string error)
    {
        return $"{ProviderName} yanıtı başarısız: {error}";
    }
}

// Implements the local Ollama provider backed by the machine-local Ollama daemon.
public sealed class OllamaLocalClient : OllamaNativeClientBase
{
    // Stores the local provider options.
    private readonly OllamaLocalOptions _options;
    // Stores the AI settings service for selected model lookup.
    private readonly IAiConfigurationService _aiConfigurationService;
    // Stores the HTTP client used to query local model tags.
    private readonly HttpClient _httpClient;

    // Creates a local Ollama provider client.
    public OllamaLocalClient(
        HttpClient httpClient,
        IOptions<OllamaLocalOptions> options,
        IAiConfigurationService aiConfigurationService)
        : base(httpClient, aiConfigurationService)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _aiConfigurationService = aiConfigurationService;
    }

    // Identifies this client as the local Ollama provider.
    public override string ProviderName => "ollama-local";
    // Returns the local provider temperature.
    protected override double Temperature => _options.Temperature;

    // Verifies that the selected local model exists in the local Ollama tag list.
    public override async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var selectedModel = await _aiConfigurationService.GetSelectedModelAsync(ProviderName, cancellationToken);
            var models = await GetModelsAsync(cancellationToken);
            return models.Any(model => model.IsAvailable && string.Equals(model.Id, selectedModel, StringComparison.Ordinal));
        }
        catch
        {
            return false;
        }
    }

    // Reads local models from /api/tags.
    public override async Task<IReadOnlyList<AiModelDescriptor>> GetModelsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<AiModelDescriptor>();
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("models", out var modelsElement))
            {
                return Array.Empty<AiModelDescriptor>();
            }

            return modelsElement
                .EnumerateArray()
                .Select(model => model.TryGetProperty("name", out var name) ? name.GetString() : null)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => new AiModelDescriptor(name!, name!, true))
                .ToList();
        }
        catch
        {
            return Array.Empty<AiModelDescriptor>();
        }
    }
}

// Implements the direct Ollama Cloud provider backed by https://ollama.com/api.
public sealed class OllamaCloudClient : OllamaNativeClientBase
{
    // Stores the Ollama Cloud options.
    private readonly OllamaCloudOptions _options;
    // Stores the AI settings service for selected model lookup.
    private readonly IAiConfigurationService _aiConfigurationService;

    // Creates an Ollama Cloud provider client.
    public OllamaCloudClient(
        HttpClient httpClient,
        IOptions<OllamaCloudOptions> options,
        IAiConfigurationService aiConfigurationService)
        : base(httpClient, aiConfigurationService)
    {
        _options = options.Value;
        _aiConfigurationService = aiConfigurationService;
    }

    // Identifies this client as the Ollama Cloud provider.
    public override string ProviderName => "ollama-cloud";
    // Returns the cloud provider temperature.
    protected override double Temperature => _options.Temperature;

    // Verifies that a cloud API key is configured and the selected cloud model is in the configured model list.
    public override async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ResolveApiKey()))
        {
            return false;
        }

        var selectedModel = await _aiConfigurationService.GetSelectedModelAsync(ProviderName, cancellationToken);
        var models = await GetModelsAsync(cancellationToken);
        return models.Any(model => model.IsAvailable && string.Equals(model.Id, selectedModel, StringComparison.Ordinal));
    }

    // Returns configured Ollama Cloud models because the direct cloud API does not require local tag discovery for Oyako.
    public override Task<IReadOnlyList<AiModelDescriptor>> GetModelsAsync(CancellationToken cancellationToken)
    {
        var hasKey = !string.IsNullOrWhiteSpace(ResolveApiKey());
        var modelNames = (_options.Models.Length == 0 ? new[] { _options.Model } : _options.Models)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        IReadOnlyList<AiModelDescriptor> models = modelNames
            .Select(model => new AiModelDescriptor(model, model, hasKey))
            .ToList();
        return Task.FromResult(models);
    }

    // Adds the Bearer API key required by the direct Ollama Cloud API.
    protected override void PrepareRequest(HttpRequestMessage request)
    {
        var apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Ollama Cloud API key bulunamadı. ollama-cloud.env içinde ollama_api_key değerini yapılandırın.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    // Resolves the API key from explicit options or supported environment variables.
    private string ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return _options.ApiKey;
        }

        foreach (var variableName in _options.ApiKeyEnvironmentVariables.Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }
}
