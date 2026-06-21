// Codex developer note: Defines public tenant branding and deployment metadata loaded from tenant env files.
namespace webapi_oyako.Infrastructure.Configuration;

// Holds the active single-tenant runtime configuration for one isolated Oyako deployment.
public sealed class TenantOptions
{
    public const string SectionName = "Tenant";
    public const string DefaultTenantName = "oyakdijital";

    public string Id { get; set; } = "013dfb350ed64e324a805eae86646ddf";
    public int OrderNumber { get; set; } = 1;
    public string Name { get; set; } = DefaultTenantName;
    public string DisplayName { get; set; } = "Oyak Dijital";
    public string AzureDomainName { get; set; } = "oyako";
    public string CustomDomainName { get; set; } = "oyako.oyakdijital.com.tr";
    public string WebUrl { get; set; } = "https://www.oyakdijital.com.tr";
    public string AdminEmail { get; set; } = "admin@oyakdijital.com.tr";
    public string FeedbackEmail { get; set; } = "iletisim@oyakdijital.com.tr";
    public string UiWebBrandName { get; set; } = "Oyak Dijital";
    public string UiWebAssistantName { get; set; } = "Oyako";
    public string UiWebTitle { get; set; } = "Oyako: Oyak Dijital Soru-Cevap Platformu";
    public string UiWebHeaderTitle { get; set; } = "Oyak Dijital soru-cevap platformu";
    public string UiWebBrandLogoUrl { get; set; } = "/tenants/oyakdijital/brand-logo.svg";
    public string UiWebAssistantWelcomeMessage { get; set; } = "Merhaba, ben dijital asistanınız Oyako. Oyak Dijital ile ilgili merak ettiğiniz her şeyi bana sorabilirsiniz. Cevaplamak için hazırım.";
    public string UiWebAssistantHeaderTitle { get; set; } = "Oyak Dijital hakkında öğrenmek istediğinizi sorun:";
    public string UiWebMoreMenuBrandLink { get; set; } = "Oyak Dijital";
    public string UiWebMoreMenuFeedbackLink { get; set; } = "Geri Bildirim Gönderin";
    public string UiWebMoreMenuHelpLink { get; set; } = "Yardım";
    public string UiWebSettingsPageTitle { get; set; } = "Ayarlar";
    public string UiWebSettingsHeaderTitle { get; set; } = "Oyako çalışma ayarları";
    public string UiWebKnowledgeBankHeaderTitle { get; set; } = "Bilgi Bankası";
    public string UiWebKnowledgeSourceHeaderTitle { get; set; } = "Bilgi Kaynakları";
    public string UiWebKnowledgeSourceHeaderMessage { get; set; } = "Oyako, sorularınıza cevap verirken aşağıda gösterilen {sourceCount} adet bilgi kaynağını ve {documentCount} adet belgeyi kullanabilir.";
    public string UiWebKnowledgeSourcesTableTitle { get; set; } = "Şu kaynaklar kullanılabilir:";
    public string UiWebKnowledgeDocumentsTableTitle { get; set; } = "Şu belgeler kullanılabilir:";
}
