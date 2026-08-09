using System.Net;
using System.Diagnostics;
using System.Net.Security;
using System.Security.Authentication;
using HappyGemini.Extensibility;
using HappyGemini.Pages;
using HappyGemini.Telemetry;
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
    TelemetryService telemetryService,
    ILogger<GeminiConnectionHandler> logger
) : ITcpConnectionHandler
{
    private readonly GeminiServerOptions _options = options.Value;

    public async ValueTask HandleAsync(
        TcpConnectionContext context,
        CancellationToken cancellationToken
    )
    {
        GeminiSessionResult? result = await ProcessAsync(context, cancellationToken);

        if (result is null)
        {
            return;
        }

        long connectionId = context.ConnectionId;

        context.RegisterAfterClose(afterCloseToken =>
            telemetryService.PublishPageServedTelemetryAsync(
                connectionId,
                result,
                afterCloseToken
            )
        );
    }

    private async Task<GeminiSessionResult?> ProcessAsync(
        TcpConnectionContext context,
        CancellationToken cancellationToken
    )
    {
        await using var sslStream = new SslStream(context.Stream, leaveInnerStreamOpen: true);

        GeminiRequest? request = null;
        GeminiResponseWriter? response = null;
        Stopwatch? stopwatch = null;
        DateTimeOffset occurredAt = default;
        string? correlationId = null;
        bool responseCompleted = false;

        GeminiSessionResult? CreateResult()
        {
            if (
                request is null
                || response is null
                || stopwatch is null
                || correlationId is null
            )
            {
                return null;
            }

            return GeminiSessionResult.Create(
                request,
                response,
                context.RemoteEndPoint,
                stopwatch.ElapsedMilliseconds,
                responseCompleted,
                occurredAt,
                correlationId
            );
        }

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

            occurredAt = DateTimeOffset.UtcNow;
            correlationId = Guid.NewGuid().ToString("N");
            stopwatch = Stopwatch.StartNew();

            using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );

            requestTimeout.CancelAfter(_options.RequestTimeout);

            response = new GeminiResponseWriter(sslStream);

            request = await GeminiRequestReader.ReadAsync(
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
                return null;
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
                responseCompleted = true;
                return CreateResult();
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
                responseCompleted = true;
                return CreateResult();
            }

            if (!hostValidator.TargetsServerPort(request.Url, _options.Port))
            {
                await response.WriteHeaderAsync(
                    GeminiStatusCode.ProxyRequestRefused,
                    "Port not served",
                    requestTimeout.Token
                );

                await sslStream.ShutdownAsync();
                responseCompleted = true;
                return CreateResult();
            }

            IGeminiPage? page = pageResolver.Resolve(virtualHost, request.Url.AbsolutePath);

            if (page is not null)
            {
                bool shouldShutdownGracefully = await ExecutePageAsync(
                    page,
                    request,
                    response,
                    context.RemoteEndPoint,
                    logger,
                    requestTimeout.Token
                );

                if (shouldShutdownGracefully)
                {
                    await sslStream.ShutdownAsync();
                    responseCompleted = true;
                }

                return CreateResult();
            }

            if (
                contentStore.TryResolve(virtualHost, request.Url.AbsolutePath, out string? filePath)
                && filePath is not null
            )
            {
                FileStream fileStream;

                try
                {
                    fileStream = File.OpenRead(filePath);
                }
                catch (IOException exception)
                    when (exception is FileNotFoundException or DirectoryNotFoundException)
                {
                    logger.LogDebug(
                        "Resolved static file disappeared for host {RequestHost}, path {RequestPath}, from {Remote}.",
                        request.Url.IdnHost,
                        request.Url.AbsolutePath,
                        context.RemoteEndPoint
                    );

                    await response.WriteHeaderAsync(
                        GeminiStatusCode.NotFound,
                        "Not found",
                        requestTimeout.Token
                    );

                    await sslStream.ShutdownAsync();
                    responseCompleted = true;
                    return CreateResult();
                }
                catch (Exception exception)
                    when (exception is UnauthorizedAccessException or IOException)
                {
                    logger.LogWarning(
                        "Unable to open static file for host {RequestHost}, path {RequestPath}, from {Remote}: {ErrorType}.",
                        request.Url.IdnHost,
                        request.Url.AbsolutePath,
                        context.RemoteEndPoint,
                        exception.GetType().Name
                    );

                    await response.WriteHeaderAsync(
                        GeminiStatusCode.TemporaryFailure,
                        "Temporary failure",
                        requestTimeout.Token
                    );

                    await sslStream.ShutdownAsync();
                    responseCompleted = true;
                    return CreateResult();
                }

                await using (fileStream)
                {
                    string contentType = GeminiContentTypeProvider.GetContentType(filePath);

                    await response.WriteHeaderAsync(
                        GeminiStatusCode.Success,
                        contentType,
                        requestTimeout.Token
                    );

                    await response.WriteStreamAsync(fileStream, requestTimeout.Token);
                }

                await sslStream.ShutdownAsync();
                responseCompleted = true;
                return CreateResult();
            }

            await response.WriteHeaderAsync(
                GeminiStatusCode.NotFound,
                "Not found",
                requestTimeout.Token
            );

            await sslStream.ShutdownAsync();
            responseCompleted = true;
            return CreateResult();
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
        finally
        {
            stopwatch?.Stop();
        }

        return CreateResult();
    }

    internal static async Task<bool> ExecutePageAsync(
        IGeminiPage page,
        GeminiRequest request,
        GeminiResponseWriter response,
        EndPoint? remoteEndPoint,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        Type pageType = page.GetType();
        string pageTypeName = pageType.FullName ?? pageType.Name;

        try
        {
            await page.WriteAsync(request, response, cancellationToken);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException and not IOException)
        {
            logger.LogError(
                exception,
                "Gemini page {PageType} failed for host {RequestHost}, path {RequestPath}, from {Remote}.",
                pageTypeName,
                request.Url.IdnHost,
                request.Url.AbsolutePath,
                remoteEndPoint
            );

            if (response.HasStarted)
            {
                return false;
            }

            await response.WriteHeaderAsync(
                GeminiStatusCode.TemporaryFailure,
                "Temporary failure",
                cancellationToken
            );

            return true;
        }

        if (!response.HasStarted)
        {
            logger.LogWarning(
                "Gemini page {PageType} completed without a response for host {RequestHost}, path {RequestPath}, from {Remote}.",
                pageTypeName,
                request.Url.IdnHost,
                request.Url.AbsolutePath,
                remoteEndPoint
            );

            await response.WriteHeaderAsync(
                GeminiStatusCode.TemporaryFailure,
                "Temporary failure",
                cancellationToken
            );
        }

        return true;
    }
}
