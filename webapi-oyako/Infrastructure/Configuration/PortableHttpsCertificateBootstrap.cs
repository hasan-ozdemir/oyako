// Codex developer note: Creates the repository-local HTTPS certificate required by the development web API.
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace webapi_oyako.Infrastructure.Configuration;

// Generates a portable localhost PFX without depending on the operating system certificate store.
public static class PortableHttpsCertificateBootstrap
{
    // Defines the local directory that contains the generated development certificate.
    public const string CertificateDirectoryName = ".certificates";

    // Defines the generated development certificate file name consumed by Kestrel.
    public const string CertificateFileName = "oyako-localhost.pfx";

    // Ensures the localhost development certificate exists and returns its absolute path.
    public static string EnsureCertificate(string contentRootPath)
    {
        var certificateDirectory = Path.Combine(contentRootPath, CertificateDirectoryName);
        var certificatePath = Path.Combine(certificateDirectory, CertificateFileName);
        if (File.Exists(certificatePath))
        {
            return certificatePath;
        }

        Directory.CreateDirectory(certificateDirectory);
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            false));
        var usages = new OidCollection
        {
            new("1.3.6.1.5.5.7.3.1")
        };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, false));
        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddDnsName("localhost");
        subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
        subjectAlternativeNames.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(10));
        File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pfx));
        return certificatePath;
    }
}
