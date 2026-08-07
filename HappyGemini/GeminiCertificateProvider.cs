using Microsoft.Extensions.Options;
using System.Security.Cryptography.X509Certificates;

namespace HappyGemini.Server;

public sealed class GeminiCertificateProvider : IDisposable
{
    public GeminiCertificateProvider(IOptions<GeminiServerOptions> options)
    {
        GeminiServerOptions value = options.Value;

        Certificate = X509CertificateLoader.LoadPkcs12FromFile(
            value.CertificatePath,
            value.CertificatePassword);
    }

    public X509Certificate2 Certificate { get; }

    public X509Certificate2 SelectCertificate(string? hostname)
    {
        return Certificate;
    }

    public void Dispose()
    {
        Certificate.Dispose();
    }
}