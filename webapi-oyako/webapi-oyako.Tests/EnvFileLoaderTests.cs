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
    // Verifies explicitly loaded env files can expose user-friendly aliases.
    public void LoadMany_ReadsExplicitEnvAliases()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"oyako-env-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var crawlerKey = "Crawler__MaxPagesToCrawl";
        var fallbackKey = "Ai__FallbackProviders__0";

        try
        {
            File.WriteAllText(
                Path.Combine(tempRoot, "runtime.env"),
                """
                web_document_max_count=1000
                ai_fallback_provider=azure-cloud
                """);

            EnvFileLoader.LoadMany(["azure-cloud.env", "ollama-cloud.env", "runtime.env"], tempRoot);

            Assert.Equal("1000", Environment.GetEnvironmentVariable(crawlerKey));
            Assert.Equal("azure", Environment.GetEnvironmentVariable(fallbackKey));
        }
        finally
        {
            Environment.SetEnvironmentVariable("web_document_max_count", null);
            Environment.SetEnvironmentVariable("ai_fallback_provider", null);
            Environment.SetEnvironmentVariable(crawlerKey, null);
            Environment.SetEnvironmentVariable(fallbackKey, null);
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    // Verifies tenant env files are loaded from .tenants and expose UI, tenant, and AI aliases.
    public void LoadTenant_ReadsDefaultTenantEnvAliases()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"oyako-tenant-env-{Guid.NewGuid():N}");
        var tenantsRoot = Path.Combine(tempRoot, ".tenants");
        Directory.CreateDirectory(tenantsRoot);

        try
        {
            File.WriteAllText(
                Path.Combine(tenantsRoot, "oyakdijital.env"),
                """
                tenant_id=013dfb350ed64e324a805eae86646ddf
                tenant_name=oyakdijital
                ui_web_assistant_name=Oyako
                ui_web_brand_name=Oyak Dijital
                ui_web_title=%ui_web_assistant_name%: %ui_web_brand_name% Soru-Cevap Platformu
                primary_ai_provider=ollama-cloud
                secondary_ai_provider=azure-cloud
                ai_provider_ollama_cloud_model=minimax-m3:cloud
                ai_provider_azure_cloud_model=deepseek-v4-flash
                """);

            Environment.SetEnvironmentVariable("OYAKO_TENANT_NAME", null);
            EnvFileLoader.LoadTenant(tempRoot);

            Assert.Equal("oyakdijital", Environment.GetEnvironmentVariable("Tenant__Name"));
            Assert.Equal("Oyako: Oyak Dijital Soru-Cevap Platformu", Environment.GetEnvironmentVariable("Tenant__UiWebTitle"));
            Assert.Equal("ollama-cloud", Environment.GetEnvironmentVariable("Ai__DefaultProvider"));
            Assert.Equal("azure", Environment.GetEnvironmentVariable("Ai__FallbackProviders__0"));
            Assert.Equal("minimax-m3:cloud", Environment.GetEnvironmentVariable("OllamaCloud__Models__0"));
            Assert.Equal("deepseek-v4-flash", Environment.GetEnvironmentVariable("AzureAi__Deployments__0"));
        }
        finally
        {
            foreach (var key in new[]
            {
                "tenant_id",
                "tenant_name",
                "ui_web_assistant_name",
                "ui_web_brand_name",
                "ui_web_title",
                "primary_ai_provider",
                "secondary_ai_provider",
                "ai_provider_ollama_cloud_model",
                "ai_provider_azure_cloud_model",
                "Tenant__Name",
                "Tenant__UiWebTitle",
                "Ai__DefaultProvider",
                "Ai__FallbackProviders__0",
                "OllamaCloud__Models__0",
                "AzureAi__Deployments__0",
                "OYAKO_TENANT_NAME"
            })
            {
                Environment.SetEnvironmentVariable(key, null);
            }

            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
