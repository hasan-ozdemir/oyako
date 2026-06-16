// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Configuration/EnvFileLoader.cs for maintainers.
namespace webapi_oyako.Infrastructure.Configuration;

// Implements the EnvFileLoader component and its responsibilities in the Oyako codebase.
public static class EnvFileLoader
{
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

            Environment.SetEnvironmentVariable(key, value);
        }
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
