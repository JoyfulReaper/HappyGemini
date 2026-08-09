using Microsoft.Extensions.Options;

namespace HappyGemini;

/// <summary>
/// Resolves request hostnames to configured Gemini virtual hosts.
/// </summary>
public sealed class GeminiVirtualHostResolver
{
    private readonly Dictionary<string, GeminiVirtualHost>
        _hosts;

    public GeminiVirtualHostResolver(
        IOptions<GeminiServerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _hosts =
            new Dictionary<string, GeminiVirtualHost>(
                StringComparer.OrdinalIgnoreCase);

        foreach (string hostname
            in options.Value.Hostnames)
        {
            string normalizedHostname =
                NormalizeHostname(hostname);

            GeminiVirtualHost host =
                new(normalizedHostname);

            if (!_hosts.TryAdd(
                    normalizedHostname,
                    host))
            {
                throw new InvalidOperationException(
                    $"Gemini virtual host '{hostname}' is configured more than once.");
            }
        }
    }

    public GeminiVirtualHost? Resolve(
        Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        _hosts.TryGetValue(
            NormalizeHostname(url.IdnHost),
            out GeminiVirtualHost? host);

        return host;
    }

    private static string NormalizeHostname(
        string hostname)
    {
        return hostname
            .Trim()
            .TrimEnd('.');
    }
}