// Codex developer note: Explains the purpose and flow of webapi-oyako/webapi-oyako.Tests/AnswerHtmlSanitizerTests.cs for maintainers.
using webapi_oyako.Application.Services;
using Xunit;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Tests;

// Implements the AnswerHtmlSanitizerTests component and its responsibilities in the Oyako codebase.
public class AnswerHtmlSanitizerTests
{
    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public void RenderAssistantMarkdown_RemovesUnsafeHtmlAndReturnsStructuredSuggestions()
    {
        // Creates the object needed for the next step of the workflow.
        var sanitizer = new AnswerHtmlSanitizer(new AnswerActionLinkifier());
        var markdown = """
            Tenant Demo hizmetleri.

            <script>alert(1)</script>
            <a href="javascript:alert(1)">kötü link</a>

            ## Önerilen sorular
            - Tenant Demo hangi hizmetleri sunar?
            """;

        var result = sanitizer.RenderAssistantMarkdown(markdown);

        // Verifies the expected behavior for this test scenario.
        Assert.DoesNotContain("<script", result.AnswerContent, StringComparison.OrdinalIgnoreCase);
        // Verifies the expected behavior for this test scenario.
        Assert.DoesNotContain("onclick", result.AnswerContent, StringComparison.OrdinalIgnoreCase);
        // Verifies the expected behavior for this test scenario.
        Assert.DoesNotContain("javascript:", result.AnswerContent, StringComparison.OrdinalIgnoreCase);
        // Verifies the expected behavior for this test scenario.
        Assert.DoesNotContain("data-oyako-question", result.AnswerContent, StringComparison.OrdinalIgnoreCase);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("Tenant Demo hizmetleri.", result.AnswerContent);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("Tenant Demo hangi hizmetleri sunar?", result.SuggestedQuestions);
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public void RenderAssistantMarkdown_ReturnsNoSuggestionsWhenAssistantMarkdownOmitsSuggestions()
    {
        // Creates the object needed for the next step of the workflow.
        var sanitizer = new AnswerHtmlSanitizer(new AnswerActionLinkifier());

        var result = sanitizer.RenderAssistantMarkdown("Yanıt metni.");

        // Verifies the expected behavior for this test scenario.
        Assert.Contains("Yanıt metni.", result.AnswerContent);
        // Verifies the expected behavior for this test scenario.
        Assert.Empty(result.SuggestedQuestions);
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public void RenderAssistantMarkdown_AutoLinksActionableContactData()
    {
        // Creates the object needed for the next step of the workflow.
        var sanitizer = new AnswerHtmlSanitizer(new AnswerActionLinkifier());
        var markdown = """
            İletişim bilgileri:

            - E-posta: iletisim@tenantdemo.com
            - Alternatif e-posta: bilgi@tenantdemo.com
            - Telefon: 0 (312) 444 15 52
            - SMS: 0532 111 22 33
            - WhatsApp: 0532 111 22 33
            - Web: tenantdemo.com/iletisim
            - **Adres (İstanbul):** YTÜ Yıldız Teknopark, Maslak Mah. Taşyoncası Sk. Maslak 1453 Yerleşkesi No: 1 G İç Kapı No: B1 Kat:1 34398 Sarıyer / İSTANBUL
            - Konum: 41.107, 29.019
            - LinkedIn: https://www.linkedin.com/company/oyak-dijital

            ## Önerilen sorular
            - Tenant Demo'e nasıl ulaşabilirim?
            """;

        var result = sanitizer.RenderAssistantMarkdown(markdown);

        // Verifies the expected behavior for this test scenario.
        Assert.Contains("href=\"mailto:iletisim@tenantdemo.com\"", result.AnswerContent);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("href=\"mailto:bilgi@tenantdemo.com\"", result.AnswerContent);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("href=\"tel:+903124441552\"", result.AnswerContent);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("href=\"sms:+905321112233\"", result.AnswerContent);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("href=\"https://wa.me/905321112233\"", result.AnswerContent);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("href=\"https://tenantdemo.com/iletisim\"", result.AnswerContent);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("href=\"https://www.google.com/maps/search/?api=1&amp;query=YT%C3%9C%20Y%C4%B1ld%C4%B1z%20Teknopark", result.AnswerContent);
        // Verifies the expected behavior for this test scenario.
        Assert.DoesNotContain("query=%28%C4%B0stanbul", result.AnswerContent);
        // Verifies the expected behavior for this test scenario.
        Assert.DoesNotContain("query=si%20No", result.AnswerContent);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("href=\"https://www.google.com/maps/search/?api=1&amp;query=41.107%2C%2029.019\"", result.AnswerContent);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("href=\"https://www.linkedin.com/company/oyak-dijital\"", result.AnswerContent);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("rel=\"noopener noreferrer\"", result.AnswerContent);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("Tenant Demo'e nasıl ulaşabilirim?", result.SuggestedQuestions);
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public void RenderAssistantMarkdown_CanDelayActionLinkificationDuringStreaming()
    {
        // Creates the object needed for the next step of the workflow.
        var sanitizer = new AnswerHtmlSanitizer(new AnswerActionLinkifier());
        var markdown = """
            - **Adres (İstanbul):** YTÜ Yıldız Teknopark, Maslak Mah. Taşyoncası Sk. Maslak 1453 Yerleşkesi No: 1 G İç Kapı No: B1 Kat:1 34398 Sarıyer / İSTANBUL
            - E-posta: iletisim@tenantdemo.com
            """;

        var streamingResult = sanitizer.RenderAssistantMarkdown(markdown, enableActionLinks: false);
        var finalResult = sanitizer.RenderAssistantMarkdown(markdown);

        // Verifies the expected behavior for this test scenario.
        Assert.DoesNotContain("https://www.google.com/maps/search/?api=1", streamingResult.AnswerContent);
        // Verifies the expected behavior for this test scenario.
        Assert.DoesNotContain("mailto:", streamingResult.AnswerContent);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("https://www.google.com/maps/search/?api=1", finalResult.AnswerContent);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("mailto:", finalResult.AnswerContent);
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public void RenderAssistantMarkdown_DoesNotDoubleLinkExistingMarkdownLinksOrUnsafeSchemes()
    {
        // Creates the object needed for the next step of the workflow.
        var sanitizer = new AnswerHtmlSanitizer(new AnswerActionLinkifier());
        var markdown = """
            [Tenant Demo](https://tenantdemo.com) bağlantısı korunur.

            <a href="javascript:alert(1)">zararlı link</a>
            """;

        var result = sanitizer.RenderAssistantMarkdown(markdown);

        // Verifies the expected behavior for this test scenario.
        Assert.Equal(1, CountOccurrences(result.AnswerContent, "href=\"https://tenantdemo.com\""));
        // Verifies the expected behavior for this test scenario.
        Assert.DoesNotContain("javascript:", result.AnswerContent, StringComparison.OrdinalIgnoreCase);
    }

    // Counts occurrences of a substring inside the supplied value.
    private static int CountOccurrences(string value, string expected)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(expected, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += expected.Length;
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return count;
    }
}
