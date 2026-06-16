// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Configuration/AzureAiOptions.cs for maintainers.
namespace webapi_oyako.Infrastructure.Configuration;

// Implements the AzureAiOptions component and its responsibilities in the Oyako codebase.
public sealed class AzureAiOptions
{
    public const string SectionName = "AzureAi";

    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string Endpoint { get; set; } = "https://oyako-pr1-sc1.services.ai.azure.com/";
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string DeploymentName { get; set; } = "DeepSeek-V4-Flash";
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string[] Deployments { get; set; } = ["DeepSeek-V4-Flash"];
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string ApiVersion { get; set; } = "2024-10-21";
    // Exposes the direct Azure AI API key supplied by azure-cloud.env or the host environment.
    public string ApiKey { get; set; } = string.Empty;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string ApiKeyEnvironmentVariable { get; set; } = string.Empty;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public int TimeoutSeconds { get; set; } = 180;
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public double Temperature { get; set; } = 0.2;
}
