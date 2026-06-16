// Codex developer note: Defines verified source attribution links returned with assistant answers.
namespace webapi_oyako.Domain.Models;

// Describes one source document that the UI may render as an external link or DocumentViewer link.
public sealed record SourceAttribution(
    int SourceId,
    string SourceName,
    string SourceType,
    int DocumentId,
    string DocumentTitle,
    string DisplayLabel,
    string Url,
    string OpenMode);
