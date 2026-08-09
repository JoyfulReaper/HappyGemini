using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;

namespace HappyGemini.Server;

public sealed class GeminiCertificateProvider : IDisposable
{
    private readonly Dictionary<string, X509Certificate2> _certificates;

    public GeminiCertificateProvider(IOptions<GeminiServerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        GeminiServerOptions value = options.Value;

        Certificate = LoadCertificate(value.CertificatePath, value.CertificatePassword);

        _certificates = new Dictionary<string, X509Certificate2>(StringComparer.OrdinalIgnoreCase);

        foreach (
            (string hostname, GeminiCertificateOptions certificateOptions) in value.Certificates
        )
        {
            if (!GeminiHostname.TryNormalize(hostname, out string normalizedHostname))
            {
                throw new InvalidOperationException(
                    $"Gemini certificate hostname '{hostname}' is invalid."
                );
            }

            if (string.IsNullOrWhiteSpace(certificateOptions.Path))
            {
                throw new InvalidOperationException(
                    $"Certificate path for Gemini host '{hostname}' must not be empty."
                );
            }

            X509Certificate2 certificate = LoadCertificate(
                certificateOptions.Path,
                certificateOptions.Password
            );

            if (!_certificates.TryAdd(normalizedHostname, certificate))
            {
                certificate.Dispose();

                throw new InvalidOperationException(
                    $"A certificate is already configured for Gemini host '{hostname}'."
                );
            }
        }
    }

    public X509Certificate2 Certificate { get; }

    public X509Certificate2 SelectCertificate(string? hostname)
    {
        if (
            GeminiHostname.TryNormalize(hostname, out string normalizedHostname)
            && _certificates.TryGetValue(normalizedHostname, out X509Certificate2? certificate)
        )
        {
            return certificate;
        }

        return Certificate;
    }

    public void Dispose()
    {
        foreach (X509Certificate2 certificate in _certificates.Values)
        {
            certificate.Dispose();
        }

        Certificate.Dispose();
    }

    private static X509Certificate2 LoadCertificate(string path, string? password)
    {
        return X509CertificateLoader.LoadPkcs12FromFile(path, password);
    }
}
