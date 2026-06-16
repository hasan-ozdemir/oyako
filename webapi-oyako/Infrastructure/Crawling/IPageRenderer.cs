// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Crawling/IPageRenderer.cs for maintainers.
namespace webapi_oyako.Infrastructure.Crawling;

// Declares the IPageRenderer contract used to decouple Oyako layers.
public interface IPageRenderer
{
    Task<RenderedPage> RenderAsync(string url, CancellationToken cancellationToken);
}
