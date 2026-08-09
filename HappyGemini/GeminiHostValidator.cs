using Microsoft.Extensions.Options;

namespace HappyGemini;

/// <summary>
/// Determines whether a Gemini request targets a hostname
/// served by this server.
/// </summary>
public sealed class GeminiHostValidator
{
    private readonly HashSet<string> _hostnames;

    public GeminiHostValidator(
        IOptions<GeminiServerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _hostnames = options.Value.Hostnames
            .Select(NormalizeHostname)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsServed(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        return _hostnames.Contains(
            NormalizeHostname(url.IdnHost));
    }

    public bool MatchesServerName(
        Uri url,
        string? serverName)
    {
        ArgumentNullException.ThrowIfNull(url);

        // Gemini clients SHOULD omit SNI when the URI
        // authority is an IP address, so only enforce
        // matching for DNS hostnames.
        if (url.HostNameType != UriHostNameType.Dns)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(serverName))
        {
            return false;
        }

        return string.Equals(
            NormalizeHostname(url.IdnHost),
            NormalizeHostname(serverName),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeHostname(
        string hostname)
    {
        return hostname
            .Trim()
            .TrimEnd('.');
    }
}