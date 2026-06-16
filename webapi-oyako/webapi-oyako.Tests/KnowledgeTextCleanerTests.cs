// Codex developer note: Explains the purpose and flow of webapi-oyako/webapi-oyako.Tests/KnowledgeTextCleanerTests.cs for maintainers.
using webapi_oyako.Application.Services;
using Xunit;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Tests;

// Implements the KnowledgeTextCleanerTests component and its responsibilities in the Oyako codebase.
public class KnowledgeTextCleanerTests
{
    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public void BuildPreview_RemovesRepeatedNavigationBoilerplate()
    {
        // Creates the object needed for the next step of the workflow.
        var cleaner = new KnowledgeTextCleaner();
        var preview = cleaner.BuildPreview("""
            Çözümler
            Teknolojilerimiz
            Hakkımızda
            Bize Ulaşın
            Dijital Dönüşüm ve Yapay Zekâ
            OYAK Dijital, kurumların dijital dönüşüm yolculuğunu hızlandıran çözümler geliştirir.
            © 2026 OYAK Dijital Tüm hakları saklıdır.
            """);

        // Verifies the expected behavior for this test scenario.
        // Verifies that the preview no longer starts with repeated navigation boilerplate.
        Assert.False(preview.StartsWith("Çözümler", StringComparison.Ordinal));
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("kurumların dijital dönüşüm", preview);
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public void Clean_RemovesClientSideErrorText()
    {
        // Creates the object needed for the next step of the workflow.
        var cleaner = new KnowledgeTextCleaner();
        var cleaned = cleaner.Clean("""
            Application error: a client-side exception has occurred while loading www.oyakdijital.com.tr.
            OYAK Dijital, teknoloji ve dijital dönüşümü odağına alan yenilikçi bir firmadır.
            """);

        // Verifies the expected behavior for this test scenario.
        Assert.DoesNotContain("Application error", cleaned);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains("teknoloji ve dijital dönüşümü", cleaned);
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public void BuildPreview_WhenContentIsShort_StillReturnsDocumentPreview()
    {
        var cleaner = new KnowledgeTextCleaner();
        var preview = cleaner.BuildPreview("Kısa ama geçerli belge içeriği.", "Kısa Belge");

        Assert.Contains("Kısa ama geçerli belge içeriği", preview);
        Assert.DoesNotContain("henüz oluşturulamadı", preview);
    }
}
