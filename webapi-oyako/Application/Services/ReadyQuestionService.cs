// Codex developer note: Explains the purpose and flow of webapi-oyako/Application/Services/ReadyQuestionService.cs for maintainers.
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Domain.Services;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Application.Services;

public sealed partial class ReadyQuestionService : IReadyQuestionService
{
    private const int TargetQuestionCount = 100;
    private const int MinimumGeneratedQuestionCount = 80;

    // Stores state or a dependency required by the surrounding component.
    private readonly IServiceScopeFactory _scopeFactory;
    // Stores state or a dependency required by the surrounding component.
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Creates a new instance and captures the dependencies needed by this component.
    public ReadyQuestionService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<ReadyQuestionSet> GetNextAsync(int count, CancellationToken cancellationToken)
    {
        var requestedCount = Math.Clamp(count <= 0 ? 4 : count, 1, 10);
        // Creates a disposable resource scoped to this operation.
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReadyQuestionRepository>();

        var questions = await repository.GetNextAsync(requestedCount, cancellationToken);
        // Guards the following branch so the workflow handles this condition deliberately.
        if (questions.Count == 0)
        {
            QueueRefreshFromKnowledge();
            // Returns the computed result to the caller and completes this branch of the workflow.
            return new ReadyQuestionSet(
                Array.Empty<string>(),
                "generated",
                null,
                0,
                null,
                true);
        }

        var metadata = await repository.GetMetadataAsync(cancellationToken);
        var selected = questions.Select(question => question.Text).ToList();

        // Returns the computed result to the caller and completes this branch of the workflow.
        return new ReadyQuestionSet(
            selected,
            "generated",
            questions.Max(question => question.CreatedAtUtc),
            metadata.TotalAvailable > 0 ? metadata.TotalAvailable : selected.Count,
            metadata.SourceFingerprint ?? questions.FirstOrDefault()?.SourceFingerprint,
            _gate.CurrentCount == 0);
    }

