// Codex developer note: Explains the purpose and flow of webapi-oyako/Application/Services/KnowledgeTextCleaner.cs for maintainers.
using System.Text.RegularExpressions;
using webapi_oyako.Domain.Services;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Application.Services;

// Implements the KnowledgeTextCleaner component and its responsibilities in the Oyako codebase.
public sealed class KnowledgeTextCleaner : IKnowledgeTextCleaner
{
    // Executes this component behavior as part of the Oyako application flow.
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex LeadingBoilerplateRegex = new(
        @"^(?:(?:Çözümler|Teknolojilerimiz|Hakkımızda|Bize Ulaşın|Referanslarımız|Politikalarımız|OYAK Dijital|Anasayfa|Keşfet|İletişim|Yasal Uyarı|Kişisel Verilerin Korunması|Bilgi Güvenliği Politikası|Hizmet Yönetim Politikası|İş Sürekliliği Politikası|Yapay Zeka Yönetim Sistemi Politikası|Aydınlatma Metni|Çerez Politikası)\s*)+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] ExactBoilerplateLines =
    [
        "çözümler",
        "teknolojilerimiz",
        "hakkımızda",
        "bize ulaşın",
        "referanslarımız",
        "politikalarımız",
        "oyak dijital",
        "yasal uyarı",
        "kişisel verilerin korunması",
        "bilgi güvenliği politikası",
        "hizmet yönetim politikası",
        "iş sürekliliği politikası",
        "yapay zeka yönetim sistemi politikası",
        "aydınlatma metni",
        "çerez politikası",
        "anasayfa",
        "keşfet",
        "iletişim",
        "© 2026 oyak dijital tüm hakları saklıdır."
    ];

    // Executes this component behavior as part of the Oyako application flow.
    public string Clean(string text)
    {
        // Guards the following branch so the workflow handles this condition deliberately.
        if (string.IsNullOrWhiteSpace(text))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return string.Empty;
        }

        var lines = text
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(NormalizeLine)
            .Where(line => line.Length > 0)
            .Select(RemoveLeadingBoilerplate)
            .Where(line => line.Length > 0)
            .Where(line => !IsBoilerplateLine(line))
            .Where(line => !IsClientErrorLine(line))
            .ToList();

        // Creates the object needed for the next step of the workflow.
        var result = new List<string>(lines.Count);
        // Creates the object needed for the next step of the workflow.
        var seenShortLines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Iterates through the collection to process each item consistently.
        foreach (var line in lines)
        {
            // Guards the following branch so the workflow handles this condition deliberately.
            if (line.Length < 80 && !seenShortLines.Add(line))
            {
                continue;
            }

            // Registers or maps application behavior into the runtime pipeline.
            result.Add(line);
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return string.Join("\n", result);
    }

    // Executes this component behavior as part of the Oyako application flow.
    public string BuildPreview(string text, string? title = null, int maxLength = 220)
    {
        var max = Math.Clamp(maxLength, 80, 500);
        var cleaned = Clean(text);
        // Guards the following branch so the workflow handles this condition deliberately.
        if (cleaned.Length == 0)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return "Bu kaynak için temiz önizleme henüz oluşturulamadı.";
        }

        var titleText = NormalizeLine(title ?? string.Empty);
        var lines = cleaned
            .Split('\n')
            .Select(NormalizeLine)
            .Where(IsUsefulPreviewLine)
            .ToList();

        if (lines.Count == 0)
        {
            lines = cleaned
                .Split('\n')
                .Select(NormalizeLine)
                .Where(line => line.Length > 0 && !IsBoilerplateLine(line) && !IsClientErrorLine(line))
                .ToList();
        }

        var preview = RemoveLeadingBoilerplate(string.Join(" ", lines));
        // Guards the following branch so the workflow handles this condition deliberately.
        if (preview.Length == 0)
        {
            preview = RemoveLeadingBoilerplate(NormalizeLine(cleaned));
        }

        if (preview.Length == 0 && titleText.Length > 0)
        {
            preview = titleText;
        }

        if (preview.Length == 0)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return "Bu kaynak için temiz önizleme henüz oluşturulamadı.";
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return TrimToLength(preview, max);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string NormalizeLine(string value)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return WhitespaceRegex.Replace(value, " ").Trim();
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string RemoveLeadingBoilerplate(string value)
    {
        var current = NormalizeLine(value);
        string next;
        do
        {
            next = LeadingBoilerplateRegex.Replace(current, string.Empty).Trim();
            // Guards the following branch so the workflow handles this condition deliberately.
            if (string.Equals(next, current, StringComparison.Ordinal))
            {
                // Returns the computed result to the caller and completes this branch of the workflow.
                return next;
            }

            current = next;
        }
        while (current.Length > 0);

        // Returns the computed result to the caller and completes this branch of the workflow.
        return current;
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static bool IsBoilerplateLine(string line)
    {
        var normalized = NormalizeLine(line).ToLowerInvariant();
        // Returns the computed result to the caller and completes this branch of the workflow.
        return ExactBoilerplateLines.Contains(normalized)
            || normalized.StartsWith("© ", StringComparison.Ordinal)
            || normalized.StartsWith("tüm hakları saklıdır", StringComparison.Ordinal)
            || normalized.StartsWith("bu sitedeki deneyiminizi çerezlere izin vererek", StringComparison.Ordinal)
            || normalized is "kurumsal uygulama" or "dijital dönüşüm ve yapay zeka" or "dijital dönüşüm ve yapay zekâ"
                or "yönetilen hizmetler" or "bulut ve teknoloji çözümleri";
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static bool IsClientErrorLine(string line)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return line.Contains("Application error:", StringComparison.OrdinalIgnoreCase)
            || line.Contains("a client-side exception has occurred", StringComparison.OrdinalIgnoreCase)
            || line.Contains("browser console for more information", StringComparison.OrdinalIgnoreCase);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static bool IsUsefulPreviewLine(string line)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return line.Length >= 24
            && !IsBoilerplateLine(line)
            && !IsClientErrorLine(line);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string TrimToLength(string value, int maxLength)
    {
        // Guards the following branch so the workflow handles this condition deliberately.
        if (value.Length <= maxLength)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return value;
        }

        var cut = value.LastIndexOf(' ', Math.Min(maxLength, value.Length - 1));
        // Guards the following branch so the workflow handles this condition deliberately.
        if (cut < 80)
        {
            cut = maxLength;
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return $"{value[..cut].TrimEnd()}...";
    }
}
