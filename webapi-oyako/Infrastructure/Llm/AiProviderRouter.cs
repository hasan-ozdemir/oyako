// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Llm/AiProviderRouter.cs for maintainers.
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Services;
using webapi_oyako.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Infrastructure.Llm;

// Implements the AiProviderRouter component and its responsibilities in the Oyako codebase.
public sealed class AiProviderRouter : IAiChatClient, IAiProviderCatalog
{
    private readonly IReadOnlyDictionary<string, IAiProviderClient> _providers;
    // Stores state or a dependency required by the surrounding component.
    private readonly IAiConfigurationService _aiConfigurationService;

    // Creates a new instance and captures the dependencies needed by this component.
    public AiProviderRouter(IEnumerable<IAiProviderClient> providers, IAiConfigurationService aiConfigurationService, IOptions<AiOptions>? aiOptions = null)
    {
        var disabledProviders = aiOptions?.Value.DisabledProviders ?? [];
        var disabledSet = new HashSet<string>(disabledProviders.Where(provider => !string.IsNullOrWhiteSpace(provider)), StringComparer.OrdinalIgnoreCase);
        _providers = providers
            .Where(provider => !disabledSet.Contains(provider.ProviderName))
            .ToDictionary(provider => provider.ProviderName, StringComparer.OrdinalIgnoreCase);
        _aiConfigurationService = aiConfigurationService;
    }

    // Stores state or a dependency required by the surrounding component.
    public string ProviderName => ActiveProvider;

    // Stores state or a dependency required by the surrounding component.
    public string ActiveProvider => _aiConfigurationService.Current.ActiveProvider;

    // Stores state or a dependency required by the surrounding component.
    public string ActiveModel => _aiConfigurationService.Current.ActiveModel;

    public async IAsyncEnumerable<string> StreamChatAsync(
        string systemInstruction,
        string userMessage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var failures = new List<Exception>();
        foreach (var client in await GetProviderCandidatesAsync(cancellationToken))
        {
            var emittedToken = false;
            await using var enumerator = client.StreamChatAsync(systemInstruction, userMessage, cancellationToken).GetAsyncEnumerator(cancellationToken);
            while (true)
            {
                var moveResult = await TryMoveNextAsync(enumerator);
                if (moveResult.Error is not null)
                {
                    failures.Add(new InvalidOperationException($"{client.ProviderName}: {moveResult.Error.Message}", moveResult.Error));
                    if (emittedToken)
                    {
                        throw BuildProviderFailure(failures);
                    }

                    break;
                }

                if (!moveResult.Moved)
                {
                    yield break;
                }

                emittedToken = true;
                yield return enumerator.Current;
            }
        }

        throw BuildProviderFailure(failures);
    }

    public Task<string> CompleteChatAsync(
        string systemInstruction,
        string userMessage,
        CancellationToken cancellationToken)
    {
        return CompleteWithFallbackAsync(systemInstruction, userMessage, cancellationToken);
    }

