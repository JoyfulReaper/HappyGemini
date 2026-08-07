using JoyfulReaperLib.TcpServer;
using Microsoft.Extensions.Options;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;

namespace HappyGemini.Server;

public sealed class GeminiConnectionHandler(
    GeminiCertificateProvider certificateProvider,
    IOptions<GeminiServerOptions> options,
    ILogger<GeminiConnectionHandler> logger) : ITcpConnectionHandler
{
    private const int MaximumUriLength = 1024;
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private readonly GeminiServerOptions _options = options.Value;

    public async ValueTask HandleAsync(
        TcpConnectionContext context,
        CancellationToken cancellationToken)
    {
        await using var sslStream = new SslStream(context.Stream, leaveInnerStreamOpen: true);

        try
        {
            using var handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            handshakeTimeout.CancelAfter(_options.HandshakeTimeout);

            var authenticationOptions = new SslServerAuthenticationOptions
            {
                EnabledSslProtocols =
                    SslProtocols.Tls12 |
                    SslProtocols.Tls13,

                ServerCertificateSelectionCallback = (_, hostname) => certificateProvider
                    .SelectCertificate(hostname)
            };

            await sslStream.AuthenticateAsServerAsync(authenticationOptions, handshakeTimeout.Token);

            using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestTimeout.CancelAfter(_options.RequestTimeout);

            string? request = await ReadRequestAsync(sslStream, requestTimeout.Token);
            if (!TryParseRequest(request, out Uri? uri))
            {
                await WriteAsync(
                    sslStream,
                    "59 Bad request\r\n",
                    requestTimeout.Token);

                await sslStream.ShutdownAsync();
                return;
            }

            logger.LogInformation(
                "Gemini request {Uri} from {Remote}",
                uri,
                context.RemoteEndPoint);

            await WriteAsync(
                sslStream,
                """
                20 text/gemini; charset=utf-8
                # HappyGemini

                It lives.

                """.ReplaceLineEndings("\r\n"),
                requestTimeout.Token);

            await sslStream.ShutdownAsync();
        }
        catch (AuthenticationException exception)
        {
            logger.LogDebug(
                exception,
                "TLS handshake failed for {Remote}",
                context.RemoteEndPoint);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(
                "Gemini connection from {Remote} timed out.",
                context.RemoteEndPoint);
        }
        catch (IOException exception)
        {
            logger.LogDebug(
                exception,
                "Gemini connection from {Remote} ended unexpectedly.",
                context.RemoteEndPoint);
        }
    }

    private static async ValueTask<string?> ReadRequestAsync(
        Stream stream,
        CancellationToken cancellationToken)
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
                if (buffer[i - 1] != '\r' ||
                    buffer[i] != '\n')
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

    private static ValueTask WriteAsync(
        Stream stream,
        string value,
        CancellationToken cancellationToken)
    {
        byte[] bytes = Utf8.GetBytes(value);
        return stream.WriteAsync(bytes, cancellationToken);
    }
}