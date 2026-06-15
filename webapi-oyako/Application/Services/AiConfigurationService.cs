// Codex developer note: Explains the purpose and flow of webapi-oyako/Application/Services/AiConfigurationService.cs for maintainers.
using Microsoft.Extensions.Options;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Domain.Services;
using webapi_oyako.Infrastructure.Configuration;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Application.Services;

// Implements the AiConfigurationService component and its responsibilities in the Oyako codebase.
public sealed class AiConfigurationService : IAiConfigurationService
{
    // Stores state or a dependency required by the surrounding component.
    private readonly IAiSettingsRepository _repository;
    // Stores state or a dependency required by the surrounding component.
    private readonly AiOptions _aiOptions;
    // Stores state or a dependency required by the surrounding component.
    private readonly AzureAiOptions _azureAiOptions;
    // Stores local Ollama defaults for the local provider.
    private readonly OllamaLocalOptions _ollamaLocalOptions;
    // Stores Ollama Cloud defaults for the cloud provider.
    private readonly OllamaCloudOptions _ollamaCloudOptions;
    // Stores state or a dependency required by the surrounding component.
    private readonly SemaphoreSlim _gate = new(1, 1);
    // Stores state or a dependency required by the surrounding component.
    private AiSettingsSnapshot? _current;

    public AiConfigurationService(
        IAiSettingsRepository repository,
        IOptions<AiOptions> aiOptions,
        IOptions<AzureAiOptions> azureAiOptions,
        IOptions<OllamaLocalOptions> ollamaLocalOptions,
        IOptions<OllamaCloudOptions> ollamaCloudOptions)
    {
        _repository = repository;
        _aiOptions = aiOptions.Value;
        _azureAiOptions = azureAiOptions.Value;
        _ollamaLocalOptions = ollamaLocalOptions.Value;
        _ollamaCloudOptions = ollamaCloudOptions.Value;
    }

    // Stores state or a dependency required by the surrounding component.
    public AiSettingsSnapshot Current => _current ?? BuildDefault();

    // Executes this component behavior as part of the Oyako application flow.
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await GetAsync(cancellationToken);
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<AiSettingsSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        // Guards the following branch so the workflow handles this condition deliberately.
        if (_current is not null)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return _current;
        }

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await _gate.WaitAsync(cancellationToken);
        try
        {
            // Guards the following branch so the workflow handles this condition deliberately.
            if (_current is not null)
            {
                // Returns the computed result to the caller and completes this branch of the workflow.
                return _current;
            }

            _current = await _repository.GetAsync(cancellationToken) ?? BuildDefault();
            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await _repository.UpsertAsync(_current, cancellationToken);
            // Returns the computed result to the caller and completes this branch of the workflow.
            return _current;
        }
        finally
        {
            _gate.Release();
        }
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<AiSettingsSnapshot> UpdateAsync(string provider, string model, CancellationToken cancellationToken)
    {
        var normalizedProvider = NormalizeProvider(provider);
        // Guards the following branch so the workflow handles this condition deliberately.
        if (normalizedProvider is null)
        {
            // Stops the current workflow with an explicit failure that upstream handlers can report.
            throw new ArgumentException("Invalid AI provider.", nameof(provider));
        }

        var current = await GetAsync(cancellationToken);
        var next = normalizedProvider switch
        {
            "ollama-local" => current with { ActiveProvider = "ollama-local", OllamaLocalModel = model, UpdatedAtUtc = DateTime.UtcNow },
            "ollama-cloud" => current with { ActiveProvider = "ollama-cloud", OllamaCloudModel = model, UpdatedAtUtc = DateTime.UtcNow },
            _ => current with { ActiveProvider = "azure", AzureModel = model, UpdatedAtUtc = DateTime.UtcNow }
        };

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await _gate.WaitAsync(cancellationToken);
        try
        {
            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await _repository.UpsertAsync(next, cancellationToken);
            _current = next;
            // Returns the computed result to the caller and completes this branch of the workflow.
            return next;
        }
        finally
        {
            _gate.Release();
        }
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<string> GetSelectedModelAsync(string provider, CancellationToken cancellationToken)
    {
        var current = await GetAsync(cancellationToken);
        // Returns the computed result to the caller and completes this branch of the workflow.
        return current.GetModel(NormalizeProvider(provider) ?? provider);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private AiSettingsSnapshot BuildDefault()
    {
        var activeProvider = NormalizeProvider(_aiOptions.DefaultProvider) ?? "ollama-cloud";
        // Returns the computed result to the caller and completes this branch of the workflow.
        return new AiSettingsSnapshot(
            activeProvider,
            _azureAiOptions.DeploymentName,
            _ollamaLocalOptions.Model,
            _ollamaCloudOptions.Model,
            DateTime.UtcNow);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string? NormalizeProvider(string provider)
    {
        // Guards the following branch so the workflow handles this condition deliberately.
        if (provider.Equals("azure", StringComparison.OrdinalIgnoreCase))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return "azure";
        }

        // Guards the following branch so the workflow handles this condition deliberately.
        if (provider.Equals("ollama-local", StringComparison.OrdinalIgnoreCase))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return "ollama-local";
        }

        // Guards the following branch so the workflow handles this condition deliberately.
        if (provider.Equals("ollama-cloud", StringComparison.OrdinalIgnoreCase))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return "ollama-cloud";
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return null;
    }
}
