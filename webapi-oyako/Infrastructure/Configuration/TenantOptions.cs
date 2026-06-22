// Codex developer note: Defines tenant runtime metadata loaded from strict tenant env files.
using Microsoft.Extensions.Options;

namespace webapi_oyako.Infrastructure.Configuration;

// Holds the active single-tenant runtime configuration for one isolated Oyako deployment.
public sealed class TenantOptions
{
    public const string SectionName = "Tenant";
    public const string DefaultTenantName = "oyakdijital";

    public bool Enabled { get; set; }
    public string Id { get; set; } = string.Empty;
    public int OrderNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AzureDomainName { get; set; } = string.Empty;
    public string CustomDomainName { get; set; } = string.Empty;
    public string WebUrl { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public string FeedbackEmail { get; set; } = string.Empty;
    public string UiWebBrandName { get; set; } = string.Empty;
    public string UiWebAssistantName { get; set; } = string.Empty;
    public string UiWebTitle { get; set; } = string.Empty;
    public string UiWebHeaderTitle { get; set; } = string.Empty;
    public string UiWebBrandLogoUrl { get; set; } = string.Empty;
    public string UiWebAssistantWelcomeMessage { get; set; } = string.Empty;
    public string UiWebAssistantHeaderTitle { get; set; } = string.Empty;
    public string UiWebMoreMenuBrandLink { get; set; } = string.Empty;
    public string UiWebMoreMenuFeedbackLink { get; set; } = string.Empty;
    public string UiWebMoreMenuHelpLink { get; set; } = string.Empty;
    public string UiWebSettingsPageTitle { get; set; } = string.Empty;
    public string UiWebSettingsHeaderTitle { get; set; } = string.Empty;
    public string UiWebKnowledgeBankHeaderTitle { get; set; } = string.Empty;
    public string UiWebKnowledgeSourceHeaderTitle { get; set; } = string.Empty;
    public string UiWebKnowledgeSourceHeaderMessage { get; set; } = string.Empty;
    public string UiWebKnowledgeSourcesTableTitle { get; set; } = string.Empty;
    public string UiWebKnowledgeDocumentsTableTitle { get; set; } = string.Empty;
    public List<string> TextCleanerLeadingBoilerplateTerms { get; set; } = [];
    public List<string> TextCleanerExactBoilerplateLines { get; set; } = [];
    public List<string> TextCleanerFooterLinePrefixes { get; set; } = [];
    public List<TenantKnowledgeSourceOptions> KnowledgeSources { get; set; } = [];
}

// Describes one seed knowledge source declared by a tenant env file.
public sealed class TenantKnowledgeSourceOptions
{
    public string Key { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string RefreshPeriod { get; set; } = "1hour";
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

// Validates the active tenant as a strict deployment contract instead of using legacy fallbacks.
public sealed class TenantOptionsValidator : IValidateOptions<TenantOptions>
{
    public ValidateOptionsResult Validate(string? name, TenantOptions options)
    {
        var errors = new List<string>();
        if (!options.Enabled)
        {
            errors.Add("Tenant must be explicitly enabled with tenant_enabled=true.");
        }

        Require(options.Id, nameof(options.Id), errors);
        Require(options.Name, nameof(options.Name), errors);
        Require(options.DisplayName, nameof(options.DisplayName), errors);
        Require(options.AzureDomainName, nameof(options.AzureDomainName), errors);
        Require(options.WebUrl, nameof(options.WebUrl), errors);
        Require(options.AdminEmail, nameof(options.AdminEmail), errors);
        Require(options.FeedbackEmail, nameof(options.FeedbackEmail), errors);
        Require(options.UiWebBrandName, nameof(options.UiWebBrandName), errors);
        Require(options.UiWebAssistantName, nameof(options.UiWebAssistantName), errors);
        Require(options.UiWebTitle, nameof(options.UiWebTitle), errors);
        Require(options.UiWebHeaderTitle, nameof(options.UiWebHeaderTitle), errors);
        Require(options.UiWebBrandLogoUrl, nameof(options.UiWebBrandLogoUrl), errors);
        Require(options.UiWebAssistantWelcomeMessage, nameof(options.UiWebAssistantWelcomeMessage), errors);
        Require(options.UiWebAssistantHeaderTitle, nameof(options.UiWebAssistantHeaderTitle), errors);
        Require(options.UiWebKnowledgeBankHeaderTitle, nameof(options.UiWebKnowledgeBankHeaderTitle), errors);
        Require(options.UiWebKnowledgeSourceHeaderTitle, nameof(options.UiWebKnowledgeSourceHeaderTitle), errors);
        Require(options.UiWebKnowledgeSourceHeaderMessage, nameof(options.UiWebKnowledgeSourceHeaderMessage), errors);
        Require(options.UiWebKnowledgeSourcesTableTitle, nameof(options.UiWebKnowledgeSourcesTableTitle), errors);
        Require(options.UiWebKnowledgeDocumentsTableTitle, nameof(options.UiWebKnowledgeDocumentsTableTitle), errors);

        if (options.OrderNumber < 1)
        {
            errors.Add("Tenant order number must be greater than zero.");
        }

        if (!Uri.TryCreate(options.WebUrl, UriKind.Absolute, out var webUri) || webUri.Scheme is not "http" and not "https")
        {
            errors.Add("Tenant WebUrl must be an absolute http/https URL.");
        }

        if (options.KnowledgeSources.Count == 0)
        {
            errors.Add("At least one tenant_knowledge_source_N_* entry is required.");
        }

        foreach (var source in options.KnowledgeSources)
        {
            Require(source.Key, "KnowledgeSources.Key", errors);
            Require(source.Type, $"KnowledgeSources[{source.Key}].Type", errors);
            Require(source.Url, $"KnowledgeSources[{source.Key}].Url", errors);
            Require(source.RefreshPeriod, $"KnowledgeSources[{source.Key}].RefreshPeriod", errors);

            if (!source.Type.Equals("web_site", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Knowledge source '{source.Key}' type must be web_site.");
            }

            if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var sourceUri) || sourceUri.Scheme is not "http" and not "https")
            {
                errors.Add($"Knowledge source '{source.Key}' URL must be an absolute http/https URL.");
            }

            if (!TenantRefreshPeriodParser.TryParseMinutes(source.RefreshPeriod, out _))
            {
                errors.Add($"Knowledge source '{source.Key}' refresh period '{source.RefreshPeriod}' is invalid.");
            }
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static void Require(string? value, string name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{name} is required.");
        }
    }
}
