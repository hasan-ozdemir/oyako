// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Llm/AzureAiClient.cs for maintainers.
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Services;
using webapi_oyako.Infrastructure.Configuration;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Infrastructure.Llm;

// Implements the AzureAiClient component and its responsibilities in the Oyako codebase.
public sealed class AzureAiClient : IAiProviderClient
{
    // Stores state or a dependency required by the surrounding component.
    private readonly HttpClient _httpClient;
    // Stores state or a dependency required by the surrounding component.
    private readonly AzureAiOptions _options;
    // Stores state or a dependency required by the surrounding component.
    private readonly IAiConfigurationService _aiConfigurationService;

    public AzureAiClient(
        HttpClient httpClient,
        IOptions<AzureAiOptions> options,
        IAiConfigurationService aiConfigurationService)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _aiConfigurationService = aiConfigurationService;
    }

    // Stores state or a dependency required by the surrounding component.
    public string ProviderName => "azure";

    public async IAsyncEnumerable<string> StreamChatAsync(
        string systemInstruction,
        string userMessage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Creates a disposable resource scoped to this operation.
        using var request = await BuildRequestAsync(stream: true, systemInstruction, userMessage, cancellationToken);
        // Creates a disposable resource scoped to this operation.
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        // Creates a disposable resource scoped to this operation.
        using var reader = new StreamReader(stream);
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            // Guards the following branch so the workflow handles this condition deliberately.
            if (line is null)
            {
                break;
            }

            var content = ExtractStreamContent(line);
            // Guards the following branch so the workflow handles this condition deliberately.
            if (!string.IsNullOrEmpty(content))
            {
                yield return content;
            }
        }
    }

    public async Task<string> CompleteChatAsync(
        string systemInstruction,
        string userMessage,
        CancellationToken cancellationToken)
    {
        // Creates a disposable resource scoped to this operation.
        using var request = await BuildRequestAsync(stream: false, systemInstruction, userMessage, cancellationToken);
        // Creates a disposable resource scoped to this operation.
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        // Creates a disposable resource scoped to this operation.
        using var document = JsonDocument.Parse(content);
        // Returns the computed result to the caller and completes this branch of the workflow.
        return ExtractMessageContent(document.RootElement);
    }

    // Executes this component behavior as part of the Oyako application flow.
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return Task.FromResult(
            !string.IsNullOrWhiteSpace(_options.Endpoint)
            && GetConfiguredDeployments().Count > 0
            && !string.IsNullOrWhiteSpace(_options.ApiVersion)
            && !string.IsNullOrWhiteSpace(GetApiKey()));
    }

    // Executes this component behavior as part of the Oyako application flow.
    public Task<IReadOnlyList<AiModelDescriptor>> GetModelsAsync(CancellationToken cancellationToken)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return Task.FromResult<IReadOnlyList<AiModelDescriptor>>(
            GetConfiguredDeployments()
                // Creates the object needed for the next step of the workflow.
                .Select(model => new AiModelDescriptor(model, model, true))
                .ToList());
    }

    private async Task<HttpRequestMessage> BuildRequestAsync(
        bool stream,
        string systemInstruction,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var apiKey = GetApiKey();
        // Guards the following branch so the workflow handles this condition deliberately.
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // Stops the current workflow with an explicit failure that upstream handlers can report.
            throw new InvalidOperationException("Azure AI API key bulunamadı. azure-cloud.env içinde AzureAi__ApiKey değerini yapılandırın.");
        }

        var model = await GetModelAsync(cancellationToken);
        // Creates the object needed for the next step of the workflow.
        var request = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsPath(model));
        // Registers or maps application behavior into the runtime pipeline.
        request.Headers.Add("api-key", apiKey);
        request.Content = JsonContent.Create(new
        {
            stream,
            temperature = _options.Temperature,
            messages = new[]
            {
                // Creates the object needed for the next step of the workflow.
                new { role = "system", content = systemInstruction },
                // Creates the object needed for the next step of the workflow.
                new { role = "user", content = userMessage }
            }
        });
        // Returns the computed result to the caller and completes this branch of the workflow.
        return request;
    }

    // Executes this component behavior as part of the Oyako application flow.
    private string BuildChatCompletionsPath(string model)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return $"/openai/deployments/{Uri.EscapeDataString(model)}/chat/completions?api-version={Uri.EscapeDataString(_options.ApiVersion)}";
    }

    // Executes this component behavior as part of the Oyako application flow.
    private string? GetApiKey()
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return _options.ApiKey;
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKeyEnvironmentVariable))
        {
            return null;
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return Environment.GetEnvironmentVariable(_options.ApiKeyEnvironmentVariable);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private async Task<string> GetModelAsync(CancellationToken cancellationToken)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return await _aiConfigurationService.GetSelectedModelAsync("azure", cancellationToken);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private IReadOnlyList<string> GetConfiguredDeployments()
    {
        var deployments = _options.Deployments
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        // Guards the following branch so the workflow handles this condition deliberately.
        if (deployments.Count == 0 && !string.IsNullOrWhiteSpace(_options.DeploymentName))
        {
            // Registers or maps application behavior into the runtime pipeline.
            deployments.Add(_options.DeploymentName);
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return deployments;
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string? ExtractStreamContent(string line)
    {
        var trimmed = line.Trim();
        // Guards the following branch so the workflow handles this condition deliberately.
        if (!trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return null;
        }

        var payload = trimmed["data:".Length..].Trim();
        // Guards the following branch so the workflow handles this condition deliberately.
        if (payload.Length == 0 || payload.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return null;
        }

        try
        {
            // Creates a disposable resource scoped to this operation.
            using var document = JsonDocument.Parse(payload);
            var choice = document.RootElement.GetProperty("choices").EnumerateArray().FirstOrDefault();
            // Guards the following branch so the workflow handles this condition deliberately.
            if (choice.ValueKind == JsonValueKind.Undefined)
            {
                // Returns the computed result to the caller and completes this branch of the workflow.
                return null;
            }

            // Guards the following branch so the workflow handles this condition deliberately.
            if (choice.TryGetProperty("delta", out var delta)
                && delta.TryGetProperty("content", out var deltaContent)
                && deltaContent.ValueKind == JsonValueKind.String)
            {
                // Returns the computed result to the caller and completes this branch of the workflow.
                return deltaContent.GetString();
            }

            // Guards the following branch so the workflow handles this condition deliberately.
            if (choice.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var messageContent)
                && messageContent.ValueKind == JsonValueKind.String)
            {
                // Returns the computed result to the caller and completes this branch of the workflow.
                return messageContent.GetString();
            }
        }
        catch
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return null;
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return null;
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string ExtractMessageContent(JsonElement root)
    {
        var choice = root.GetProperty("choices").EnumerateArray().FirstOrDefault();
        // Guards the following branch so the workflow handles this condition deliberately.
        if (choice.ValueKind == JsonValueKind.Undefined)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return string.Empty;
        }

        // Guards the following branch so the workflow handles this condition deliberately.
        if (choice.TryGetProperty("message", out var message)
            && message.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.String)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return content.GetString() ?? string.Empty;
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return string.Empty;
    }
}
