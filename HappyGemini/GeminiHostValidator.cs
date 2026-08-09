namespace HappyGemini;

/// <summary>
/// Validates Gemini request host information.
/// </summary>
public sealed class GeminiHostValidator
{
    private const int DefaultGeminiPort = 1965;

    public bool TargetsServerPort(Uri url, int serverPort)
    {
        ArgumentNullException.ThrowIfNull(url);

        int requestPort = url.Port < 0 ? DefaultGeminiPort : url.Port;

        return requestPort == serverPort;
    }

    public bool MatchesServerName(Uri url, string? serverName)
    {
        ArgumentNullException.ThrowIfNull(url);

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
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static string NormalizeHostname(string hostname)
    {
        return hostname.Trim().TrimEnd('.');
    }
}
