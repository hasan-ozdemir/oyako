// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Configuration/EnvFileLoader.cs for maintainers.
namespace webapi_oyako.Infrastructure.Configuration;

// Implements the EnvFileLoader component and its responsibilities in the Oyako codebase.
public static class EnvFileLoader
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["domain_only_crawling"] = "Crawler__DomainOnlyCrawling",
        ["web_document_max_count"] = "Crawler__MaxPagesToCrawl",
        ["web_document_max_depth"] = "Crawler__MaxDepth",
        ["ai_default_provider"] = "Ai__DefaultProvider",
        ["ai_fallback_provider"] = "Ai__FallbackProviders__0"
    };

    // Loads each explicitly supported environment file and keeps the last file as the highest-precedence file.
    public static void LoadMany(IEnumerable<string> fileNames, string contentRootPath)
    {
        foreach (var fileName in fileNames)
        {
            Load(fileName, contentRootPath);
        }
    }

    // Executes this component behavior as part of the Oyako application flow.
    public static void Load(string fileName, string contentRootPath)
    {
        var filePath = FindEnvFile(fileName, contentRootPath);
        // Guards the following branch so the workflow handles this condition deliberately.
        if (filePath is null)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return;
        }

        // Iterates through the collection to process each item consistently.
        foreach (var rawLine in File.ReadAllLines(filePath))
        {
            var line = rawLine.Trim();
            // Guards the following branch so the workflow handles this condition deliberately.
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            // Guards the following branch so the workflow handles this condition deliberately.
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"');
            // Guards the following branch so the workflow handles this condition deliberately.
            if (key.Length == 0)
            {
                continue;
            }

            SetEnvironmentValue(key, value);
        }
    }

    private static void SetEnvironmentValue(string key, string value)
    {
        Environment.SetEnvironmentVariable(key, value);
        if (!Aliases.TryGetValue(key, out var mappedKey))
        {
            return;
        }

        Environment.SetEnvironmentVariable(mappedKey, NormalizeAliasValue(mappedKey, value));
    }

    private static string NormalizeAliasValue(string mappedKey, string value)
    {
        if (!mappedKey.StartsWith("Ai__", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return value.Equals("azure-cloud", StringComparison.OrdinalIgnoreCase) ? "azure" : value;
    }

    // Executes this component behavior as part of the Oyako application flow.
    private static string? FindEnvFile(string fileName, string contentRootPath)
    {
        var candidates = new[]
        {
            Path.Combine(contentRootPath, fileName),
            Path.Combine(contentRootPath, "..", fileName),
            Path.Combine(AppContext.BaseDirectory, fileName),
            Path.Combine(AppContext.BaseDirectory, "..", fileName),
            Path.Combine(AppContext.BaseDirectory, "..", "..", fileName),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", fileName),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", fileName)
        };

        // Returns the computed result to the caller and completes this branch of the workflow.
        return candidates
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(File.Exists);
    }
}
