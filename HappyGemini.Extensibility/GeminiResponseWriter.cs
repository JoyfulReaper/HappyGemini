using System.Text;

namespace HappyGemini.Extensibility;

/// <summary>
/// Writes Gemini response headers and bodies to a connection.
/// </summary>
public sealed class GeminiResponseWriter
{
    private static readonly UTF8Encoding Utf8 =
        new(encoderShouldEmitUTF8Identifier: false);

    private readonly Stream _output;
    private bool _headerWritten;
    private int? _statusClass;

    public GeminiResponseWriter(Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _output = output;
    }

    /// <summary>
    /// Writes the Gemini response header.
    /// </summary>
    public async ValueTask WriteHeaderAsync(
        GeminiStatusCode status,
        string? meta = null,
        CancellationToken cancellationToken = default)
    {
        if (_headerWritten)
        {
            throw new InvalidOperationException(
                "The Gemini response header has already been written.");
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The Gemini status code is not defined.");
        }

        if (meta is not null &&
            (meta.Contains('\r') || meta.Contains('\n')))
        {
            throw new ArgumentException(
                "Gemini response metadata must not contain line breaks.",
                nameof(meta));
        }

        int statusValue = (int)status;
        int statusClass = statusValue / 10;

        if (statusClass is 1 or 2 or 3 &&
            string.IsNullOrEmpty(meta))
        {
            throw new ArgumentException(
                "Gemini 1x, 2x, and 3x responses require metadata.",
                nameof(meta));
        }

        string header =
            string.IsNullOrEmpty(meta)
                ? $"{statusValue:D2}\r\n"
                : $"{statusValue:D2} {meta}\r\n";

        byte[] bytes =
            Utf8.GetBytes(header);

        await _output.WriteAsync(
            bytes,
            cancellationToken);

        _headerWritten = true;
        _statusClass = statusClass;
    }

    /// <summary>
    /// Writes UTF-8 text to the Gemini response body.
    /// </summary>
    public ValueTask WriteTextAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        EnsureBodyAllowed();

        byte[] bytes =
            Utf8.GetBytes(text);

        return _output.WriteAsync(
            bytes,
            cancellationToken);
    }

    /// <summary>
    /// Writes raw bytes to the Gemini response body.
    /// </summary>
    public ValueTask WriteBytesAsync(
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken = default)
    {
        EnsureBodyAllowed();

        return _output.WriteAsync(
            bytes,
            cancellationToken);
    }

    /// <summary>
    /// Copies a stream to the Gemini response body.
    /// </summary>
    public async Task WriteStreamAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        EnsureBodyAllowed();

        await source.CopyToAsync(
            _output,
            cancellationToken);
    }

    private void EnsureBodyAllowed()
    {
        if (!_headerWritten)
        {
            throw new InvalidOperationException(
                "The Gemini response header must be written before the body.");
        }

        if (_statusClass != 2)
        {
            throw new InvalidOperationException(
                "Gemini response bodies are only valid for 2x success responses.");
        }
    }
}