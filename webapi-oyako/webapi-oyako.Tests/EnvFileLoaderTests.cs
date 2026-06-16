// Codex developer note: Explains the purpose and flow of webapi-oyako/webapi-oyako.Tests/EnvFileLoaderTests.cs for maintainers.
using webapi_oyako.Infrastructure.Configuration;
using Xunit;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Tests;

// Verifies supported environment files are loaded into process environment variables.
public sealed class EnvFileLoaderTests
{
    [Fact]
    // Verifies Azure and Ollama env files are both loaded and later files can override existing process values.
    public void LoadMany_ReadsPrimaryAzureAndOllamaFilesAndLetsFilesOverrideProcessValues()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"oyako-env-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var azureEnvPath = Path.Combine(tempRoot, "azure-cloud.env");
        var ollamaEnvPath = Path.Combine(tempRoot, "ollama-cloud.env");
        var fileKey = $"AzureAi__ApiKey_{Guid.NewGuid():N}";
        var cloudKey = $"ollama_api_key_test_{Guid.NewGuid():N}";

        try
        {
            Environment.SetEnvironmentVariable(fileKey, "process-value");
            File.WriteAllText(azureEnvPath, $"{fileKey}=azure-file-value");
            File.WriteAllText(ollamaEnvPath, $"{cloudKey}=ollama-cloud-secret");

            EnvFileLoader.LoadMany(["azure-cloud.env", "ollama-cloud.env"], tempRoot);

            Assert.Equal("azure-file-value", Environment.GetEnvironmentVariable(fileKey));
            Assert.Equal("ollama-cloud-secret", Environment.GetEnvironmentVariable(cloudKey));
        }
        finally
        {
            Environment.SetEnvironmentVariable(fileKey, null);
            Environment.SetEnvironmentVariable(cloudKey, null);
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    // Verifies that the old oyako.env file is not part of the primary bootstrap contract.
    public void LoadMany_DoesNotReadOldOyakoEnvFile()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"oyako-env-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var oldKey = $"LegacyOyakoKey_{Guid.NewGuid():N}";

        try
        {
            File.WriteAllText(Path.Combine(tempRoot, "oyako.env"), $"{oldKey}=legacy-value");

            EnvFileLoader.LoadMany(["azure-cloud.env", "ollama-cloud.env"], tempRoot);

            Assert.Null(Environment.GetEnvironmentVariable(oldKey));
        }
        finally
        {
            Environment.SetEnvironmentVariable(oldKey, null);
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
