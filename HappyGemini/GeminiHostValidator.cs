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

    private static string NormalizeHostname(
        string hostname)
    {
        return hostname
            .Trim()
            .TrimEnd('.');
    }
}