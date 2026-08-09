using System.Text;

namespace HappyGemini.Extensibility;

/// <summary>
/// Writes Gemini response headers and text bodies to a connection.
/// </summary>
public sealed class GeminiResponseWriter
{
    private static readonly UTF8Encoding Utf8 =
        new(encoderShouldEmitUTF8Identifier: false);

    private readonly Stream _output;
    private bool _headerWritten;

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
        string meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(meta);

        if (_headerWritten)
        {
            throw new InvalidOperationException(
                "The Gemini response header has already been written.");
        }

        if (meta.Contains('\r') || meta.Contains('\n'))
        {
            throw new ArgumentException(
                "Gemini response metadata must not contain line breaks.",
                nameof(meta));
        }

        string header =
            $"{(int)status:D2} {meta}\r\n";

        await WriteRawAsync(
            header,
            cancellationToken);

        _headerWritten = true;
    }

    /// <summary>
    /// Writes UTF-8 text to the Gemini response body.
    /// </summary>
    public ValueTask WriteTextAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!_headerWritten)
        {
            throw new InvalidOperationException(
                "The Gemini response header must be written before the body.");
        }

        return WriteRawAsync(
            text,
            cancellationToken);
    }

    private ValueTask WriteRawAsync(
        string value,
        CancellationToken cancellationToken)
    {
        byte[] bytes = Utf8.GetBytes(value);

        return _output.WriteAsync(
            bytes,
            cancellationToken);
    }
}