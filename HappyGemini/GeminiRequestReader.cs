using System.Text;
using HappyGemini.Extensibility;

namespace HappyGemini.Server;

public static class GeminiRequestReader
{
    private const int MaximumUriLength = 1024;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static async ValueTask<GeminiRequest?> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(stream);

        string? request = await ReadRequestLineAsync(stream, cancellationToken);

        if (!TryParseRequest(request, out Uri? uri))
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

                return Utf8.GetString(buffer, 0, uriLength);
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
}
