// Codex developer note: Explains the purpose and flow of webapi-oyako/webapi-oyako.Tests/ReadyQuestionServiceTests.cs for maintainers.
using Microsoft.Extensions.DependencyInjection;
using webapi_oyako.Application.Services;
using webapi_oyako.Domain.Entities;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Repositories;
using webapi_oyako.Domain.Services;
using Xunit;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Tests;

// Implements the ReadyQuestionServiceTests component and its responsibilities in the Oyako codebase.
public class ReadyQuestionServiceTests
{
    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task GetNextAsync_WhenRepositoryIsEmpty_ReturnsEmptyGeneratedSetAndQueuesRefresh()
    {
        var services = CreateServices(Array.Empty<WebPage>(), Array.Empty<ReadyQuestion>(), string.Empty);
        var service = services.GetRequiredService<IReadyQuestionService>();

        var result = await service.GetNextAsync(4, CancellationToken.None);

        // Verifies the expected behavior for this test scenario.
        Assert.Equal("generated", result.Source);
        // Verifies the expected behavior for this test scenario.
        Assert.Empty(result.Questions);
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task RefreshFromKnowledgeAsync_WhenGenerationSucceeds_ReplacesOldQuestions()
    {
        var pages = new[]
        {
            // Creates the object needed for the next step of the workflow.
            new WebPage
            {
                Id = 1,
                SourceId = 1,
                WebSourceUrl = "https://www.oyakdijital.com.tr",
                WebTitle = "Oyak Dijital",
                WebContent = "Oyak Dijital kurumsal uygulama hizmetleri sunar.",
                ContentHash = "hash-1"
            }
        };
        var oldQuestions = new[]
        {
            // Creates the object needed for the next step of the workflow.
            new ReadyQuestion { Text = "Eski soru?", SourceFingerprint = "old", CreatedAtUtc = DateTime.UtcNow }
        };
        var llmResponse = string.Join("\n", Enumerable.Range(1, 100).Select(i => $"1|Oyak Dijital hakkında {i} numaralı soru nedir?"));
        var services = CreateServices(pages, oldQuestions, llmResponse);
        var service = services.GetRequiredService<IReadyQuestionService>();
        var repository = (InMemoryReadyQuestionRepository)services.GetRequiredService<IReadyQuestionRepository>();

        var refreshed = await service.RefreshFromKnowledgeAsync(CancellationToken.None);

        // Verifies the expected behavior for this test scenario.
        Assert.True(refreshed);
        // Verifies the expected behavior for this test scenario.
        Assert.Equal(100, repository.Questions.Count);
        // Verifies the expected behavior for this test scenario.
        Assert.DoesNotContain(repository.Questions, question => question.Text == "Eski soru?");
        // Verifies the expected behavior for this test scenario.
        Assert.All(repository.Questions, question =>
        {
            var reference = Assert.Single(question.DocumentReferences);
            Assert.Equal(1, reference.SourceId);
            Assert.Equal(1, reference.DocumentId);
            Assert.Equal("hash-1", reference.DocumentContentHash);
        });
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task RefreshFromKnowledgeAsync_WhenLlmReturnsTooFewQuestions_CompletesQuestionsFromKnowledgeSources()
    {
        var pages = Enumerable.Range(1, 25)
            // Creates the object needed for the next step of the workflow.
            .Select(index => new WebPage
            {
                Id = index,
                SourceId = 1,
                WebSourceUrl = $"https://www.oyakdijital.com.tr/kaynak-{index}",
                WebTitle = $"Kaynak {index} | OYAK Dijital",
                WebContent = $"Kaynak {index} Oyak Dijital hizmet ve cozum bilgileri.",
                ContentHash = $"hash-{index}"
            })
            .ToArray();
        var oldQuestions = new[]
        {
            // Creates the object needed for the next step of the workflow.
            new ReadyQuestion { Text = "Eski soru?", SourceFingerprint = "old", CreatedAtUtc = DateTime.UtcNow }
        };
        var services = CreateServices(pages, oldQuestions, "1|Oyak Dijital kaynaklarından gelen ilk soru nedir?");
        var service = services.GetRequiredService<IReadyQuestionService>();
        var repository = (InMemoryReadyQuestionRepository)services.GetRequiredService<IReadyQuestionRepository>();

        var refreshed = await service.RefreshFromKnowledgeAsync(CancellationToken.None);

        // Verifies the expected behavior for this test scenario.
        Assert.True(refreshed);
        // Verifies the expected behavior for this test scenario.
        Assert.Equal(100, repository.Questions.Count);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains(repository.Questions, question => question.Text.Contains("Kaynak 1 hakkında", StringComparison.OrdinalIgnoreCase));
        // Verifies the expected behavior for this test scenario.
        Assert.DoesNotContain(repository.Questions, question => question.Text == "Eski soru?");
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task RefreshFromKnowledgeAsync_WhenAiQuestionGenerationFails_CompletesQuestionsFromKnowledgeSources()
    {
        var pages = Enumerable.Range(1, 25)
            // Creates the object needed for the next step of the workflow.
            .Select(index => new WebPage
            {
                Id = index,
                SourceId = 1,
                WebSourceUrl = $"https://www.oyakdijital.com.tr/belge-{index}",
                WebTitle = $"Belge {index} | OYAK Dijital",
                WebContent = $"Belge {index} Oyak Dijital bilgi kaynagi.",
                ContentHash = $"hash-{index}"
            })
            .ToArray();
        var oldQuestions = new[]
        {
            // Creates the object needed for the next step of the workflow.
            new ReadyQuestion { Text = "Eski soru?", SourceFingerprint = "old", CreatedAtUtc = DateTime.UtcNow }
        };
        var services = CreateServicesWithAiClient(pages, oldQuestions, new ThrowingAiChatClient());
        var service = services.GetRequiredService<IReadyQuestionService>();
        var repository = (InMemoryReadyQuestionRepository)services.GetRequiredService<IReadyQuestionRepository>();

        var refreshed = await service.RefreshFromKnowledgeAsync(CancellationToken.None);

        // Verifies the expected behavior for this test scenario.
        Assert.True(refreshed);
        // Verifies the expected behavior for this test scenario.
        Assert.Equal(100, repository.Questions.Count);
        // Verifies the expected behavior for this test scenario.
        Assert.Contains(repository.Questions, question => question.Text.Contains("Belge 1 hakkında", StringComparison.OrdinalIgnoreCase));
        // Verifies the expected behavior for this test scenario.
        Assert.DoesNotContain(repository.Questions, question => question.Text == "Eski soru?");
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task GetNextAsync_WhenGeneratedQuestionsExist_ReturnsOnlyGeneratedQuestions()
    {
        var generatedQuestions = new[]
        {
            // Creates the object needed for the next step of the workflow.
            new ReadyQuestion { Text = "Gerçek soru 1?", SourceFingerprint = "fingerprint", CreatedAtUtc = DateTime.UtcNow },
            // Creates the object needed for the next step of the workflow.
            new ReadyQuestion { Text = "Gerçek soru 2?", SourceFingerprint = "fingerprint", CreatedAtUtc = DateTime.UtcNow }
        };
        var services = CreateServices(Array.Empty<WebPage>(), generatedQuestions, string.Empty);
        var service = services.GetRequiredService<IReadyQuestionService>();

        var result = await service.GetNextAsync(4, CancellationToken.None);

        // Verifies the expected behavior for this test scenario.
        Assert.Equal("generated", result.Source);
        // Verifies the expected behavior for this test scenario.
        Assert.Equal(2, result.Questions.Count);
        // Verifies the expected behavior for this test scenario.
        Assert.All(result.Questions, question => Assert.StartsWith("Gerçek soru", question, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    // Executes this component behavior as part of the Oyako application flow.
    public async Task RefreshFromKnowledgeAsync_WhenGeneratedQuestionCountIsTooLow_PreservesOldQuestions()
    {
        var pages = new[]
        {
            // Creates the object needed for the next step of the workflow.
            new WebPage
            {
                Id = 1,
                SourceId = 1,
                WebSourceUrl = "https://www.oyakdijital.com.tr",
                WebTitle = "Oyak Dijital",
                WebContent = "Oyak Dijital kurumsal uygulama hizmetleri sunar.",
                ContentHash = "hash-1"
            }
        };
        var oldQuestions = new[]
        {
            // Creates the object needed for the next step of the workflow.
            new ReadyQuestion { Text = "Eski soru?", SourceFingerprint = "old", CreatedAtUtc = DateTime.UtcNow }
        };
        var services = CreateServices(pages, oldQuestions, "1|Yetersiz soru?");
        var service = services.GetRequiredService<IReadyQuestionService>();
        var repository = (InMemoryReadyQuestionRepository)services.GetRequiredService<IReadyQuestionRepository>();

        var refreshed = await service.RefreshFromKnowledgeAsync(CancellationToken.None);

        // Verifies the expected behavior for this test scenario.
        Assert.False(refreshed);
        // Verifies the expected behavior for this test scenario.
        Assert.Single(repository.Questions);
        // Verifies the expected behavior for this test scenario.
        Assert.Equal("Eski soru?", repository.Questions[0].Text);
    }

    private static ServiceProvider CreateServices(
        IReadOnlyList<WebPage> pages,
        IReadOnlyList<ReadyQuestion> questions,
        string llmResponse)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return CreateServicesWithAiClient(pages, questions, new StubAiChatClient(llmResponse));
    }

    private static ServiceProvider CreateServicesWithAiClient(
        IReadOnlyList<WebPage> pages,
        IReadOnlyList<ReadyQuestion> questions,
        IAiChatClient aiChatClient)
    {
        // Creates the object needed for the next step of the workflow.
        var services = new ServiceCollection();
        // Creates the object needed for the next step of the workflow.
        services.AddSingleton<IWebPageRepository>(new InMemoryWebPageRepository(pages));
        // Creates the object needed for the next step of the workflow.
        services.AddSingleton<IReadyQuestionRepository>(new InMemoryReadyQuestionRepository(questions));
        // Creates the object needed for the next step of the workflow.
        services.AddSingleton(aiChatClient);
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IRuntimeStatusService, RuntimeStatusService>();
        // Registers or maps application behavior into the runtime pipeline.
        services.AddSingleton<IReadyQuestionService, ReadyQuestionService>();
        // Returns the computed result to the caller and completes this branch of the workflow.
        return services.BuildServiceProvider();
    }

    private sealed class InMemoryReadyQuestionRepository : IReadyQuestionRepository
    {
        // Executes this component behavior as part of the Oyako application flow.
        public InMemoryReadyQuestionRepository(IReadOnlyList<ReadyQuestion> questions)
        {
            Questions = questions.ToList();
        }

        // Exposes data consumed by other layers while preserving the domain or DTO shape.
        public List<ReadyQuestion> Questions { get; private set; }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<int> CountAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(Questions.Count);
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<string?> GetCurrentSourceFingerprintAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(Questions.FirstOrDefault()?.SourceFingerprint);
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<ReadyQuestionMetadata> GetMetadataAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(new ReadyQuestionMetadata
            {
                TotalAvailable = Questions.Count,
                SourceFingerprint = Questions.FirstOrDefault()?.SourceFingerprint,
                GeneratedAtUtc = Questions.Count == 0 ? null : Questions.Max(question => question.CreatedAtUtc)
            });
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<IReadOnlyList<ReadyQuestion>> GetNextAsync(int count, CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult<IReadOnlyList<ReadyQuestion>>(Questions.Take(count).ToArray());
        }

        public Task ReplaceAllAsync(
            IReadOnlyList<ReadyQuestionCandidate> questions,
            string sourceFingerprint,
            DateTime createdAtUtc,
            CancellationToken cancellationToken)
        {
            Questions = questions
                // Creates the object needed for the next step of the workflow.
                .Select((question, index) => new ReadyQuestion
                {
                    Id = index + 1,
                    Text = question.Text,
                    DocumentReferences = question.DocumentReferences,
                    SourceFingerprint = sourceFingerprint,
                    CreatedAtUtc = createdAtUtc
                })
                .ToList();
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.CompletedTask;
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task ClearAsync(CancellationToken cancellationToken)
        {
            Questions.Clear();
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryWebPageRepository : IWebPageRepository
    {
        // Stores state or a dependency required by the surrounding component.
        private readonly IReadOnlyList<WebPage> _pages;

        // Executes this component behavior as part of the Oyako application flow.
        public InMemoryWebPageRepository(IReadOnlyList<WebPage> pages)
        {
            _pages = pages;
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        // Executes this component behavior as part of the Oyako application flow.
        public Task<IReadOnlyDictionary<string, WebPage>> GetAllPagesByUrlAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult<IReadOnlyDictionary<string, WebPage>>(
                _pages.ToDictionary(page => page.WebSourceUrl, StringComparer.OrdinalIgnoreCase));
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<IReadOnlyList<WebPage>> GetAllPagesAsync(CancellationToken cancellationToken) => Task.FromResult(_pages);

        // Executes this component behavior as part of the Oyako application flow.
        public Task<IReadOnlyList<WebPage>> GetKnowledgeSourcesAsync(CancellationToken cancellationToken) => Task.FromResult(_pages);

        // Executes this component behavior as part of the Oyako application flow.
        public Task UpsertPagesAsync(IReadOnlyCollection<WebPage> pages, CancellationToken cancellationToken) => Task.CompletedTask;

        // Executes this component behavior as part of the Oyako application flow.
        public Task DeleteByUrlsAsync(IReadOnlyCollection<string> urls, CancellationToken cancellationToken) => Task.CompletedTask;

        // Executes this component behavior as part of the Oyako application flow.
        public Task ClearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubAiChatClient : IAiChatClient
    {
        // Stores state or a dependency required by the surrounding component.
        private readonly string _response;

        // Executes this component behavior as part of the Oyako application flow.
        public StubAiChatClient(string response)
        {
            _response = response;
        }

        // Stores state or a dependency required by the surrounding component.
        public string ProviderName => "stub";

        public async IAsyncEnumerable<string> StreamChatAsync(
            string systemInstruction,
            string userMessage,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await Task.CompletedTask;
            yield break;
        }

        public Task<string> CompleteChatAsync(
            string systemInstruction,
            string userMessage,
            CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(_response);
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(true);
        }
    }

    private sealed class ThrowingAiChatClient : IAiChatClient
    {
        // Stores state or a dependency required by the surrounding component.
        public string ProviderName => "throwing";

        public async IAsyncEnumerable<string> StreamChatAsync(
            string systemInstruction,
            string userMessage,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await Task.CompletedTask;
            yield break;
        }

        public Task<string> CompleteChatAsync(
            string systemInstruction,
            string userMessage,
            CancellationToken cancellationToken)
        {
            // Stops the current workflow with an explicit failure that upstream handlers can report.
            throw new InvalidOperationException("AI unavailable for test.");
        }

        // Executes this component behavior as part of the Oyako application flow.
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return Task.FromResult(false);
        }
    }
}


