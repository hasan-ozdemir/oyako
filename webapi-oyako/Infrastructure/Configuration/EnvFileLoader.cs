// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Configuration/EnvFileLoader.cs for maintainers.
using System.Text;
using System.Text.RegularExpressions;

namespace webapi_oyako.Infrastructure.Configuration;

// Implements the EnvFileLoader component and its responsibilities in the Oyako codebase.
public static class EnvFileLoader
{
    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["domain_only_crawling"] = ["Crawler__DomainOnlyCrawling"],
        ["web_document_max_count"] = ["Crawler__MaxPagesToCrawl"],
        ["web_document_max_depth"] = ["Crawler__MaxDepth"],
        ["ai_default_provider"] = ["Ai__DefaultProvider"],
        ["ai_fallback_provider"] = ["Ai__FallbackProviders__0"],
        ["primary_ai_provider"] = ["Ai__DefaultProvider"],
        ["secondary_ai_provider"] = ["Ai__FallbackProviders__0"],
        ["ai_provider_ollama_cloud_model"] = ["OllamaCloud__Model", "OllamaCloud__Models__0"],
        ["ai_provider_azure_cloud_model"] = ["AzureAi__DeploymentName", "AzureAi__Deployments__0"],
        ["tenant_enabled"] = ["Tenant__Enabled"],
        ["tenant_id"] = ["Tenant__Id"],
        ["tenant_order_number"] = ["Tenant__OrderNumber"],
        ["tenant_name"] = ["Tenant__Name"],
        ["tenant_display_name"] = ["Tenant__DisplayName"],
        ["tenant_azure_domain_name"] = ["Tenant__AzureDomainName"],
        ["tenant_custom_domain_name"] = ["Tenant__CustomDomainName"],
        ["tenant_web_url"] = ["Tenant__WebUrl"],
        ["tenant_admin_email"] = ["Tenant__AdminEmail"],
        ["tenant_feedback_email"] = ["Tenant__FeedbackEmail"],
        ["ui_web_brand_name"] = ["Tenant__UiWebBrandName"],
        ["ui_web_assistant_name"] = ["Tenant__UiWebAssistantName"],
        ["ui_web_title"] = ["Tenant__UiWebTitle"],
        ["ui_web_header_title"] = ["Tenant__UiWebHeaderTitle"],
        ["ui_web_brand_logo_url"] = ["Tenant__UiWebBrandLogoUrl"],
        ["ui_web_assistant_welcome_message"] = ["Tenant__UiWebAssistantWelcomeMessage"],
        ["ui_web_assistant_header_title"] = ["Tenant__UiWebAssistantHeaderTitle"],
        ["ui_web_more_menu_brand_link"] = ["Tenant__UiWebMoreMenuBrandLink"],
        ["ui_web_more_menu_feedback_link"] = ["Tenant__UiWebMoreMenuFeedbackLink"],
        ["ui_web_more_menu_help_link"] = ["Tenant__UiWebMoreMenuHelpLink"],
        ["ui_web_settings_page_title"] = ["Tenant__UiWebSettingsPageTitle"],
        ["ui_web_settings_header_title"] = ["Tenant__UiWebSettingsHeaderTitle"],
        ["ui_web_knowledge_bank_header_title"] = ["Tenant__UiWebKnowledgeBankHeaderTitle"],
        ["ui_web_knowledge_source_header_title"] = ["Tenant__UiWebKnowledgeSourceHeaderTitle"],
        ["ui_web_knowledge_source_header_message"] = ["Tenant__UiWebKnowledgeSourceHeaderMessage"],
        ["ui_web_knowledge_sources_table_title"] = ["Tenant__UiWebKnowledgeSourcesTableTitle"],
        ["ui_web_knowledge_documents_table_title"] = ["Tenant__UiWebKnowledgeDocumentsTableTitle"]
    };

    private static readonly Dictionary<string, string> ListAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tenant_text_cleaner_leading_boilerplate_terms"] = "Tenant__TextCleanerLeadingBoilerplateTerms",
        ["tenant_text_cleaner_exact_boilerplate_lines"] = "Tenant__TextCleanerExactBoilerplateLines",
        ["tenant_text_cleaner_footer_line_prefixes"] = "Tenant__TextCleanerFooterLinePrefixes"
    };

    private static readonly Regex EnvReferencePattern = new("%([A-Za-z0-9_]+)%", RegexOptions.Compiled);
    private static readonly Regex TenantIdPattern = new("^[a-f0-9]{32}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex TenantKnowledgeSourcePattern = new(
        @"^tenant_knowledge_source_(?<index>[1-9][0-9]*)_(?<field>type|url|refresh_period|name|description|enabled)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Loads each explicitly supported environment file and keeps the last file as the highest-precedence file.
    public static void LoadMany(IEnumerable<string> fileNames, string contentRootPath)
    {
        foreach (var fileName in fileNames)
        {
            Load(fileName, contentRootPath);
        }
    }

    public static string ResolveTenantName(string? contentRootPath = null)
    {
        var tenantName = Environment.GetEnvironmentVariable("OYAKO_TENANT_NAME");
        if (string.IsNullOrWhiteSpace(tenantName))
        {
            tenantName = Environment.GetEnvironmentVariable("tenant_name");
        }

        if (!string.IsNullOrWhiteSpace(tenantName))
        {
            return tenantName.Trim();
        }

        var defaultTenantId = Environment.GetEnvironmentVariable("default_tenant_id");
        if (!string.IsNullOrWhiteSpace(defaultTenantId))
        {
            return ResolveTenantNameByDefaultId(defaultTenantId.Trim(), contentRootPath);
        }

        tenantName = Environment.GetEnvironmentVariable("default_tenant_name");

        return string.IsNullOrWhiteSpace(tenantName)
            ? TenantOptions.DefaultTenantName
            : tenantName.Trim();
    }

    public static void LoadTenant(string contentRootPath)
    {
        var tenantName = ResolveTenantName(contentRootPath);
        if (FindTenantFile(tenantName, contentRootPath) is null && HasTenantEnvironment(tenantName))
        {
            LoadAliasesFromEnvironment();
            return;
        }

        var tenantFileName = DiscoverTenantFile(tenantName, contentRootPath);
        LoadRequired(tenantFileName, contentRootPath);
    }

    // Executes this component behavior as part of the Oyako application flow.
    public static void Load(string fileName, string contentRootPath)
    {
        LoadCore(fileName, contentRootPath, required: false);
    }

    private static void LoadRequired(string fileName, string contentRootPath)
    {
        LoadCore(fileName, contentRootPath, required: true);
    }

    private static void LoadCore(string fileName, string contentRootPath, bool required)
    {
        var filePath = FindEnvFile(fileName, contentRootPath);
        // Guards the following branch so the workflow handles this condition deliberately.
        if (filePath is null)
        {
            if (!required)
            {
                return;
            }

            throw new FileNotFoundException($"Required environment file was not found: {fileName}");
        }

        // Iterates through the collection to process each item consistently.
        var parsedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadAllLines(filePath, Encoding.UTF8))
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

            parsedValues[key] = value;
            SetEnvironmentValue(key, ExpandReferences(value, parsedValues));
        }

        if (fileName.Replace('\\', '/').StartsWith(".tenants/", StringComparison.OrdinalIgnoreCase))
        {
            ValidateTenantEnabled(parsedValues, filePath);
            ValidateTenantKnowledgeSourceIndexes(parsedValues, filePath);
        }
    }

    private static void SetEnvironmentValue(string key, string value)
    {
        Environment.SetEnvironmentVariable(key, value);
        if (ListAliases.TryGetValue(key, out var mappedListKey))
        {
            SetListEnvironmentValues(mappedListKey, value);
            return;
        }

        if (!Aliases.TryGetValue(key, out var mappedKeys))
        {
            SetTenantKnowledgeSourceValue(key, value);
            return;
        }

        foreach (var mappedKey in mappedKeys)
        {
            Environment.SetEnvironmentVariable(mappedKey, NormalizeAliasValue(mappedKey, value));
        }
    }

    private static void SetListEnvironmentValues(string mappedListKey, string value)
    {
        var values = value
            .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToArray();

        for (var index = 0; index < values.Length; index++)
        {
            Environment.SetEnvironmentVariable($"{mappedListKey}__{index}", values[index]);
        }
    }

    private static void SetTenantKnowledgeSourceValue(string key, string value)
    {
        var match = TenantKnowledgeSourcePattern.Match(key);
        if (!match.Success)
        {
            return;
        }

        var sourceIndex = int.Parse(match.Groups["index"].Value) - 1;
        var field = match.Groups["field"].Value.ToLowerInvariant() switch
        {
            "type" => "Type",
            "url" => "Url",
            "refresh_period" => "RefreshPeriod",
            "name" => "Name",
            "description" => "Description",
            "enabled" => "Enabled",
            _ => string.Empty
        };

        if (field.Length == 0)
        {
            return;
        }

        Environment.SetEnvironmentVariable($"Tenant__KnowledgeSources__{sourceIndex}__Key", $"source_{sourceIndex + 1}");
        Environment.SetEnvironmentVariable($"Tenant__KnowledgeSources__{sourceIndex}__{field}", value);

        if (sourceIndex == 0 && field == "Url")
        {
            Environment.SetEnvironmentVariable("Crawler__SeedUrl", value);
        }
    }

    private static void ValidateTenantKnowledgeSourceIndexes(IReadOnlyDictionary<string, string> parsedValues, string filePath)
    {
        var indexes = parsedValues.Keys
            .Select(key => TenantKnowledgeSourcePattern.Match(key))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups["index"].Value))
            .Distinct()
            .OrderBy(static index => index)
            .ToArray();

        if (indexes.Length == 0)
        {
            throw new InvalidOperationException($"Tenant env file must declare at least tenant_knowledge_source_1_type/url/refresh_period: {filePath}");
        }

        for (var expected = 1; expected <= indexes[^1]; expected++)
        {
            if (!indexes.Contains(expected))
            {
                throw new InvalidOperationException($"Tenant env file has a gap in tenant_knowledge_source_N_* indexes. Missing index: {expected}. File: {filePath}");
            }
        }
    }

    private static string DiscoverTenantFile(string tenantName, string contentRootPath)
    {
        var files = DiscoverTenantFiles(contentRootPath);
        if (files.Count == 0)
        {
            throw new FileNotFoundException("No tenant env files were found under .tenants for tenant discovery.");
        }

        var discoveredNames = new List<string>();
        foreach (var file in files)
        {
            var values = ParseEnvIdentity(file);
            var fileTenantName = Path.GetFileNameWithoutExtension(file);
            if (!values.TryGetValue("tenant_name", out var declaredName) || string.IsNullOrWhiteSpace(declaredName))
            {
                throw new InvalidOperationException($"Tenant env file must declare tenant_name: {file}");
            }

            if (!string.Equals(fileTenantName, declaredName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Tenant env file name '{fileTenantName}' must match tenant_name='{declaredName}'. File: {file}");
            }

            discoveredNames.Add(declaredName);
            if (string.Equals(declaredName, tenantName, StringComparison.OrdinalIgnoreCase))
            {
                if (!IsEnabled(values))
                {
                    throw new InvalidOperationException($"Tenant '{tenantName}' is disabled. Set tenant_enabled=true in {file} to run or deploy it.");
                }

                return Path.Combine(".tenants", Path.GetFileName(file));
            }
        }

        throw new FileNotFoundException($"Tenant '{tenantName}' was not discovered under .tenants. Discovered tenants: {string.Join(", ", discoveredNames.OrderBy(static value => value))}");
    }

    private static string? FindTenantFile(string tenantName, string contentRootPath)
    {
        return DiscoverTenantFiles(contentRootPath)
            .FirstOrDefault(file => string.Equals(Path.GetFileNameWithoutExtension(file), tenantName, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> DiscoverTenantFiles(string contentRootPath)
    {
        var candidates = new[]
        {
            Path.Combine(contentRootPath, ".tenants"),
            Path.Combine(contentRootPath, "..", ".tenants"),
            Path.Combine(AppContext.BaseDirectory, ".tenants"),
            Path.Combine(AppContext.BaseDirectory, "..", ".tenants"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", ".tenants"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".tenants"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".tenants")
        };

        return candidates
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.env", SearchOption.TopDirectoryOnly))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, string> ParseEnvIdentity(string filePath)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadAllLines(filePath, Encoding.UTF8))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"');
            values[key] = ExpandReferences(value, values);
        }

        return values;
    }

    private static void ValidateTenantEnabled(IReadOnlyDictionary<string, string> parsedValues, string filePath)
    {
        if (!parsedValues.TryGetValue("tenant_enabled", out var enabled))
        {
            throw new InvalidOperationException($"Tenant env file must declare tenant_enabled=true|false. Missing in: {filePath}");
        }

        if (!enabled.Equals("true", StringComparison.OrdinalIgnoreCase) && !enabled.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"tenant_enabled must be true or false. File: {filePath}");
        }
    }

    private static bool IsEnabled(IReadOnlyDictionary<string, string> values)
    {
        return values.TryGetValue("tenant_enabled", out var enabled)
            && enabled.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasTenantEnvironment(string tenantName)
    {
        return string.Equals(Environment.GetEnvironmentVariable("tenant_name"), tenantName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("Tenant__Name"), tenantName, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveTenantNameByDefaultId(string defaultTenantId, string? contentRootPath)
    {
        if (!TenantIdPattern.IsMatch(defaultTenantId))
        {
            throw new InvalidOperationException("default_tenant_id must be 32 lowercase hex characters.");
        }

        if (string.IsNullOrWhiteSpace(contentRootPath))
        {
            throw new FileNotFoundException("default_tenant_id is configured, but no content root was provided for tenant discovery.");
        }

        var files = DiscoverTenantFiles(contentRootPath);
        if (files.Count == 0)
        {
            throw new FileNotFoundException("default_tenant_id is configured, but no tenant env files were found under .tenants.");
        }

        var discoveredTenantNames = new List<string>();
        foreach (var file in files)
        {
            var values = ParseEnvIdentity(file);
            if (!values.TryGetValue("tenant_name", out var declaredName) || string.IsNullOrWhiteSpace(declaredName))
            {
                throw new InvalidOperationException($"Tenant env file must declare tenant_name: {file}");
            }

            var fileTenantName = Path.GetFileNameWithoutExtension(file);
            if (!string.Equals(fileTenantName, declaredName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Tenant env file name '{fileTenantName}' must match tenant_name='{declaredName}'. File: {file}");
            }

            discoveredTenantNames.Add(declaredName);
            if (!values.TryGetValue("tenant_id", out var tenantId) || !string.Equals(tenantId, defaultTenantId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsEnabled(values))
            {
                throw new InvalidOperationException($"Tenant '{declaredName}' matches default_tenant_id but is disabled. Set tenant_enabled=true in {file} to run or deploy it.");
            }

            return declaredName.Trim();
        }

        throw new FileNotFoundException($"default_tenant_id '{defaultTenantId}' did not match an enabled tenant under .tenants. Discovered tenants: {string.Join(", ", discoveredTenantNames.OrderBy(static value => value))}");
    }

    private static void LoadAliasesFromEnvironment()
    {
        var values = Environment.GetEnvironmentVariables()
            .Keys
            .OfType<string>()
            .ToDictionary(key => key, key => Environment.GetEnvironmentVariable(key) ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        foreach (var key in values.Keys.ToArray())
        {
            if (Aliases.ContainsKey(key) || ListAliases.ContainsKey(key) || TenantKnowledgeSourcePattern.IsMatch(key))
            {
                SetEnvironmentValue(key, ExpandReferences(values[key], values));
            }
        }
    }

    private static string ExpandReferences(string value, IReadOnlyDictionary<string, string> parsedValues)
    {
        return EnvReferencePattern.Replace(value, match =>
        {
            var key = match.Groups[1].Value;
            return parsedValues.TryGetValue(key, out var parsedValue)
                ? parsedValue
                : Environment.GetEnvironmentVariable(key) ?? match.Value;
        });
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
