// Codex developer note: Verifies that the Web API can recreate its portable local HTTPS certificate from source code.
using System.Security.Cryptography.X509Certificates;
using webapi_oyako.Infrastructure.Configuration;
using Xunit;

namespace webapi_oyako.Tests;

// Covers the certificate bootstrap behavior required after deleting .certificates.
public class PortableHttpsCertificateBootstrapTests
{
    [Fact]
    // Ensures a clean checkout can regenerate the HTTPS PFX without using the OS certificate store.
    public void EnsureCertificate_WhenCertificateIsMissing_CreatesLoadablePfx()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"oyako-cert-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var certificatePath = PortableHttpsCertificateBootstrap.EnsureCertificate(tempRoot);

            // Verifies the expected behavior for this test scenario.
            Assert.True(File.Exists(certificatePath));
            // Creates a certificate instance to prove the generated PFX can be consumed by Kestrel-compatible APIs.
            using var certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath, null);
            // Verifies the expected behavior for this test scenario.
            Assert.Contains("CN=localhost", certificate.Subject, StringComparison.OrdinalIgnoreCase);
            // Verifies the expected behavior for this test scenario.
            Assert.True(certificate.HasPrivateKey);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
