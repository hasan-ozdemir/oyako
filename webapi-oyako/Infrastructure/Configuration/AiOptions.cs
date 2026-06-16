// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Configuration/AiOptions.cs for maintainers.
namespace webapi_oyako.Infrastructure.Configuration;

// Implements the AiOptions component and its responsibilities in the Oyako codebase.
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string DefaultProvider { get; set; } = "ollama-cloud";
    // Lists provider identifiers that should not be registered into runtime provider selection surfaces.
    public string[] DisabledProviders { get; set; } = [];
}
