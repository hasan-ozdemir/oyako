// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Services/IKnowledgeRedownloadService.cs for maintainers.
using webapi_oyako.Domain.Models;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Domain.Services;

// Declares the IKnowledgeRedownloadService contract used to decouple Oyako layers.
public interface IKnowledgeRedownloadService
{
    // Redownloads every configured knowledge source and rebuilds active cache artifacts.
    Task<KnowledgeRedownloadResult> RedownloadAsync(CancellationToken cancellationToken);

    // Redownloads one configured knowledge source and rebuilds active cache artifacts.
    Task<KnowledgeRedownloadResult> RedownloadSourceAsync(int sourceId, CancellationToken cancellationToken);

    // Redownloads one knowledge document from its original backing source and rebuilds active cache artifacts.
    Task<KnowledgeRedownloadResult> RedownloadDocumentAsync(int documentId, CancellationToken cancellationToken);
}
