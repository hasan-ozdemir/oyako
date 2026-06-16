// Codex developer note: Explains the purpose and flow of webapi-oyako/webapi-oyako.Tests/AzureAiClientTests.cs for maintainers.
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using webapi_oyako.Infrastructure.Configuration;
using webapi_oyako.Infrastructure.Llm;
using Xunit;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Tests;

// Implements the AzureAiClientTests component and its responsibilities in the Oyako codebase.
public class AzureAiClientTests
{
    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task CompleteChatAsync_ParsesAzureMessageContent()
    {
        var client = CreateClient("""
            {"choices":[{"message":{"role":"assistant","content":"Azure cevabı"}}]}
            """);

        var answer = await client.CompleteChatAsync("system", "user", CancellationToken.None);

        // Verifies the expected behavior for this test scenario.
        Assert.Equal("Azure cevabı", answer);
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task StreamChatAsync_ParsesAzureSseDeltaContent()
    {
        var client = CreateClient("""
            data: {"choices":[{"delta":{"content":"Merhaba "}}]}

            data: {"choices":[{"delta":{"content":"Oyako"}}]}

            data: [DONE]

            """);

        // Creates the object needed for the next step of the workflow.
        var chunks = new List<string>();
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await foreach (var chunk in client.StreamChatAsync("system", "user", CancellationToken.None))
        {
            // Registers or maps application behavior into the runtime pipeline.
            chunks.Add(chunk);
        }

        // Verifies the expected behavior for this test scenario.
        Assert.Equal(new[] { "Merhaba ", "Oyako" }, chunks);
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task StreamChatAsync_SendsOnlySystemAndCurrentUserMessage()
    {
        var handler = new CapturingHandler("""
            data: [DONE]

            """);
        var client = CreateClient(handler);

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await foreach (var _ in client.StreamChatAsync("system instruction", "tek soru", CancellationToken.None))
        {
        }

        // Verifies the expected behavior for this test scenario.
        Assert.Contains("\"role\":\"system\"", handler.RequestBody);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("\"content\":\"system instruction\"", handler.RequestBody);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("\"role\":\"user\"", handler.RequestBody);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("\"content\":\"tek soru\"", handler.RequestBody);
        // Verifies the expected behavior for this test scenario.
        Assert.DoesNotContain("\"role\":\"assistant\"", handler.RequestBody);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static AzureAiClient CreateClient(string responseContent)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return CreateClient(new CapturingHandler(responseContent));
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static AzureAiClient CreateClient(CapturingHandler handler)
    {
        var apiKeyEnvironmentVariable = $"azure_ai_api_key_test_{Guid.NewGuid():N}";
        // Creates the object needed for the next step of the workflow.
        var httpClient = new HttpClient(handler)
        {
            // Creates the object needed for the next step of the workflow.
            BaseAddress = new Uri("https://oyako-pr1-sc1.services.ai.azure.com/")
        };

        // Returns the computed result to the caller and completes this branch of the workflow.
        return new AzureAiClient(
            httpClient,
            // Creates the object needed for the next step of the workflow.
            Options.Create(new AzureAiOptions
            {
                Endpoint = "https://oyako-pr1-sc1.services.ai.azure.com/",
                DeploymentName = "DeepSeek-V4-Flash",
                ApiVersion = "2024-10-21",
                ApiKey = "test-key",
                ApiKeyEnvironmentVariable = apiKeyEnvironmentVariable
            }),
            // Creates the object needed for the next step of the workflow.
            new TestAiConfigurationService(activeProvider: "azure", azureModel: "DeepSeek-V4-Flash"));
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        // Stores state or a dependency required by the surrounding component.
        private readonly string _responseContent;

        // Executes this component behavior as part of the Oyako application flow.
        public CapturingHandler(string responseContent)
        {
            _responseContent = responseContent;
        }

        // Exposes data consumed by other layers while preserving the domain or DTO shape.
        public string RequestBody { get; private set; } = string.Empty;

        // Executes this component behavior as part of the Oyako application flow.
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            // Returns the computed result to the caller and completes this branch of the workflow.
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                // Creates the object needed for the next step of the workflow.
                Content = new StringContent(_responseContent, Encoding.UTF8, "application/json")
            };
        }
    }
}
