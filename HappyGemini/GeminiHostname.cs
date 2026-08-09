using System.Globalization;
using System.Net;

namespace HappyGemini;

internal static class GeminiHostname
{
    private static readonly IdnMapping Idn = new() { UseStd3AsciiRules = true };

    public static bool TryNormalize(string? hostname, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(hostname))
        {
            return false;
        }

        string value = hostname.Trim().TrimEnd('.');

        if (value.Length == 0)
        {
            return false;
        }

        if (value[0] == '[' || value[^1] == ']')
        {
            if (
                value.Length < 3
                || value[0] != '['
                || value[^1] != ']'
                || !IPAddress.TryParse(value[1..^1], out IPAddress? bracketedAddress)
            )
            {
                return false;
            }

            normalized = bracketedAddress.ToString();
            return true;
        }

        if (IPAddress.TryParse(value, out IPAddress? address))
        {
            normalized = address.ToString();
            return true;
        }

        string asciiHostname;

        try
        {
            asciiHostname = Idn.GetAscii(value);
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (Uri.CheckHostName(asciiHostname) != UriHostNameType.Dns)
        {
            return false;
        }

        normalized = asciiHostname.ToLowerInvariant();
        return true;
    }

    public static string Normalize(string hostname)
    {
        if (!TryNormalize(hostname, out string normalized))
        {
            throw new ArgumentException($"'{hostname}' is not a valid hostname.", nameof(hostname));
        }

        return normalized;
    }
}
