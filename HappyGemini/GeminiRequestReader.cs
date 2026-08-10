using System.Text;
using HappyGemini.Extensibility;

namespace HappyGemini.Server;

public static class GeminiRequestReader
{
    private const int MaximumUriLength = 1024;
    private const string AllowedWireCharacters = "-._~:/?#@!$&'()*+,;=";

    private static readonly UTF8Encoding Utf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );

    public static async ValueTask<GeminiRequest?> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(stream);

        string? request = await ReadRequestLineAsync(stream, cancellationToken);

        if (!TryParseRequest(request, out Uri? uri) || uri is null)
        {
            return null;
        }

        return new GeminiRequest(uri);
    }

    private static async ValueTask<string?> ReadRequestLineAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        // 1024 bytes URI + CRLF.
        byte[] buffer = new byte[MaximumUriLength + 2];

        int count = 0;

        while (count < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(count), cancellationToken);

            if (read == 0)
            {
                return null;
            }

            int previousCount = count;
            count += read;

            int scanStart = Math.Max(1, previousCount);

            for (int i = scanStart; i < count; i++)
            {
                if (buffer[i - 1] != '\r' || buffer[i] != '\n')
                {
                    continue;
                }

                int uriLength = i - 1;

                if (uriLength > MaximumUriLength)
                {
                    return null;
                }

                try
                {
                    return Utf8.GetString(buffer, 0, uriLength);
                }
                catch (DecoderFallbackException)
                {
                    return null;
                }
            }
        }

        return null;
    }

    private static bool TryParseRequest(string? request, out Uri? uri)
    {
        uri = null;

        if (string.IsNullOrWhiteSpace(request))
        {
            return false;
        }

        if (!IsValidWireUri(request))
        {
            return false;
        }

        if (!Uri.TryCreate(request, UriKind.Absolute, out Uri? parsed))
        {
            return false;
        }

        if (!string.Equals(parsed.Scheme, "gemini", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrEmpty(parsed.Host))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(parsed.UserInfo))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(parsed.Fragment))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    private static bool IsValidWireUri(string value)
    {
        int schemeSeparator = value.IndexOf(':');

        if (
            schemeSeparator <= 0
            || !char.IsAsciiLetter(value[0])
            || schemeSeparator + 2 >= value.Length
            || value[schemeSeparator + 1] != '/'
            || value[schemeSeparator + 2] != '/'
        )
        {
            return false;
        }

        for (int i = 1; i < schemeSeparator; i++)
        {
            char character = value[i];

            if (
                !char.IsAsciiLetterOrDigit(character)
                && character is not '+' and not '-' and not '.'
            )
            {
                return false;
            }
        }

        int authorityStart = schemeSeparator + 3;
        int authorityEnd = value.IndexOfAny(['/', '?', '#'], authorityStart);

        if (authorityEnd < 0)
        {
            authorityEnd = value.Length;
        }

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];

            if (character <= ' ' || character >= '\x7f' || character == '\\')
            {
                return false;
            }

            if (character == '%')
            {
                if (
                    i + 2 >= value.Length
                    || !IsHexDigit(value[i + 1])
                    || !IsHexDigit(value[i + 2])
                )
                {
                    return false;
                }

                i += 2;
                continue;
            }

            if (
                char.IsAsciiLetterOrDigit(character)
                || AllowedWireCharacters.Contains(character)
                || character is '[' or ']' && i >= authorityStart && i < authorityEnd
            )
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool IsHexDigit(char character)
    {
        return character is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f';
    }
}
