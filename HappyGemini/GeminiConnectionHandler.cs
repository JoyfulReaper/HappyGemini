using HappyGemini.Extensibility;
using JoyfulReaperLib.TcpServer;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;

namespace HappyGemini.Server;

public sealed class GeminiConnectionHandler(
    GeminiCertificateProvider certificateProvider,
    IOptions<GeminiServerOptions> options,
    ILogger<GeminiConnectionHandler> logger) : ITcpConnectionHandler
{
    private static readonly UTF8Encoding Utf8 =
        new(encoderShouldEmitUTF8Identifier: false);

    private readonly GeminiServerOptions _options = options.Value;

    public async ValueTask HandleAsync(
        TcpConnectionContext context,
        CancellationToken cancellationToken)
    {
        await using var sslStream = new SslStream(
            context.Stream,
            leaveInnerStreamOpen: true);

        try
        {
            using var handshakeTimeout =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            handshakeTimeout.CancelAfter(
                _options.HandshakeTimeout);

            var authenticationOptions =
                new SslServerAuthenticationOptions
                {
                    EnabledSslProtocols =
                        SslProtocols.Tls12 |
                        SslProtocols.Tls13,

                    ServerCertificateSelectionCallback =
                        (_, hostname) =>
                            certificateProvider.SelectCertificate(
                                hostname)
                };

            await sslStream.AuthenticateAsServerAsync(
                authenticationOptions,
                handshakeTimeout.Token);

            using var requestTimeout =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            requestTimeout.CancelAfter(
                _options.RequestTimeout);

            GeminiRequest? request =
                await GeminiRequestReader.ReadAsync(
                    sslStream,
                    requestTimeout.Token);

            if (request is null)
            {
                await WriteAsync(
                    sslStream,
                    "59 Bad request\r\n",
                    requestTimeout.Token);

                await sslStream.ShutdownAsync();
                return;
            }

            request = request with
            {
                RemoteEndPoint =
                    context.RemoteEndPoint as IPEndPoint,

                LocalEndPoint =
                    context.LocalEndPoint as IPEndPoint
            };

            logger.LogInformation(
                "Gemini request {Uri} from {Remote}",
                request.Url,
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

    private static ValueTask WriteAsync(
        Stream stream,
        string value,
        CancellationToken cancellationToken)
    {
        byte[] bytes = Utf8.GetBytes(value);

        return stream.WriteAsync(
            bytes,
            cancellationToken);
    }
}