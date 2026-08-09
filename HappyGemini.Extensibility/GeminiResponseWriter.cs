using System.Text;
using System.Net;
using System.Net.Sockets;

namespace HappyGemini.Extensibility;

/// <summary>
/// Writes Gemini response headers and bodies to a connection.
/// </summary>
public sealed class GeminiResponseWriter
{
    private const string UriReferenceCharacters = "-._~:/?#[]@!$&'()*+,;=";
    private const string SubDelimiters = "!$&'()*+,;=";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Stream _output;
    private bool _headerWritten;
    private int? _statusClass;

    public GeminiResponseWriter(Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _output = output;
    }

    /// <summary>
    /// Gets whether the Gemini response header has been written.
    /// </summary>
    public bool HasStarted => _headerWritten;

    /// <summary>
    /// Gets the Gemini status code whose response header was written.
    /// </summary>
    public GeminiStatusCode? StatusCode { get; private set; }

    /// <summary>
    /// Writes the Gemini response header.
    /// </summary>
    public async ValueTask WriteHeaderAsync(
        GeminiStatusCode status,
        string? meta = null,
        CancellationToken cancellationToken = default
    )
    {
        if (_headerWritten)
        {
            throw new InvalidOperationException(
                "The Gemini response header has already been written."
            );
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The Gemini status code is not defined."
            );
        }

        if (meta is not null && (meta.Contains('\r') || meta.Contains('\n')))
        {
            throw new ArgumentException(
                "Gemini response metadata must not contain line breaks.",
                nameof(meta)
            );
        }

        int statusValue = (int)status;
        int statusClass = statusValue / 10;

        if (statusClass is 1 or 2 or 3 && string.IsNullOrEmpty(meta))
        {
            throw new ArgumentException(
                "Gemini 1x, 2x, and 3x responses require metadata.",
                nameof(meta)
            );
        }

        if (!IsValidMeta(statusClass, meta))
        {
            throw new ArgumentException(
                "Gemini response metadata is invalid for the status code.",
                nameof(meta)
            );
        }

        string header = string.IsNullOrEmpty(meta)
            ? $"{statusValue:D2}\r\n"
            : $"{statusValue:D2} {meta}\r\n";

        byte[] bytes = Utf8.GetBytes(header);

        await _output.WriteAsync(bytes, cancellationToken);

