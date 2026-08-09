using System.Text;
namespace HappyGemini.Extensibility;

/// <summary>
/// Writes Gemini response headers and bodies to a connection.
/// </summary>
public sealed class GeminiResponseWriter
{
    private const string UriReferenceCharacters = "-._~:/?#[]@!$&'()*+,;=";

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

            if (character == '%')
            {
                if (
                    i + 2 >= meta.Length
                    || !Uri.IsHexDigit(meta[i + 1])
                    || !Uri.IsHexDigit(meta[i + 2])
                )
                {
                    return false;
                }

                i += 2;
                continue;
            }

            if (
                character is '[' or ']'
                && fragmentStart >= 0
                && i > fragmentStart
            )
            {
                return false;
            }

            if (
                !char.IsAsciiLetterOrDigit(character)
                && !UriReferenceCharacters.Contains(character)
            )
            {
                return false;
            }
        }

        if (Uri.IsWellFormedUriString(meta, UriKind.RelativeOrAbsolute))
        {
            return true;
        }

        if (fragmentStart < 0)
        {
            return false;
        }

        string referenceWithoutFragment = meta[..fragmentStart];

        return referenceWithoutFragment.Length == 0
            || Uri.IsWellFormedUriString(
                referenceWithoutFragment,
                UriKind.RelativeOrAbsolute
            );
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
