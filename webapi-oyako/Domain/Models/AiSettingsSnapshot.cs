// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Models/AiSettingsSnapshot.cs for maintainers.
namespace webapi_oyako.Domain.Models;

// Defines the immutable AiSettingsSnapshot data shape exchanged between Oyako components.
public sealed record AiSettingsSnapshot(
    string ActiveProvider,
    string AzureModel,
    string OllamaLocalModel,
    string OllamaCloudModel,
    DateTime UpdatedAtUtc)
{
    // Returns the model selected for the active provider.
    public string ActiveModel => GetModel(ActiveProvider);

    // Returns the model selected for a specific provider identifier.
    public string GetModel(string provider) => provider.ToLowerInvariant() switch
    {
        "ollama-local" => OllamaLocalModel,
        "ollama-cloud" => OllamaCloudModel,
        _ => AzureModel
    };
}
