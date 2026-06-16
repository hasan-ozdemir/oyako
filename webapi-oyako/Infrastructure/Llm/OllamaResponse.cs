// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Llm/OllamaResponse.cs for maintainers.
using System.Text.Json.Serialization;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Infrastructure.Llm;

// Implements the OllamaResponseChunk component and its responsibilities in the Oyako codebase.
public sealed class OllamaResponseChunk
{
    [JsonPropertyName("done")]
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public bool Done { get; set; }

    [JsonPropertyName("message")]
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public OllamaMessage? Message { get; set; }

    [JsonPropertyName("error")]
    // Exposes provider stream errors returned inside Ollama NDJSON responses.
    public string? Error { get; set; }
}

// Implements the OllamaMessage component and its responsibilities in the Oyako codebase.
public sealed class OllamaMessage
{
    [JsonPropertyName("role")]
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string? Content { get; set; }
}

// Implements the OllamaTagsResponse component and its responsibilities in the Oyako codebase.
public sealed class OllamaTagsResponse
{
    [JsonPropertyName("models")]
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public List<OllamaTag>? Models { get; set; }
}

// Implements the OllamaTag component and its responsibilities in the Oyako codebase.
public sealed class OllamaTag
{
    [JsonPropertyName("name")]
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string? Name { get; set; }
}