        _headerWritten = true;
        _statusClass = statusClass;
        StatusCode = status;
    }

    private static bool IsValidMeta(int statusClass, string? meta)
    {
        if (string.IsNullOrEmpty(meta))
        {
            return true;
        }

        return statusClass switch
        {
            1 or 4 or 5 or 6 => IsValidPromptOrErrorMessage(meta),
            2 => IsValidMediaType(meta),
            3 => IsValidUriReference(meta),
            _ => false,
        };
    }

    private static bool IsValidMediaType(string meta)
    {
        if (char.IsWhiteSpace(meta[0]) || char.IsWhiteSpace(meta[^1]))
        {
            return false;
        }

        foreach (char character in meta)
        {
            if (!char.IsAscii(character))
            {
                return false;
            }
        }

        if (
            !System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(
                meta,
                out System.Net.Http.Headers.MediaTypeHeaderValue? mediaType
            )
        )
        {
            return false;
        }

        return mediaType.Parameters.All(parameter => parameter.Value is not null);
    }

    private static bool IsValidPromptOrErrorMessage(string meta)
    {
        for (int i = 0; i < meta.Length; i++)
        {
            char character = meta[i];

            if (char.IsControl(character))
            {
                return false;
            }

            if (!char.IsSurrogate(character))
            {
                continue;
            }

            if (
                !char.IsHighSurrogate(character)
                || i + 1 >= meta.Length
                || !char.IsLowSurrogate(meta[++i])
            )
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidUriReference(string meta)
    {
        int fragmentStart = meta.IndexOf('#');

        if (fragmentStart >= 0 && meta.IndexOf('#', fragmentStart + 1) >= 0)
        {
            return false;
        }

        for (int i = 0; i < meta.Length; i++)
        {
            char character = meta[i];

            if (character <= ' ' || character >= '\x7f' || character == '\\')
            {
                return false;
            }

            if (character == '%')
            {
                if (
                    i + 2 >= meta.Length
                    || !IsHexDigit(meta[i + 1])
                    || !IsHexDigit(meta[i + 2])
                )
                {
                    return false;
                }

                i += 2;
                continue;
            }

            if (
                !char.IsAsciiLetterOrDigit(character)
                && !UriReferenceCharacters.Contains(character)
            )
            {
                return false;
            }
        }

        string reference = fragmentStart >= 0 ? meta[..fragmentStart] : meta;

        if (
            fragmentStart >= 0
            && !IsValidPCharSequence(meta[(fragmentStart + 1)..], allowSlashAndQuestion: true)
        )
        {
            return false;
        }

        int queryStart = reference.IndexOf('?');

        if (
            queryStart >= 0
            && !IsValidPCharSequence(reference[(queryStart + 1)..], allowSlashAndQuestion: true)
        )
        {
            return false;
        }

        string referencePath = queryStart >= 0 ? reference[..queryStart] : reference;
        int schemeSeparator = referencePath.IndexOf(':');

        if (
            schemeSeparator > 0
            && IsValidScheme(referencePath.AsSpan(0, schemeSeparator))
        )
        {
            return IsValidHierPart(referencePath[(schemeSeparator + 1)..]);
        }

        return IsValidRelativePart(referencePath);
    }

    private static bool IsValidScheme(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty || !char.IsAsciiLetter(value[0]))
        {
            return false;
        }

        for (int i = 1; i < value.Length; i++)
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

        return true;
    }

    private static bool IsValidHierPart(string value)
    {
        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            return IsValidAuthorityAndPath(value[2..]);
        }

        if (value.Length == 0)
        {
            return true;
        }

        return value[0] == '/'
            ? IsValidPathAbsolute(value)
            : IsValidPath(value, allowColonInFirstSegment: true);
    }

    private static bool IsValidRelativePart(string value)
    {
        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            return IsValidAuthorityAndPath(value[2..]);
        }

        if (value.Length == 0)
        {
            return true;
        }

        return value[0] == '/'
            ? IsValidPathAbsolute(value)
            : IsValidPath(value, allowColonInFirstSegment: false);
    }

    private static bool IsValidAuthorityAndPath(string value)
    {
        int pathStart = value.IndexOf('/');
        string authority = pathStart >= 0 ? value[..pathStart] : value;
        string path = pathStart >= 0 ? value[pathStart..] : string.Empty;

        return IsValidAuthority(authority)
            && (path.Length == 0
                || path[0] == '/' && IsValidPath(path, allowColonInFirstSegment: true));
    }

    private static bool IsValidAuthority(string value)
    {
        int userInfoEnd = value.IndexOf('@');
        string hostAndPort = value;

        if (userInfoEnd >= 0)
        {
            if (
                value.IndexOf('@', userInfoEnd + 1) >= 0
                || !IsValidUriCharacterSequence(
                    value[..userInfoEnd],
                    allowColon: true,
                    allowAt: false,
                    allowSlash: false,
                    allowQuestion: false
                )
            )
            {
                return false;
            }

            hostAndPort = value[(userInfoEnd + 1)..];
        }

        if (hostAndPort.StartsWith('['))
        {
            int literalEnd = hostAndPort.IndexOf(']');

            if (literalEnd < 0 || !IsValidIpLiteral(hostAndPort[1..literalEnd]))
            {
                return false;
            }

            string remainder = hostAndPort[(literalEnd + 1)..];

            return remainder.Length == 0
                || remainder[0] == ':' && IsValidPort(remainder[1..]);
        }

        if (hostAndPort.Contains('[') || hostAndPort.Contains(']'))
        {
            return false;
        }

        int portStart = hostAndPort.IndexOf(':');
        string host = hostAndPort;

        if (portStart >= 0)
        {
            if (
                hostAndPort.IndexOf(':', portStart + 1) >= 0
                || !IsValidPort(hostAndPort[(portStart + 1)..])
            )
            {
                return false;
            }

            host = hostAndPort[..portStart];
        }

        return IsValidUriCharacterSequence(
            host,
            allowColon: false,
            allowAt: false,
            allowSlash: false,
            allowQuestion: false
        );
    }

    private static bool IsValidIpLiteral(string value)
    {
        if (value.Length == 0 || value.Contains('%'))
        {
            return false;
        }

        if (value[0] is 'v' or 'V')
        {
            return IsValidIpvFuture(value);
        }

        return IPAddress.TryParse(value, out IPAddress? address)
            && address.AddressFamily == AddressFamily.InterNetworkV6;
    }

    private static bool IsValidIpvFuture(string value)
    {
        int position = 1;

        while (position < value.Length && IsHexDigit(value[position]))
        {
            position++;
        }

        if (position == 1 || position >= value.Length - 1 || value[position] != '.')
        {
            return false;
        }

        for (position++; position < value.Length; position++)
        {
            char character = value[position];

            if (!IsUnreserved(character) && !IsSubDelimiter(character) && character != ':')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidPort(string value)
    {
        foreach (char character in value)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidPathAbsolute(string value)
    {
        return value[0] == '/'
            && (value.Length == 1 || value[1] != '/')
            && IsValidPath(value, allowColonInFirstSegment: true);
    }

    private static bool IsValidPath(string value, bool allowColonInFirstSegment)
    {
        bool inFirstSegment = true;

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];

            if (character == '/')
            {
                inFirstSegment = false;
                continue;
            }

            if (character == '%')
            {
                i += 2;
                continue;
            }

            if (
                !IsPChar(character)
                || inFirstSegment && !allowColonInFirstSegment && character == ':'
            )
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidPCharSequence(string value, bool allowSlashAndQuestion)
    {
        return IsValidUriCharacterSequence(
            value,
            allowColon: true,
            allowAt: true,
            allowSlash: allowSlashAndQuestion,
            allowQuestion: allowSlashAndQuestion
        );
    }

    private static bool IsValidUriCharacterSequence(
        string value,
        bool allowColon,
        bool allowAt,
        bool allowSlash,
        bool allowQuestion
    )
    {
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];

            if (character == '%')
            {
                i += 2;
                continue;
            }

            if (
                !IsUnreserved(character)
                && !IsSubDelimiter(character)
                && !(allowColon && character == ':')
                && !(allowAt && character == '@')
                && !(allowSlash && character == '/')
                && !(allowQuestion && character == '?')
            )
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPChar(char character)
    {
        return IsUnreserved(character)
            || IsSubDelimiter(character)
            || character is ':' or '@';
    }

    private static bool IsUnreserved(char character)
    {
        return char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_' or '~';
    }

    private static bool IsSubDelimiter(char character)
    {
        return SubDelimiters.Contains(character);
    }

    private static bool IsHexDigit(char character)
    {
        return character is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f';
    }

    /// <summary>
    /// Writes UTF-8 text to the Gemini response body.
    /// </summary>
    public ValueTask WriteTextAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        EnsureBodyAllowed();

        byte[] bytes = Utf8.GetBytes(text);

        return _output.WriteAsync(bytes, cancellationToken);
    }

    /// <summary>
    /// Writes raw bytes to the Gemini response body.
    /// </summary>
    public ValueTask WriteBytesAsync(
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken = default
    )
    {
        EnsureBodyAllowed();

        return _output.WriteAsync(bytes, cancellationToken);
    }

    /// <summary>
    /// Copies a stream to the Gemini response body.
    /// </summary>
    public async Task WriteStreamAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        EnsureBodyAllowed();

        await source.CopyToAsync(_output, cancellationToken);
    }

    private void EnsureBodyAllowed()
    {
        if (!_headerWritten)
        {
            throw new InvalidOperationException(
                "The Gemini response header must be written before the body."
            );
        }

        if (_statusClass != 2)
        {
            throw new InvalidOperationException(
                "Gemini response bodies are only valid for 2x success responses."
            );
        }
    }
}
