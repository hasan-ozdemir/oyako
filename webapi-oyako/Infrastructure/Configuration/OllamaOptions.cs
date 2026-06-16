// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Configuration/OllamaOptions.cs for maintainers.
namespace webapi_oyako.Infrastructure.Configuration;

// Holds configuration for the local Ollama daemon provider.
public sealed class OllamaLocalOptions
{
    public const string SectionName = "OllamaLocal";

    // Stores the local Ollama API base URL.
    public string BaseUrl { get; set; } = "http://localhost:11434";
    // Stores the default local Ollama model.
    public string Model { get; set; } = "gemma4:12b";
    // Stores the local Ollama request timeout.
    public int TimeoutSeconds { get; set; } = 180;
    // Stores the temperature used for local Ollama requests.
    public double Temperature { get; set; } = 0.2;
}

// Holds configuration for the direct Ollama Cloud API provider.
public sealed class OllamaCloudOptions
{
    public const string SectionName = "OllamaCloud";

    // Stores the direct Ollama Cloud API base URL.
    public string BaseUrl { get; set; } = "https://ollama.com";
    // Stores the default cloud model used by Oyako.
    public string Model { get; set; } = "minimax-m3:cloud";
    // Stores the cloud model list exposed to the settings UI.
    public string[] Models { get; set; } = ["minimax-m3:cloud"];
    // Stores the direct API key when configuration binding supplies it explicitly.
    public string ApiKey { get; set; } = string.Empty;
    // Stores environment variable names that may contain the Ollama Cloud API key.
    public string[] ApiKeyEnvironmentVariables { get; set; } = ["ollama_api_key", "OLLAMA_API_KEY"];
    // Stores the Ollama Cloud request timeout.
    public int TimeoutSeconds { get; set; } = 180;
    // Stores the temperature used for Ollama Cloud requests.
    public double Temperature { get; set; } = 0.2;
}
