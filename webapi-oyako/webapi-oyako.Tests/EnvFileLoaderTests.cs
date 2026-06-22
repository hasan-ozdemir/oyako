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
    // Verifies the tracked global env file can provide non-secret runtime defaults without tenant loading.
    public void LoadMany_ReadsGlobalOyakoEnv()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"oyako-env-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            File.WriteAllText(
                Path.Combine(tempRoot, "oyako.env"),
                """
                default_tenant_id=013dfb350ed64e324a805eae86646ddf
                default_tenant_name=tenantdemo
                azure_location=italynorth
                """);

            EnvFileLoader.LoadMany(["oyako.env"], tempRoot);

            Assert.Equal("013dfb350ed64e324a805eae86646ddf", Environment.GetEnvironmentVariable("default_tenant_id"));
            Assert.Equal("tenantdemo", Environment.GetEnvironmentVariable("default_tenant_name"));
            Assert.Equal("italynorth", Environment.GetEnvironmentVariable("azure_location"));
        }
        finally
        {
            ClearEnvironment("default_tenant_id", "default_tenant_name", "azure_location");
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    // Verifies default_tenant_id resolves the enabled tenant env file when no tenant name is explicit.
    public void LoadTenant_ResolvesEnabledTenantFromDefaultTenantId()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"oyako-tenant-env-{Guid.NewGuid():N}");
        var tenantsRoot = Path.Combine(tempRoot, ".tenants");
        Directory.CreateDirectory(tenantsRoot);

        try
        {
            File.WriteAllText(Path.Combine(tempRoot, "oyako.env"), "default_tenant_id=013dfb350ed64e324a805eae86646ddf");
            File.WriteAllText(Path.Combine(tenantsRoot, "tenantdemo.env"), BuildTenantEnv(enabled: true));

            EnvFileLoader.LoadMany(["oyako.env"], tempRoot);
            EnvFileLoader.LoadTenant(tempRoot);

            Assert.Equal("tenantdemo", Environment.GetEnvironmentVariable("Tenant__Name"));
        }
        finally
        {
            ClearTenantEnvironment();
            ClearEnvironment("default_tenant_id");
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    // Verifies default_tenant_id never silently activates a disabled tenant.
    public void LoadTenant_RejectsDisabledTenantFromDefaultTenantId()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"oyako-tenant-env-{Guid.NewGuid():N}");
        var tenantsRoot = Path.Combine(tempRoot, ".tenants");
        Directory.CreateDirectory(tenantsRoot);

        try
        {
            File.WriteAllText(Path.Combine(tempRoot, "oyako.env"), "default_tenant_id=013dfb350ed64e324a805eae86646ddf");
            File.WriteAllText(Path.Combine(tenantsRoot, "tenantdemo.env"), BuildTenantEnv(enabled: false));

            EnvFileLoader.LoadMany(["oyako.env"], tempRoot);

            var exception = Assert.Throws<InvalidOperationException>(() => EnvFileLoader.LoadTenant(tempRoot));
            Assert.Contains("disabled", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            ClearTenantEnvironment();
            ClearEnvironment("default_tenant_id");
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
                Path.Combine(tenantsRoot, "tenantdemo.env"),
                """
                tenant_enabled=true
                tenant_id=013dfb350ed64e324a805eae86646ddf
                tenant_order_number=1
                tenant_name=tenantdemo
                tenant_display_name=Tenant Demo
                tenant_azure_domain_name=tenantdemo
                tenant_web_url=https://www.tenantdemo.example
                tenant_admin_email=admin@tenantdemo.example
                tenant_feedback_email=iletisim@tenantdemo.example
                ui_web_assistant_name=Oyako
                ui_web_brand_name=Tenant Demo
                ui_web_title=%ui_web_assistant_name%: %ui_web_brand_name% Soru-Cevap Platformu
                ui_web_header_title=%ui_web_brand_name% soru-cevap platformu
                ui_web_brand_logo_url=https://www.tenantdemo.example/logo.svg
                ui_web_assistant_welcome_message=Merhaba, ben %ui_web_assistant_name%.
                ui_web_assistant_header_title=%ui_web_brand_name% hakkında sorun:
                ui_web_knowledge_bank_header_title=Bilgi Bankası
                ui_web_knowledge_source_header_title=Bilgi Kaynakları
                ui_web_knowledge_source_header_message=Aşağıda {sourceCount} kaynak ve {documentCount} belge var.
                ui_web_knowledge_sources_table_title=Şu kaynaklar kullanılabilir:
                ui_web_knowledge_documents_table_title=Şu belgeler kullanılabilir:
                tenant_knowledge_source_1_type=web_site
                tenant_knowledge_source_1_url=https://www.tenantdemo.example
                tenant_knowledge_source_1_refresh_period=1hour
                tenant_text_cleaner_leading_boilerplate_terms=Demo Başlık|Demo Menü
                tenant_text_cleaner_exact_boilerplate_lines=demo başlık|demo menü
                tenant_text_cleaner_footer_line_prefixes=© demo
                primary_ai_provider=ollama-cloud
                secondary_ai_provider=azure-cloud
                ai_provider_ollama_cloud_model=minimax-m3:cloud
                ai_provider_azure_cloud_model=deepseek-v4-flash
                """);

            Environment.SetEnvironmentVariable("OYAKO_TENANT_NAME", "tenantdemo");
            EnvFileLoader.LoadTenant(tempRoot);

            Assert.Equal("true", Environment.GetEnvironmentVariable("Tenant__Enabled"));
            Assert.Equal("tenantdemo", Environment.GetEnvironmentVariable("Tenant__Name"));
            Assert.Equal("Oyako: Tenant Demo Soru-Cevap Platformu", Environment.GetEnvironmentVariable("Tenant__UiWebTitle"));
            Assert.Equal("ollama-cloud", Environment.GetEnvironmentVariable("Ai__DefaultProvider"));
            Assert.Equal("azure", Environment.GetEnvironmentVariable("Ai__FallbackProviders__0"));
            Assert.Equal("minimax-m3:cloud", Environment.GetEnvironmentVariable("OllamaCloud__Models__0"));
            Assert.Equal("deepseek-v4-flash", Environment.GetEnvironmentVariable("AzureAi__Deployments__0"));
            Assert.Equal("web_site", Environment.GetEnvironmentVariable("Tenant__KnowledgeSources__0__Type"));
            Assert.Equal("https://www.tenantdemo.example", Environment.GetEnvironmentVariable("Tenant__KnowledgeSources__0__Url"));
            Assert.Equal("1hour", Environment.GetEnvironmentVariable("Tenant__KnowledgeSources__0__RefreshPeriod"));
            Assert.Equal("Demo Başlık", Environment.GetEnvironmentVariable("Tenant__TextCleanerLeadingBoilerplateTerms__0"));
            Assert.Equal("demo menü", Environment.GetEnvironmentVariable("Tenant__TextCleanerExactBoilerplateLines__1"));
            Assert.Equal("© demo", Environment.GetEnvironmentVariable("Tenant__TextCleanerFooterLinePrefixes__0"));
        }
        finally
        {
            ClearTenantEnvironment();
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string BuildTenantEnv(bool enabled)
    {
        return $$"""
               tenant_enabled={{enabled.ToString().ToLowerInvariant()}}
               tenant_id=013dfb350ed64e324a805eae86646ddf
               tenant_order_number=1
               tenant_name=tenantdemo
               tenant_display_name=Tenant Demo
               tenant_azure_domain_name=tenantdemo
               tenant_web_url=https://www.tenantdemo.example
               tenant_admin_email=admin@tenantdemo.example
               tenant_feedback_email=iletisim@tenantdemo.example
               ui_web_assistant_name=Oyako
               ui_web_brand_name=Tenant Demo
               ui_web_title=%ui_web_assistant_name%: %ui_web_brand_name% Soru-Cevap Platformu
               ui_web_header_title=%ui_web_brand_name% soru-cevap platformu
               ui_web_brand_logo_url=https://www.tenantdemo.example/logo.svg
               ui_web_assistant_welcome_message=Merhaba, ben %ui_web_assistant_name%.
               ui_web_assistant_header_title=%ui_web_brand_name% hakkında sorun:
               ui_web_knowledge_bank_header_title=Bilgi Bankası
               ui_web_knowledge_source_header_title=Bilgi Kaynakları
               ui_web_knowledge_source_header_message=Aşağıda {sourceCount} kaynak ve {documentCount} belge var.
               ui_web_knowledge_sources_table_title=Şu kaynaklar kullanılabilir:
               ui_web_knowledge_documents_table_title=Şu belgeler kullanılabilir:
               tenant_knowledge_source_1_type=web_site
               tenant_knowledge_source_1_url=https://www.tenantdemo.example
               tenant_knowledge_source_1_refresh_period=1hour
               primary_ai_provider=ollama-cloud
               secondary_ai_provider=azure-cloud
               ai_provider_ollama_cloud_model=minimax-m3:cloud
               ai_provider_azure_cloud_model=deepseek-v4-flash
               """;
    }

    private static void ClearTenantEnvironment()
    {
        ClearEnvironment(
            "tenant_id",
            "tenant_enabled",
            "tenant_order_number",
            "tenant_name",
            "tenant_display_name",
            "tenant_azure_domain_name",
            "tenant_web_url",
            "tenant_admin_email",
            "tenant_feedback_email",
            "ui_web_assistant_name",
            "ui_web_brand_name",
            "ui_web_title",
            "ui_web_header_title",
            "ui_web_brand_logo_url",
            "ui_web_assistant_welcome_message",
            "ui_web_assistant_header_title",
            "ui_web_knowledge_bank_header_title",
            "ui_web_knowledge_source_header_title",
            "ui_web_knowledge_source_header_message",
            "ui_web_knowledge_sources_table_title",
            "ui_web_knowledge_documents_table_title",
            "tenant_knowledge_source_1_type",
            "tenant_knowledge_source_1_url",
            "tenant_knowledge_source_1_refresh_period",
            "primary_ai_provider",
            "secondary_ai_provider",
            "ai_provider_ollama_cloud_model",
            "ai_provider_azure_cloud_model",
            "tenant_text_cleaner_leading_boilerplate_terms",
            "tenant_text_cleaner_exact_boilerplate_lines",
            "tenant_text_cleaner_footer_line_prefixes",
            "Tenant__Enabled",
            "Tenant__Name",
            "Tenant__UiWebTitle",
            "Ai__DefaultProvider",
            "Ai__FallbackProviders__0",
            "OllamaCloud__Models__0",
            "AzureAi__Deployments__0",
            "Tenant__KnowledgeSources__0__Key",
            "Tenant__KnowledgeSources__0__Type",
            "Tenant__KnowledgeSources__0__Url",
            "Tenant__KnowledgeSources__0__RefreshPeriod",
            "Tenant__TextCleanerLeadingBoilerplateTerms__0",
            "Tenant__TextCleanerLeadingBoilerplateTerms__1",
            "Tenant__TextCleanerExactBoilerplateLines__0",
            "Tenant__TextCleanerExactBoilerplateLines__1",
            "Tenant__TextCleanerFooterLinePrefixes__0",
            "Crawler__SeedUrl",
            "OYAKO_TENANT_NAME");
    }

    private static void ClearEnvironment(params string[] keys)
    {
        foreach (var key in keys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }
}
