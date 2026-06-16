// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Crawling/UrlNormalizer.cs for maintainers.
namespace webapi_oyako.Infrastructure.Crawling;

// Implements the UrlNormalizer component and its responsibilities in the Oyako codebase.
public static class UrlNormalizer
{
    private static readonly string[] SkippedExtensions =
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".css", ".js", ".ico", ".xml", ".pdf", ".zip", ".mp4", ".webm"
    };

    public static bool TryNormalize(
        Uri baseUri,
        string rawLink,
        out string normalizedUrl)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return TryNormalize(baseUri, baseUri, rawLink, out normalizedUrl);
    }

    public static bool TryNormalize(
        Uri siteRootUri,
        Uri sourceUri,
        string rawLink,
        out string normalizedUrl,
        bool allowNonHtmlDocument = false)
    {
        normalizedUrl = string.Empty;

        // Guards the following branch so the workflow handles this condition deliberately.
        if (string.IsNullOrWhiteSpace(rawLink))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return false;
        }

        rawLink = rawLink.Trim();
        // Guards the following branch so the workflow handles this condition deliberately.
        if (rawLink.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || rawLink.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)
            || rawLink.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || rawLink.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || rawLink.StartsWith("#", StringComparison.Ordinal))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return false;
        }

        Uri combined;
        try
        {
            // Creates the object needed for the next step of the workflow.
            combined = new Uri(sourceUri, rawLink);
        }
        catch
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return false;
        }

        // Guards the following branch so the workflow handles this condition deliberately.
        if (!AreEquivalentHosts(combined.Host, siteRootUri.Host))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return false;
        }

        // Guards the following branch so the workflow handles this condition deliberately.
        if (!string.Equals(combined.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return false;
        }

        var path = NormalizePath(combined.AbsolutePath);
        // Guards the following branch so the workflow handles this condition deliberately.
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/";
        }

        var extension = Path.GetExtension(path);
        // Guards the following branch so the workflow handles this condition deliberately.
        if (!allowNonHtmlDocument
            && !string.IsNullOrWhiteSpace(extension)
            && SkippedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return false;
        }

        normalizedUrl = $"{combined.Scheme.ToLowerInvariant()}://{RemoveWww(siteRootUri.Host).ToLowerInvariant()}{path}";
        normalizedUrl = Uri.UnescapeDataString(normalizedUrl);
        // Returns the computed result to the caller and completes this branch of the workflow.
        return true;
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string NormalizePath(string path)
    {
        var normalizedPath = path.Replace('\\', '/');
        while (normalizedPath.Contains("//", StringComparison.Ordinal))
        {
            normalizedPath = normalizedPath.Replace("//", "/", StringComparison.Ordinal);
        }

        normalizedPath = normalizedPath.TrimEnd('/');
        // Returns the computed result to the caller and completes this branch of the workflow.
        return string.IsNullOrWhiteSpace(normalizedPath) ? "/" : normalizedPath;
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static bool AreEquivalentHosts(string currentHost, string siteHost)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return string.Equals(RemoveWww(currentHost), RemoveWww(siteHost), StringComparison.OrdinalIgnoreCase);
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string RemoveWww(string host)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }
}
