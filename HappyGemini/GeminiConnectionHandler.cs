using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using HappyGemini.Extensibility;
using HappyGemini.Pages;
using JoyfulReaperLib.TcpServer;
using Microsoft.Extensions.Options;

namespace HappyGemini.Server;

public sealed class GeminiConnectionHandler(
    GeminiCertificateProvider certificateProvider,
    IOptions<GeminiServerOptions> options,
    GeminiPageResolver pageResolver,
    GeminiContentStore contentStore,
    GeminiHostValidator hostValidator,
    GeminiVirtualHostResolver virtualHostResolver,
    ILogger<GeminiConnectionHandler> logger
) : ITcpConnectionHandler
{
    private readonly GeminiServerOptions _options = options.Value;

    public async ValueTask HandleAsync(
        TcpConnectionContext context,
        CancellationToken cancellationToken
    )
    {
        await using var sslStream = new SslStream(context.Stream, leaveInnerStreamOpen: true);

        try
        {
            using var handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );

            handshakeTimeout.CancelAfter(_options.HandshakeTimeout);

            var authenticationOptions = new SslServerAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                ServerCertificateSelectionCallback = (_, hostname) =>
                    certificateProvider.SelectCertificate(hostname),
            };

            await sslStream.AuthenticateAsServerAsync(
                authenticationOptions,
                handshakeTimeout.Token
            );

            using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );

            requestTimeout.CancelAfter(_options.RequestTimeout);

            GeminiResponseWriter response = new(sslStream);

            GeminiRequest? request = await GeminiRequestReader.ReadAsync(
                sslStream,
                requestTimeout.Token
            );

            if (request is null)
            {
                await response.WriteHeaderAsync(
                    GeminiStatusCode.BadRequest,
                    "Bad request",
                    requestTimeout.Token
                );

                await sslStream.ShutdownAsync();
                return;
            }

            request = request with
            {
                RemoteEndPoint = context.RemoteEndPoint as IPEndPoint,
                LocalEndPoint = context.LocalEndPoint as IPEndPoint,
            };

            logger.LogInformation(
                "Gemini request for host {Host}, path {Path}, from {Remote}",
                request.Url.IdnHost,
                request.Url.AbsolutePath,
                context.RemoteEndPoint
            );

            GeminiVirtualHost? virtualHost = virtualHostResolver.Resolve(request.Url);

            if (virtualHost is null)
            {
                await response.WriteHeaderAsync(
                    GeminiStatusCode.ProxyRequestRefused,
                    "Host not served",
                    requestTimeout.Token
                );

                await sslStream.ShutdownAsync();
                return;
            }

            if (!hostValidator.MatchesServerName(request.Url, sslStream.TargetHostName))
            {
                logger.LogDebug(
                    "Gemini request host {RequestHost} did not match TLS SNI host {ServerName}.",
                    request.Url.IdnHost,
                    sslStream.TargetHostName
                );

                await response.WriteHeaderAsync(
                    GeminiStatusCode.ProxyRequestRefused,
                    "TLS server name does not match request host",
                    requestTimeout.Token
                );

                await sslStream.ShutdownAsync();
                return;
            }

            if (!hostValidator.TargetsServerPort(request.Url, _options.Port))
            {
                await response.WriteHeaderAsync(
                    GeminiStatusCode.ProxyRequestRefused,
                    "Port not served",
                    requestTimeout.Token
                );

                await sslStream.ShutdownAsync();
                return;
            }

            IGeminiPage? page = pageResolver.Resolve(virtualHost, request.Url.AbsolutePath);

            if (page is not null)
            {
                try
                {
                    await page.WriteAsync(request, response, requestTimeout.Token);
                }
                catch (Exception exception)
                    when (exception is not OperationCanceledException and not IOException)
                {
                    Type pageType = page.GetType();

                    logger.LogError(
                        exception,
                        "Gemini page {PageType} failed for host {RequestHost}, path {RequestPath}, from {Remote}.",
                        pageType.FullName ?? pageType.Name,
                        request.Url.IdnHost,
                        request.Url.AbsolutePath,
                        context.RemoteEndPoint
                    );

                    if (!response.HasStarted)
                    {
                        await response.WriteHeaderAsync(
                            GeminiStatusCode.TemporaryFailure,
                            "Temporary failure",
                            requestTimeout.Token
                        );
                    }
                }

                await sslStream.ShutdownAsync();
                return;
            }

            if (
                contentStore.TryResolve(virtualHost, request.Url.AbsolutePath, out string? filePath)
                && filePath is not null
            )
            {
                string contentType = GeminiContentTypeProvider.GetContentType(filePath);

                await response.WriteHeaderAsync(
                    GeminiStatusCode.Success,
                    contentType,
                    requestTimeout.Token
                );

                await using FileStream fileStream = File.OpenRead(filePath);

                await response.WriteStreamAsync(fileStream, requestTimeout.Token);

                await sslStream.ShutdownAsync();
                return;
            }

            await response.WriteHeaderAsync(
                GeminiStatusCode.NotFound,
                "Not found",
                requestTimeout.Token
            );

            await sslStream.ShutdownAsync();
        }
        catch (AuthenticationException exception)
        {
            logger.LogDebug(exception, "TLS handshake failed for {Remote}", context.RemoteEndPoint);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("Gemini connection from {Remote} timed out.", context.RemoteEndPoint);
        }
        catch (IOException exception)
        {
            logger.LogDebug(
                exception,
                "Gemini connection from {Remote} ended unexpectedly.",
                context.RemoteEndPoint
            );
        }
    }
}
