// Codex developer note: Explains the purpose and flow of webapi-oyako/webapi-oyako.Tests/UrlNormalizerTests.cs for maintainers.
using webapi_oyako.Infrastructure.Crawling;
using Xunit;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Tests;

// Implements the UrlNormalizerTests component and its responsibilities in the Oyako codebase.
public class UrlNormalizerTests
{
    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public void TryNormalize_CanonicalizesSameHostLinks()
    {
        // Creates the object needed for the next step of the workflow.
        var baseUri = new Uri("https://tenantdemo.example");
        var ok = UrlNormalizer.TryNormalize(baseUri, "/Hakkimizda/", out var normalized);

        // Verifies the expected behavior for this test scenario.
        Assert.True(ok);
        // Verifies the expected behavior for this test scenario.
        Assert.Equal("https://tenantdemo.example/Hakkimizda", normalized);
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public void TryNormalize_RejectsExternalDomains()
    {
        // Creates the object needed for the next step of the workflow.
        var baseUri = new Uri("https://tenantdemo.example");
        var ok = UrlNormalizer.TryNormalize(baseUri, "https://google.com", out var normalized);

        // Verifies the expected behavior for this test scenario.
        Assert.False(ok);
        // Verifies the expected behavior for this test scenario.
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public void TryNormalize_AcceptsWwwAliasAndCanonicalizesToSeedHost()
    {
        // Creates the object needed for the next step of the workflow.
        var baseUri = new Uri("https://tenantdemo.example");
        // Creates the object needed for the next step of the workflow.
        var sourceUri = new Uri("https://www.tenantdemo.example/cozumler");
        var ok = UrlNormalizer.TryNormalize(baseUri, sourceUri, "/cozumler//kurumsal-uygulama/?x=1#top", out var normalized);

        // Verifies the expected behavior for this test scenario.
        Assert.True(ok);
        // Verifies the expected behavior for this test scenario.
        Assert.Equal("https://tenantdemo.example/cozumler/kurumsal-uygulama", normalized);
    }

    [Fact]
    public void TryNormalize_AcceptsSubdomainsInsideSourceDomain()
    {
        var baseUri = new Uri("https://www.charity.example");
        var sourceUri = new Uri("https://www.charity.example");
        var ok = UrlNormalizer.TryNormalize(baseUri, sourceUri, "https://donate.charity.example/tr/", out var normalized);

        Assert.True(ok);
        Assert.Equal("https://donate.charity.example/tr", normalized);
    }

    [Fact]
    public void TryNormalize_RejectsDifferentDomainsEvenWhenTheyAreLinkedBySource()
    {
        var baseUri = new Uri("https://www.charity.example");
        var sourceUri = new Uri("https://www.charity.example");
        var ok = UrlNormalizer.TryNormalize(baseUri, sourceUri, "https://kanver.org", out var normalized);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void TryNormalize_CanDisableSubdomainDiscovery()
    {
        var baseUri = new Uri("https://www.charity.example");
        var sourceUri = new Uri("https://www.charity.example");
        var ok = UrlNormalizer.TryNormalize(
            baseUri,
            sourceUri,
            "https://donate.charity.example/tr/",
            out var normalized,
            includeSubdomains: false);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public void TryNormalize_RejectsStaticAssetsByDefault()
    {
        // Creates the object needed for the next step of the workflow.
        var baseUri = new Uri("https://tenantdemo.example");
        var ok = UrlNormalizer.TryNormalize(baseUri, "/_next/static/app.js", out var normalized);

        // Verifies the expected behavior for this test scenario.
        Assert.False(ok);
        // Verifies the expected behavior for this test scenario.
        Assert.Equal(string.Empty, normalized);
    }
}
