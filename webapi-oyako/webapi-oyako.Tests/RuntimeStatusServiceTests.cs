// Codex developer note: Explains the purpose and flow of webapi-oyako/webapi-oyako.Tests/RuntimeStatusServiceTests.cs for maintainers.
using webapi_oyako.Application.Services;
using Xunit;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Tests;

// Implements the RuntimeStatusServiceTests component and its responsibilities in the Oyako codebase.
public class RuntimeStatusServiceTests
{
    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task PublishAsync_UpdatesCurrentSnapshot()
    {
        // Creates the object needed for the next step of the workflow.
        var service = new RuntimeStatusService();

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await service.PublishAsync(
            "knowledge_redownload",
            "crawling",
            "crawling",
            4,
            9,
            false,
            "bilgiler alınıyor",
            "info",
            "search",
            12);

        // Verifies the expected behavior for this test scenario.
        Assert.Equal("knowledge_redownload", service.Current.Operation);
        // Verifies the expected behavior for this test scenario.
        Assert.Equal("crawling", service.Current.Phase);
        // Verifies the expected behavior for this test scenario.
        Assert.Equal("crawling", service.Current.StepKey);
        // Verifies the expected behavior for this test scenario.
        Assert.Equal(4, service.Current.StepIndex);
        // Verifies the expected behavior for this test scenario.
        Assert.Equal(9, service.Current.StepCount);
        // Verifies the expected behavior for this test scenario.
        Assert.False(service.Current.IsTerminal);
        // Verifies the expected behavior for this test scenario.
        Assert.Equal("bilgiler alınıyor", service.Current.Message);
        // Verifies the expected behavior for this test scenario.
        Assert.Equal(12, service.Current.PageCount);
    }
}