    // Executes this component behavior as part of the Oyako application flow.
    public void QueueRefreshFromKnowledge()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                await RefreshFromKnowledgeAsync(CancellationToken.None);
            }
            catch
            {
            }
        });
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<bool> RefreshFromKnowledgeAsync(CancellationToken cancellationToken)
    {
        var result = await RefreshFromKnowledgeCoreAsync(force: false, MinimumGeneratedQuestionCount, cancellationToken);
        // Returns the computed result to the caller and completes this branch of the workflow.
        return result.Succeeded;
    }

    // Executes this component behavior as part of the Oyako application flow.
    public async Task<ReadyQuestionRefreshResult> ForceRefreshFromKnowledgeAsync(CancellationToken cancellationToken)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return await RefreshFromKnowledgeCoreAsync(force: true, TargetQuestionCount, cancellationToken);
    }

    private async Task<ReadyQuestionRefreshResult> RefreshFromKnowledgeCoreAsync(
        bool force,
        int minimumQuestionCount,
        CancellationToken cancellationToken)
    {
        // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
        await _gate.WaitAsync(cancellationToken);
        try
        {
            // Creates a disposable resource scoped to this operation.
            using var scope = _scopeFactory.CreateScope();
            var webPageRepository = scope.ServiceProvider.GetRequiredService<IWebPageRepository>();
            var readyQuestionRepository = scope.ServiceProvider.GetRequiredService<IReadyQuestionRepository>();
            var aiChatClient = scope.ServiceProvider.GetRequiredService<IAiChatClient>();
            var runtimeStatusService = scope.ServiceProvider.GetRequiredService<IRuntimeStatusService>();

            var pages = await webPageRepository.GetAllPagesAsync(cancellationToken);
            // Guards the following branch so the workflow handles this condition deliberately.
            if (pages.Count == 0)
            {
                // Returns the computed result to the caller and completes this branch of the workflow.
                return new ReadyQuestionRefreshResult(false, 0, null);
            }

            var fingerprint = BuildFingerprint(pages);
            var existingCount = await readyQuestionRepository.CountAsync(cancellationToken);
            var existingFingerprint = await readyQuestionRepository.GetCurrentSourceFingerprintAsync(cancellationToken);
            // Guards the following branch so the workflow handles this condition deliberately.
            if (!force && existingCount >= MinimumGeneratedQuestionCount && string.Equals(existingFingerprint, fingerprint, StringComparison.Ordinal))
            {
                // Returns the computed result to the caller and completes this branch of the workflow.
                return new ReadyQuestionRefreshResult(false, existingCount, existingFingerprint);
            }

            var operation = force ? "knowledge_redownload" : "ready_questions";
            var buildStepIndex = force ? 7 : 1;
            var persistStepIndex = force ? 8 : 2;
            var completeStepIndex = force ? 9 : 3;
            var stepCount = force ? 9 : 3;

            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await runtimeStatusService.PublishAsync(
                operation,
                "ready_questions_building",
                "ready_questions_building",
                buildStepIndex,
                stepCount,
                false,
                "hazır sorular hazırlanıyor",
                "info",
                "refresh",
                pages.Count,
                cancellationToken);

            IReadOnlyList<ReadyQuestionCandidate> generatedQuestions;
            try
            {
                var response = await aiChatClient.CompleteChatAsync(
                    BuildSystemInstruction(),
                    BuildKnowledgePayload(pages),
                    cancellationToken);
                generatedQuestions = ParseQuestions(response, pages);
            }
            catch
            {
                generatedQuestions = Array.Empty<ReadyQuestionCandidate>();
            }

            var questions = CompleteQuestionsFromKnowledge(generatedQuestions, pages).Take(TargetQuestionCount).ToArray();

            // Guards the following branch so the workflow handles this condition deliberately.
            if (questions.Length < minimumQuestionCount)
            {
                // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                await runtimeStatusService.PublishAsync(
                    operation,
                    "ready_for_question",
                    "completed",
                    completeStepIndex,
                    stepCount,
                    true,
                    "Uygulama Hazır",
                    "ready",
                    "message",
                    pages.Count,
                    cancellationToken);
                // Returns the computed result to the caller and completes this branch of the workflow.
                return new ReadyQuestionRefreshResult(false, questions.Length, fingerprint);
            }

            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await runtimeStatusService.PublishAsync(
                operation,
                "ready_questions_persisting",
                "ready_questions_persisting",
                persistStepIndex,
                stepCount,
                false,
                "hazır sorular kaydediliyor",
                "info",
                "database",
                pages.Count,
                cancellationToken);
            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await readyQuestionRepository.ReplaceAllAsync(
                questions,
                fingerprint,
                DateTime.UtcNow,
                cancellationToken);

            // Guards the following branch so the workflow handles this condition deliberately.
            if (!force)
            {
                // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                await runtimeStatusService.PublishAsync(
                    operation,
                    "ready_for_question",
                    "completed",
                    completeStepIndex,
                    stepCount,
                    true,
                    "Uygulama Hazır",
                    "ready",
                    "message",
                    pages.Count,
                    cancellationToken);
            }

            // Returns the computed result to the caller and completes this branch of the workflow.
            return new ReadyQuestionRefreshResult(true, questions.Length, fingerprint);
        }
        catch
        {
            try
            {
                // Creates a disposable resource scoped to this operation.
                using var scope = _scopeFactory.CreateScope();
                var runtimeStatusService = scope.ServiceProvider.GetRequiredService<IRuntimeStatusService>();
                // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
                await runtimeStatusService.PublishAsync(
                    "ready_questions",
                    "ready_for_question",
                    "completed",
                    3,
                    3,
                    true,
                    "Uygulama Hazır",
                    "ready",
                    "message",
                    cancellationToken: CancellationToken.None);
            }
            catch
            {
            }

            // Returns the computed result to the caller and completes this branch of the workflow.
            return new ReadyQuestionRefreshResult(false, 0, null);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string BuildSystemInstruction()
    {
        return """
            Sen Oyako uygulaması için hazır soru üreticisisin.
            Yalnızca sana verilen etkin bilgi kaynağı ve belge içeriklerine dayanarak Türkçe hazır sorular üret.
            Dış bilgi, tahmin, halüsinasyon veya web içeriğinde olmayan konu kullanma.
            Tam olarak 100 adet kısa, anlaşılır, kullanıcıların etkin bilgi kaynakları hakkında sorabileceği soru üret.
            Her satırda sadece bir soru olsun ve her sorunun başında ilgili 1 ile 3 adet belge id bilgisini ver.
            Birden fazla belge id kullanırsan id değerlerini virgülle ayır.
            Format tam olarak şöyle olmalıdır: document_id_1,document_id_2|Soru metni?
            Çıktıda başlık, açıklama, markdown tablo, kategori, numaralandırma dışı metin veya kapanış mesajı verme.
            Kabul edilen biçimler: "123|Soru metni?" veya "123,456|Soru metni?"
            Sana verilmeyen belge id bilgilerini asla kullanma.
            Sorular tekrar etmemeli, farklı açılardan bilgi istemeli ve soru işaretiyle bitmelidir.
            """;
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string BuildKnowledgePayload(IReadOnlyList<WebPage> pages)
    {
        // Creates the object needed for the next step of the workflow.
        var text = new StringBuilder();
        text.AppendLine("Aşağıdaki etkin bilgi kaynağı içeriklerinden 100 adet hazır soru üret:");
        text.AppendLine();

        // Iterates through the collection to process each item consistently.
        foreach (var page in pages.OrderBy(page => page.WebSourceUrl, StringComparer.OrdinalIgnoreCase))
        {
            text.AppendLine($"[DocumentId] {page.Id}");
            text.AppendLine($"[SourceId] {page.SourceId ?? 0}");
            text.AppendLine($"[Web Source URL] {page.WebSourceUrl}");
            // Guards the following branch so the workflow handles this condition deliberately.
            if (!string.IsNullOrWhiteSpace(page.WebTitle))
            {
                text.AppendLine($"[Title] {page.WebTitle}");
            }

            text.AppendLine(page.WebContent);
            text.AppendLine("[/Web Source]");
            text.AppendLine();
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return text.ToString();
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static IReadOnlyList<ReadyQuestionCandidate> ParseQuestions(string response, IReadOnlyList<WebPage> pages)
    {
        // Creates the object needed for the next step of the workflow.
        var questions = new List<ReadyQuestionCandidate>();
        // Creates the object needed for the next step of the workflow.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pagesById = pages
            .Where(page => page.SourceId is not null)
            .ToDictionary(page => page.Id);

        // Iterates through the collection to process each item consistently.
        foreach (var line in response.Replace("\r\n", "\n").Split('\n'))
        {
            var match = QuestionLineRegex().Match(line);
            // Guards the following branch so the workflow handles this condition deliberately.
            if (!match.Success)
            {
                continue;
            }

            var references = match.Groups["documentIds"].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var documentId) ? documentId : 0)
                .Where(documentId => documentId > 0)
                .Distinct()
                .Select(documentId => pagesById.TryGetValue(documentId, out var page) && page.SourceId is not null
                    ? new ReadyQuestionDocumentReference(page.SourceId.Value, page.Id, page.ContentHash)
                    : null)
                .Where(reference => reference is not null)
                .Cast<ReadyQuestionDocumentReference>()
                .Take(3)
                .ToArray();
            if (references.Length == 0)
            {
                continue;
            }

            var question = NormalizeQuestion(match.Groups["question"].Value);
            // Guards the following branch so the workflow handles this condition deliberately.
            if (question.Length < 12 || !seen.Add(question))
            {
                continue;
            }

            // Registers or maps application behavior into the runtime pipeline.
            questions.Add(new ReadyQuestionCandidate(question, references));
            // Guards the following branch so the workflow handles this condition deliberately.
            if (questions.Count == TargetQuestionCount)
            {
                break;
            }
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return questions;
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static IReadOnlyList<ReadyQuestionCandidate> CompleteQuestionsFromKnowledge(IReadOnlyList<ReadyQuestionCandidate> generatedQuestions, IReadOnlyList<WebPage> pages)
    {
        // Creates the object needed for the next step of the workflow.
        var completed = new List<ReadyQuestionCandidate>();
        // Creates the object needed for the next step of the workflow.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Iterates through the collection to process each item consistently.
        foreach (var question in generatedQuestions)
        {
            AddQuestion(question);
        }

        // Iterates through the collection to process each item consistently.
        foreach (var page in pages.OrderBy(page => page.WebSourceUrl, StringComparer.OrdinalIgnoreCase))
        {
            var topic = BuildQuestionTopic(page);
            if (page.SourceId is null)
            {
                continue;
            }

            // Iterates through the collection to process each item consistently.
            foreach (var question in BuildSourceGroundedQuestions(topic))
            {
                AddQuestion(new ReadyQuestionCandidate(
                    question,
                    new[] { new ReadyQuestionDocumentReference(page.SourceId.Value, page.Id, page.ContentHash) }));
                // Guards the following branch so the workflow handles this condition deliberately.
                if (completed.Count == TargetQuestionCount)
                {
                    // Returns the computed result to the caller and completes this branch of the workflow.
                    return completed;
                }
            }
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return completed;

        void AddQuestion(ReadyQuestionCandidate value)
        {
            var normalized = NormalizeQuestion(value.Text);
            // Guards the following branch so the workflow handles this condition deliberately.
            if (normalized.Length < 12 || !seen.Add(normalized))
            {
                return;
            }

            // Registers or maps application behavior into the runtime pipeline.
            completed.Add(value with { Text = normalized });
        }
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string BuildQuestionTopic(WebPage page)
    {
        var title = string.IsNullOrWhiteSpace(page.WebTitle) ? BuildTitleFromUrl(page.WebSourceUrl) : page.WebTitle!;
        title = title.Replace("| OYAK Dijital", string.Empty, StringComparison.OrdinalIgnoreCase);
        title = title.Replace("OYAK Dijital", string.Empty, StringComparison.OrdinalIgnoreCase);
        title = WhitespaceRegex().Replace(title, " ").Trim(' ', '-', '|', ':');
        // Returns the computed result to the caller and completes this branch of the workflow.
        return string.IsNullOrWhiteSpace(title) ? "Bilgi kaynağı" : title;
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string BuildTitleFromUrl(string url)
    {
        // Guards the following branch so the workflow handles this condition deliberately.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return "Bilgi kaynağı";
        }

        var path = uri.AbsolutePath.Trim('/');
        // Guards the following branch so the workflow handles this condition deliberately.
        if (string.IsNullOrWhiteSpace(path))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return "Bilgi kaynağı";
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return string.Join(
            " ",
            path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Last()
                .Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static IEnumerable<string> BuildSourceGroundedQuestions(string topic)
    {
        yield return $"{topic} hakkında hangi bilgiler yer alıyor?";
        yield return $"{topic} kapsamında bilgi kaynağı ne anlatıyor?";
        yield return $"{topic} ile ilgili hangi hizmet veya çözümler öne çıkıyor?";
        yield return $"{topic} konusunda hangi süreçler, politikalar veya sorumluluklar belirtiliyor?";
        yield return $"{topic} hakkında kullanıcıların bilmesi gereken temel noktalar nelerdir?";
        yield return $"{topic} başlığında hangi ihtiyaçlara cevap veriliyor?";
        yield return $"{topic} sayfasındaki ana mesajlar nelerdir?";
        yield return $"{topic} ile ilgili hangi detaylar bilgi bankasında bulunuyor?";
        yield return $"{topic} hakkında hangi örnek sorular sorulabilir?";
        yield return $"{topic} içeriği bilgi kaynağını anlamak için nasıl kullanılabilir?";
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string NormalizeQuestion(string value)
    {
        var question = MarkdownLinkRegex().Replace(value, "$1");
        question = MarkdownEmphasisRegex().Replace(question, string.Empty);
        question = WhitespaceRegex().Replace(question, " ").Trim(' ', '"', '\'', '`', '-', '*', '.');
        // Returns the computed result to the caller and completes this branch of the workflow.
        return question.EndsWith("?", StringComparison.Ordinal) ? question : $"{question}?";
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string BuildFingerprint(IReadOnlyList<WebPage> pages)
    {
        var text = string.Join(
            "\n",
            pages
                .OrderBy(page => page.Id)
                .Select(page => $"{page.Id}|{page.ContentHash}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        // Returns the computed result to the caller and completes this branch of the workflow.
        return Convert.ToHexString(bytes);
    }

    [GeneratedRegex(@"^\s*(?:[-*+]\s+|\d+[\.)]\s+)?(?:document[_\s-]?ids?\s*[:=#]?\s*)?(?<documentIds>\d+(?:\s*,\s*\d+){0,2})\s*[|:\-–—]\s*(?<question>.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    // Executes this component behavior as part of the Oyako application flow.
    private static partial Regex QuestionLineRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)")]
    // Executes this component behavior as part of the Oyako application flow.
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"[*_~]")]
    // Executes this component behavior as part of the Oyako application flow.
    private static partial Regex MarkdownEmphasisRegex();

    [GeneratedRegex(@"\s+")]
    // Executes this component behavior as part of the Oyako application flow.
    private static partial Regex WhitespaceRegex();
}
