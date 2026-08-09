using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HappyGemini.Server;
using Microsoft.Extensions.Options;

namespace HappyGemini.Tests;

public sealed class GeminiCertificateProviderTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "HappyGemini.Tests",
        Guid.NewGuid().ToString("N")
    );

    [Fact]
    public void SelectCertificate_CanonicalizesUnicodeAndPunycodeHostnames()
    {
        Directory.CreateDirectory(_testRoot);

        string defaultCertificatePath = CreateCertificate("default");
        string hostCertificatePath = CreateCertificate("idn-host");

        GeminiServerOptions options = new()
        {
            CertificatePath = defaultCertificatePath,
            Certificates = new Dictionary<string, GeminiCertificateOptions>
            {
                ["bücher.example"] = new() { Path = hostCertificatePath },
            },
        };

        using GeminiCertificateProvider provider = new(Options.Create(options));

        X509Certificate2 selected = provider.SelectCertificate("XN--BCHER-KVA.EXAMPLE.");

        Assert.NotEqual(provider.Certificate.Thumbprint, selected.Thumbprint);
        Assert.Equal("CN=idn-host", selected.Subject);
    }

    private string CreateCertificate(string commonName)
    {
        using RSA key = RSA.Create(2048);

        CertificateRequest request = new(
            $"CN={commonName}",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );

        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1)
        );

        string path = Path.Combine(_testRoot, $"{commonName}.pfx");

        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pkcs12));

        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }
}
