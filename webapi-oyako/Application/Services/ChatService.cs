// Codex developer note: Explains the purpose and flow of webapi-oyako/Application/Services/ChatService.cs for maintainers.
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Domain.Services;
using System.Globalization;
using System.Text;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Application.Services;

// Implements the ChatService component and its responsibilities in the Oyako codebase.
public sealed class ChatService : IChatService
{
    private const int MaxQuestionPromptBlocks = 12;
    private const int MaxQuestionPromptCharacters = 60000;
    // Stores state or a dependency required by the surrounding component.
    private readonly ISystemInstructionCache _systemInstructionCache;
    // Stores state or a dependency required by the surrounding component.
    private readonly IChatPromptBuilder _chatPromptBuilder;
    // Stores state or a dependency required by the surrounding component.
    private readonly IAiChatClient _aiChatClient;
    // Stores state or a dependency required by the surrounding component.
    private readonly IWebPageRepository _webPageRepository;
    // Stores state or a dependency required by the surrounding component.
    private readonly IAnswerHtmlSanitizer _answerHtmlSanitizer;
    // Stores state or a dependency required by the surrounding component.
    private readonly IQnaExperienceSettingsService _qnaExperienceSettingsService;
    // Stores state or a dependency required by the surrounding component.
    private readonly IRuntimeStatusService _runtimeStatusService;

    public ChatService(
        ISystemInstructionCache systemInstructionCache,
        IChatPromptBuilder chatPromptBuilder,
        IAiChatClient aiChatClient,
        IWebPageRepository webPageRepository,
        IAnswerHtmlSanitizer answerHtmlSanitizer,
        IQnaExperienceSettingsService qnaExperienceSettingsService,
        IRuntimeStatusService runtimeStatusService)
    {
        _systemInstructionCache = systemInstructionCache;
        _chatPromptBuilder = chatPromptBuilder;
        _aiChatClient = aiChatClient;
        _webPageRepository = webPageRepository;
        _answerHtmlSanitizer = answerHtmlSanitizer;
        _qnaExperienceSettingsService = qnaExperienceSettingsService;
        _runtimeStatusService = runtimeStatusService;
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<string> BuildSystemPromptAsync(CancellationToken cancellationToken)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return await _systemInstructionCache.GetCurrentAsync(cancellationToken);
    }

    public async IAsyncEnumerable<ChatAnswerSnapshot> StreamAnswerAsync(
        string userMessage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await _runtimeStatusService.PublishAsync(
            "chat",
            "asking",
            "asking",
            1,
            3,
            false,
            "soru soruluyor",
            "info",
            "send",
            cancellationToken: cancellationToken);

        var qnaSettings = await _qnaExperienceSettingsService.GetAsync(cancellationToken);
        var prompt = BuildRuntimePrompt(
            await BuildQuestionScopedSystemPromptAsync(userMessage, cancellationToken),
            qnaSettings);
        // Creates the object needed for the next step of the workflow.
        var rawAnswer = new System.Text.StringBuilder();
        var lastFlushAt = DateTime.UtcNow;
        var hasPublishedAnswering = false;
        var lastAnswerContent = string.Empty;

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        var runtimeUserMessage = BuildRuntimeUserMessage(userMessage, qnaSettings);
        await foreach (var token in _aiChatClient.StreamChatAsync(prompt, runtimeUserMessage, cancellationToken))
        {
            rawAnswer.Append(token);

            // Guards the following branch so the workflow handles this condition deliberately.
            if (!hasPublishedAnswering)
            {
                hasPublishedAnswering = true;
                // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                await _runtimeStatusService.PublishAsync(
                    "chat",
                    "answering",
                    "answering",
                    2,
                    3,
                    false,
                    "cevap veriliyor",
                    "info",
                    "sparkles",
                    cancellationToken: cancellationToken);
            }

            // Guards the following branch so the workflow handles this condition deliberately.
            if ((DateTime.UtcNow - lastFlushAt).TotalMilliseconds < 140)
            {
                continue;
            }

            lastFlushAt = DateTime.UtcNow;
            var snapshot = _answerHtmlSanitizer.RenderAssistantMarkdown(
                rawAnswer.ToString(),
                qnaSettings.DisplayedSuggestedQuestionCount,
                enableActionLinks: false);
            lastAnswerContent = snapshot.AnswerContent;
            yield return snapshot;
        }