    // Executes this component behavior as part of the Oyako application flow.
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return GetActiveProviderClient().IsAvailableAsync(cancellationToken);
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<IReadOnlyList<AiProviderStatus>> GetProviderStatusesAsync(CancellationToken cancellationToken)
    {
        // Creates the object needed for the next step of the workflow.
        var statuses = new List<AiProviderStatus>();
        // Iterates through the collection to process each item consistently.
        foreach (var provider in _providers.Values.OrderBy(provider => provider.ProviderName, StringComparer.OrdinalIgnoreCase))
        {
            var isActive = string.Equals(provider.ProviderName, ActiveProvider, StringComparison.OrdinalIgnoreCase);
            try
            {
                var isAvailable = await provider.IsAvailableAsync(cancellationToken);
                // Creates the object needed for the next step of the workflow.
                statuses.Add(new AiProviderStatus(
                    provider.ProviderName,
                    isAvailable ? "ok" : "unavailable",
                    isActive,
                    isAvailable ? null : "Provider kullanılamıyor veya yapılandırması eksik."));
            }
            catch (Exception ex)
            {
                // Creates the object needed for the next step of the workflow.
                statuses.Add(new AiProviderStatus(provider.ProviderName, "unavailable", isActive, ex.Message));
            }
        }

        // Guards the following branch so the workflow handles this condition deliberately.
        if (!_providers.ContainsKey(ActiveProvider))
        {
            // Creates the object needed for the next step of the workflow.
            statuses.Add(new AiProviderStatus(
                ActiveProvider,
                "unavailable",
                true,
                $"Aktif provider kayıtlı değil: {ActiveProvider}"));
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return statuses;
    }

    // Executes this component behavior as part of the Oyako application flow.
    private IAiProviderClient GetActiveProviderClient()
    {
        // Guards the following branch so the workflow handles this condition deliberately.
        if (_providers.TryGetValue(ActiveProvider, out var provider))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return provider;
        }

        // Stops the current workflow with an explicit failure that upstream handlers can report.
        throw new InvalidOperationException($"AI provider bulunamadı: {ActiveProvider}");
    }

    // Completes a chat request by trying the active provider first and then healthy fallback providers.
    private async Task<string> CompleteWithFallbackAsync(string systemInstruction, string userMessage, CancellationToken cancellationToken)
    {
        var failures = new List<Exception>();
        foreach (var client in await GetProviderCandidatesAsync(cancellationToken))
        {
            try
            {
                return await client.CompleteChatAsync(systemInstruction, userMessage, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                failures.Add(new InvalidOperationException($"{client.ProviderName}: {ex.Message}", ex));
            }
        }

        throw BuildProviderFailure(failures);
    }

    // Builds the ordered provider list: active provider first, then available alternatives.
    private async Task<IReadOnlyList<IAiProviderClient>> GetProviderCandidatesAsync(CancellationToken cancellationToken)
    {
        if (!_providers.TryGetValue(ActiveProvider, out var activeProvider))
        {
            throw new InvalidOperationException($"AI provider bulunamadı: {ActiveProvider}");
        }

        var candidates = new List<IAiProviderClient> { activeProvider };
        foreach (var provider in _providers.Values
            .Where(provider => !string.Equals(provider.ProviderName, ActiveProvider, StringComparison.OrdinalIgnoreCase))
            .OrderBy(GetFallbackPriority)
            .ThenBy(provider => provider.ProviderName, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (await provider.IsAvailableAsync(cancellationToken))
                {
                    candidates.Add(provider);
                }
            }
            catch
            {
                // Unavailable fallback providers are ignored so the active provider failure can move to the next usable option.
            }
        }

        return candidates;
    }

    // Gives cloud-capable providers priority over the local daemon for fallback in hosted and local runs.
    private static int GetFallbackPriority(IAiProviderClient provider)
    {
        return provider.ProviderName.ToLowerInvariant() switch
        {
            "azure" => 0,
            "ollama-cloud" => 1,
            "ollama-local" => 2,
            _ => 10
        };
    }

    // Converts async stream MoveNext failures into values so fallback can happen before any token reaches the UI.
    private static async Task<(bool Moved, Exception? Error)> TryMoveNextAsync(IAsyncEnumerator<string> enumerator)
    {
        try
        {
            return (await enumerator.MoveNextAsync(), null);
        }
        catch (Exception ex)
        {
            return (false, ex);
        }
    }

    // Builds a concise provider failure that keeps the user-facing API secret-safe.
    private static InvalidOperationException BuildProviderFailure(IReadOnlyList<Exception> failures)
    {
        var message = failures.Count == 0
            ? "AI provider yanıt üretemedi."
            : $"AI provider yanıt üretemedi: {string.Join(" | ", failures.Select(failure => failure.Message).Take(3))}";
        return new InvalidOperationException(message);
    }
}
