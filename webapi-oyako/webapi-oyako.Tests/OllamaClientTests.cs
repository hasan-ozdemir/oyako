// Codex developer note: Explains the purpose and flow of webapi-oyako/webapi-oyako.Tests/OllamaClientTests.cs for maintainers.
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using webapi_oyako.Infrastructure.Configuration;
using webapi_oyako.Infrastructure.Llm;
using Xunit;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Tests;

// Verifies the native Ollama local and cloud clients.
public class OllamaClientTests
{
    [Fact]
    // Verifies local Ollama sends one-shot chat requests without cloud authorization.
    public async Task LocalStreamChatAsync_SendsOnlySystemAndCurrentUserMessageWithoutAuthorization()
    {
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        var client = new OllamaLocalClient(
            httpClient,
            Options.Create(new OllamaLocalOptions { Model = "gemma4:12b" }),
            new TestAiConfigurationService(activeProvider: "ollama-local", ollamaLocalModel: "gemma4:12b"));

        await DrainAsync(client.StreamChatAsync("system instruction", "ilk soru", CancellationToken.None));
        await DrainAsync(client.StreamChatAsync("system instruction", "ikinci soru", CancellationToken.None));

        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.All(handler.AuthorizationHeaders, Assert.Null);
        AssertRequestShape(handler.RequestBodies[0], "gemma4:12b", "ilk soru");
        AssertRequestShape(handler.RequestBodies[1], "gemma4:12b", "ikinci soru");
    }

    [Fact]
    // Verifies Ollama Cloud sends one-shot chat requests with a Bearer API key.
    public async Task CloudStreamChatAsync_SendsBearerAuthorizationAndCloudModel()
    {
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://ollama.com")
        };
        var client = new OllamaCloudClient(
            httpClient,
            Options.Create(new OllamaCloudOptions
            {
                Model = "minimax-m3:cloud",
                ApiKey = "test-secret",
                Models = ["minimax-m3:cloud"]
            }),
            new TestAiConfigurationService(activeProvider: "ollama-cloud", ollamaCloudModel: "minimax-m3:cloud"));

        await DrainAsync(client.StreamChatAsync("system instruction", "cloud soru", CancellationToken.None));

        Assert.Single(handler.RequestBodies);
        Assert.Equal("Bearer", handler.AuthorizationHeaders[0]?.Scheme);
        Assert.Equal("test-secret", handler.AuthorizationHeaders[0]?.Parameter);
        AssertRequestShape(handler.RequestBodies[0], "minimax-m3:cloud", "cloud soru");
    }

    [Fact]
    // Verifies Ollama Cloud fails fast when no API key is configured.
    public async Task CloudStreamChatAsync_WithoutApiKey_FailsWithSafeMessage()
    {
        using var httpClient = new HttpClient(new CapturingHandler())
        {
            BaseAddress = new Uri("https://ollama.com")
        };
        var client = new OllamaCloudClient(
            httpClient,
            Options.Create(new OllamaCloudOptions { ApiKey = string.Empty, ApiKeyEnvironmentVariables = [] }),
            new TestAiConfigurationService(activeProvider: "ollama-cloud", ollamaCloudModel: "minimax-m3:cloud"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await DrainAsync(client.StreamChatAsync("system", "user", CancellationToken.None)));

        Assert.Contains("Ollama Cloud API key bulunamadı", exception.Message);
        Assert.DoesNotContain("Bearer", exception.Message);
    }

    // Drains an async stream so tests can inspect captured HTTP requests.
    private static async Task DrainAsync(IAsyncEnumerable<string> stream)
    {
        await foreach (var _ in stream)
        {
        }
    }

    // Verifies the provider request remains one-shot system/user without assistant history.
    private static void AssertRequestShape(string body, string expectedModel, string expectedQuestion)
    {
        using var document = JsonDocument.Parse(body);
        var messages = document.RootElement.GetProperty("messages").EnumerateArray().ToList();

        Assert.Equal(expectedModel, document.RootElement.GetProperty("model").GetString());
        Assert.Equal(2, messages.Count);
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("system instruction", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal(expectedQuestion, messages[1].GetProperty("content").GetString());
        Assert.DoesNotContain(messages, message => message.GetProperty("role").GetString() == "assistant");
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = new();
        public List<System.Net.Http.Headers.AuthenticationHeaderValue?> AuthorizationHeaders { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            AuthorizationHeaders.Add(request.Headers.Authorization);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"message\":{\"role\":\"assistant\",\"content\":\"ok\"},\"done\":false}\n{\"done\":true}\n",
                    Encoding.UTF8,
                    "application/x-ndjson")
            };
        }
    }
}