        var rawMarkdown = rawAnswer.ToString();
        IReadOnlyList<SourceAttribution> attributions = Array.Empty<SourceAttribution>();
        if (qnaSettings.ShowAnswerSourceDocumentNames)
        {
            attributions = await ResolveSourceAttributionsAsync(rawMarkdown, cancellationToken);
        }

        var finalSnapshot = _answerHtmlSanitizer.RenderAssistantMarkdown(
            rawMarkdown,
            qnaSettings.DisplayedSuggestedQuestionCount) with
        {
            SourceAttributions = attributions
        };
        // Guards the following branch so the workflow handles this condition deliberately.
        if (!string.Equals(finalSnapshot.AnswerContent, lastAnswerContent, StringComparison.Ordinal)
            || finalSnapshot.SuggestedQuestions.Count > 0
            || finalSnapshot.SourceAttributions.Count > 0)
        {
            yield return finalSnapshot;
        }

        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await _runtimeStatusService.PublishAsync(
            "chat",
            "ready_for_question",
            "completed",
            3,
            3,
            true,
            "Uygulama Hazır",
            "ready",
            "message",
            cancellationToken: cancellationToken);
    }

    // Adds request-scoped Q&A display preferences without rebuilding the knowledge cache.
    private static string BuildRuntimePrompt(string basePrompt, QnaExperienceSettingsSnapshot settings)
    {
        var sourceInstruction = settings.ShowAnswerSourceDocumentNames
            ? "Bu istekte cevap gövdesinden hemen sonra ve '## Önerilen sorular' başlığından hemen önce tek bir Kaynak satırı üret. Cevap aktif bilgi belgelerinden gelen bir bilgiye dayanıyorsa yalnızca system instruction içindeki [CitationLabel], [SourceName] ve [DocumentTitle] değerlerini kullan. Format: Kaynak: Kaynak İsmi - Belge Başlığı; Belge Başlığı. Belge başlığı uydurma, URL yazma, markdown link yazma."
            : "Bu istekte cevap gövdesinde Kaynak satırı, kaynak belge adı veya kaynak bağlantısı yazma.";
        return $"""
            {basePrompt}

            [Bu isteğe özel Oyako kullanıcı deneyimi ayarları]
            Önerilen sorular bölümünde en fazla {settings.DisplayedSuggestedQuestionCount} soru ver.
            Cevap gövdesinde e-posta, telefon, SMS, WhatsApp, web sitesi, web sayfası, adres, konum, koordinat veya açık sosyal medya/platform URL bilgisi varsa kompakt markdown link ver: [metin](mailto:...), [metin](tel:+...), [metin](sms:+...), [metin](https://wa.me/...), [metin](https://...), [tam adres](https://www.google.com/maps/search/?api=1&query=...). Yeni iletişim bilgisi uydurma.
            {sourceInstruction}
            [/Bu isteğe özel Oyako kullanıcı deneyimi ayarları]
            """;
    }

    private async Task<string> BuildQuestionScopedSystemPromptAsync(string userMessage, CancellationToken cancellationToken)
    {
        var blocks = await _webPageRepository.GetActiveDocumentCacheBlocksAsync(cancellationToken);
        if (blocks.Count == 0)
        {
            return await _systemInstructionCache.GetCurrentAsync(cancellationToken);
        }

        var selectedBlocks = SelectRelevantBlocks(userMessage, blocks);
        return _chatPromptBuilder.BuildSystemPrompt(selectedBlocks);
    }

    private static IReadOnlyList<KnowledgeDocumentCacheBlock> SelectRelevantBlocks(
        string userMessage,
        IReadOnlyList<KnowledgeDocumentCacheBlock> blocks)
    {
        var terms = BuildSearchTerms(userMessage);
        if (terms.Count == 0)
        {
            return Array.Empty<KnowledgeDocumentCacheBlock>();
        }

        var selected = new List<KnowledgeDocumentCacheBlock>();
        var characterBudget = 0;
        foreach (var candidate in blocks
            .Select(block => new
            {
                Block = block,
                Score = ScoreBlock(block, terms)
            })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Block.DocumentTitle, StringComparer.OrdinalIgnoreCase))
        {
            if (selected.Count >= MaxQuestionPromptBlocks)
            {
                break;
            }

            var nextBudget = characterBudget + candidate.Block.PromptBlock.Length;
            if (selected.Count > 0 && nextBudget > MaxQuestionPromptCharacters)
            {
                break;
            }

            selected.Add(candidate.Block);
            characterBudget = nextBudget;
        }

        return selected;
    }

    private static int ScoreBlock(KnowledgeDocumentCacheBlock block, IReadOnlySet<string> terms)
    {
        var titleAndUrl = NormalizeSearchText($"{block.SourceName} {block.DocumentTitle} {block.DocumentUrl}");
        var content = NormalizeSearchText(block.PromptBlock);
        var score = 0;
        foreach (var term in terms)
        {
            if (titleAndUrl.Contains(term, StringComparison.Ordinal))
            {
                score += 12;
            }

            if (content.Contains(term, StringComparison.Ordinal))
            {
                score += 2;
            }
        }

        return score;
    }

    private static IReadOnlySet<string> BuildSearchTerms(string userMessage)
    {
        var terms = NormalizeSearchText(userMessage)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length >= 3 || term.All(char.IsDigit))
            .ToHashSet(StringComparer.Ordinal);

        if (terms.Any(term => term.StartsWith("generic-tenant", StringComparison.Ordinal)))
        {
            terms.Add("generic-tenant");
        }

        if (terms.Contains("bagis") || terms.Contains("bagisi") || terms.Contains("bagisim") || terms.Contains("bagiscilar"))
        {
            foreach (var donationTerm in new[] { "bagis", "bagis-yontemleri", "online", "sms", "banka", "168", "ptt", "sube", "mobil", "kripto", "yurt", "iban" })
            {
                terms.Add(donationTerm);
            }
        }

        return terms;
    }

    private static string NormalizeSearchText(string value)
    {
        var mapped = value
            .Replace('ı', 'i')
            .Replace('İ', 'i')
            .Replace('ş', 's')
            .Replace('Ş', 's')
            .Replace('ğ', 'g')
            .Replace('Ğ', 'g')
            .Replace('ü', 'u')
            .Replace('Ü', 'u')
            .Replace('ö', 'o')
            .Replace('Ö', 'o')
            .Replace('ç', 'c')
            .Replace('Ç', 'c')
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(mapped.Length);
        foreach (var character in mapped)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // Wraps the user question with a final per-request response contract that the LLM sees after the raw question.
    private static string BuildRuntimeUserMessage(string userMessage, QnaExperienceSettingsSnapshot settings)
    {
        var sourceContract = settings.ShowAnswerSourceDocumentNames
            ? "Kaynak satırı zorunludur; bu satırı yazmadan cevabı bitirme. Kaynak satırı tam olarak 'Kaynak:' ile başlamalıdır. Aktif belgelerden cevap verdiysen kullanılan belge adlarını system instruction içindeki canonical [CitationLabel] değerlerinden birebir türet."
            : "Kaynak satırı, kaynak belge adı veya kaynak bağlantısı yazma.";
        return $"""
            [Kullanıcı sorusu]
            {userMessage}
            [/Kullanıcı sorusu]

            [Zorunlu yanıt sözleşmesi]
            Sadece [Kullanıcı sorusu] bölümündeki soruyu cevapla.
            Yanıt sırası kesin olarak şöyledir: cevap gövdesi, kaynak görünürlüğü açıksa tek 'Kaynak: ...' satırı, en sonda '## Önerilen sorular' başlığı.
            Cevap gövdesindeki iletişim/action bilgilerini yalnızca mailto, tel, sms veya https markdown linkleriyle ifade et; custom uygulama scheme'i veya script benzeri link üretme.
            {sourceContract}
            '## Önerilen sorular' başlığı altında en fazla {settings.DisplayedSuggestedQuestionCount} adet doğal Türkçe soru ver.
            [/Zorunlu yanıt sözleşmesi]
            """;
    }

    // Resolves the LLM source line against active DB-backed citation metadata.
    private async Task<IReadOnlyList<SourceAttribution>> ResolveSourceAttributionsAsync(string markdown, CancellationToken cancellationToken)
    {
        var sourceLine = ExtractSourceLine(markdown);
        if (string.IsNullOrWhiteSpace(sourceLine))
        {
            return Array.Empty<SourceAttribution>();
        }

        var blocks = await _webPageRepository.GetActiveDocumentCacheBlocksAsync(cancellationToken);
        if (blocks.Count == 0)
        {
            return Array.Empty<SourceAttribution>();
        }

        var result = new List<SourceAttribution>();
        var seen = new HashSet<int>();
        var seenCitationLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddExactCitationMatches(sourceLine, blocks, result, seen, seenCitationLabels);
        foreach (var sourceGroup in sourceLine.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = sourceGroup.IndexOf(" - ", StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var sourceName = sourceGroup[..separatorIndex].Trim();
            var documentNames = sourceGroup[(separatorIndex + 3)..]
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var documentName in documentNames)
            {
                var match = blocks.FirstOrDefault(block =>
                    TextMatches(block.SourceName, sourceName)
                    && (TextMatches(block.DocumentTitle, documentName)
                        || TextMatches(block.DocumentCitationLabel, $"{sourceName} - {documentName}")));
                if (match is null)
                {
                    continue;
                }

                AddAttributionIfNew(match, result, seen, seenCitationLabels);
            }
        }

        return result;
    }

    // Captures exact canonical citation labels before delimiter parsing so document titles containing "|" still work.
    private static void AddExactCitationMatches(
        string sourceLine,
        IReadOnlyList<KnowledgeDocumentCacheBlock> blocks,
        List<SourceAttribution> result,
        HashSet<int> seen,
        HashSet<string> seenCitationLabels)
    {
        var normalizedSourceLine = NormalizeCitationText(sourceLine);
        foreach (var block in blocks.OrderByDescending(block => block.DocumentCitationLabel.Length))
        {
            var normalizedCitationLabel = NormalizeCitationText(block.DocumentCitationLabel);
            if (string.IsNullOrWhiteSpace(normalizedCitationLabel)
                || !normalizedSourceLine.Contains(normalizedCitationLabel, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AddAttributionIfNew(block, result, seen, seenCitationLabels);
        }
    }

    // Adds one verified source attribution while avoiding duplicate visible citation labels.
    private static void AddAttributionIfNew(
        KnowledgeDocumentCacheBlock block,
        List<SourceAttribution> result,
        HashSet<int> seen,
        HashSet<string> seenCitationLabels)
    {
        var normalizedCitationLabel = NormalizeCitationText(block.DocumentCitationLabel);
        if (!seen.Add(block.DocumentId)
            || !seenCitationLabels.Add(normalizedCitationLabel))
        {
            return;
        }

        result.Add(ToSourceAttribution(block));
    }

    // Converts a verified cache block into the UI-ready attribution shape.
    private static SourceAttribution ToSourceAttribution(KnowledgeDocumentCacheBlock block)
    {
        return new SourceAttribution(
            block.SourceId,
            block.SourceName,
            block.SourceType,
            block.DocumentId,
            block.DocumentTitle,
            block.DocumentTitle,
            block.DocumentUrl,
            block.SourceType.Equals("local_files", StringComparison.OrdinalIgnoreCase) ? "document_viewer" : "external");
    }

    // Extracts the final Kaynak line before the suggested questions heading.
    private static string ExtractSourceLine(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n').ToList();
        var headingIndex = lines.FindIndex(line => line.Trim().StartsWith("## Önerilen sorular", StringComparison.OrdinalIgnoreCase)
            || line.Trim().StartsWith("## Onerilen sorular", StringComparison.OrdinalIgnoreCase));
        var bodyLines = headingIndex >= 0 ? lines.Take(headingIndex).ToList() : lines;
        for (var index = bodyLines.Count - 1; index >= 0; index--)
        {
            var line = bodyLines[index]
                .Trim()
                .Replace("**Kaynak:**", "Kaynak:", StringComparison.OrdinalIgnoreCase)
                .Replace("__Kaynak:__", "Kaynak:", StringComparison.OrdinalIgnoreCase)
                .Replace("**Kaynak**:", "Kaynak:", StringComparison.OrdinalIgnoreCase)
                .Replace("__Kaynak__:", "Kaynak:", StringComparison.OrdinalIgnoreCase)
                .Trim('*', '_')
                .Trim();
            if (!line.StartsWith("Kaynak:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return line["Kaynak:".Length..].Trim().Trim('*', '_').Trim();
        }

        return string.Empty;
    }

    // Compares source and document labels after whitespace and punctuation normalization.
    private static bool TextMatches(string left, string right)
    {
        return string.Equals(NormalizeCitationText(left), NormalizeCitationText(right), StringComparison.OrdinalIgnoreCase);
    }

    // Normalizes text so LLM punctuation variance does not break safe DB attribution matching.
    private static string NormalizeCitationText(string value)
    {
        var chars = value
            .Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
            .ToArray();
        return string.Join(" ", new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}
