// Codex developer note: Defines the canonical knowledge source type values used across Oyako.
namespace webapi_oyako.Domain.Entities;

// Centralizes source type constants so API, crawler, repository, and UI contracts stay aligned.
public static class KnowledgeSourceTypes
{
    // Represents crawlable HTTP/HTTPS knowledge sources.
    public const string WebSite = "web_site";

    // Represents user-managed HTTP/HTTPS document links without automatic child-page discovery.
    public const string WebLinks = "web_links";

    // Represents user-managed local file collections.
    public const string LocalFiles = "local_files";
}
