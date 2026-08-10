using System.Net;
using HappyGemini.Events;
using JoyfulReaperLib.MissionControl;
using Microsoft.Extensions.Options;

namespace HappyGemini.Server;

public sealed class GeminiLifecycleService(
    ILogger<GeminiLifecycleService> logger,
    IMissionControlClient missionControlClient,
    IOptions<MissionControlClientOptions> missionControlOptions,
    IOptions<GeminiServerOptions> options
) : IHostedLifecycleService
{
    private static readonly TimeSpan TelemetryPublishTimeout = TimeSpan.FromSeconds(2);

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartedAsync(CancellationToken cancellationToken)
    {
        GeminiServerOptions serverOptions = options.Value;
        string listenEndPoint = new IPEndPoint(
            ParseListenAddress(serverOptions.ListenAddress),
            serverOptions.Port
        ).ToString();

        logger.LogInformation(
            "HappyGemini server listening on {ListenEndPoint}; dual mode is {DualMode}; serving {HostCount} host(s).",
            listenEndPoint,
            serverOptions.DualMode,
            serverOptions.Hostnames.Length
        );

        if (!missionControlOptions.Value.Enabled)
        {
            return;
        }

        using CancellationTokenSource timeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TelemetryPublishTimeout); // TODO: Make configurable.

        try
        {
            bool published = await missionControlClient.TryPublishAsync(
                eventType: GeminiServiceStartedEvent.EventName,
                payload: new GeminiServiceStartedEvent(listenEndPoint),
                payloadTypeInfo: HappyGeminiJsonContext.Default.GeminiServiceStartedEvent,
                occurredAt: DateTimeOffset.UtcNow,
                correlationId: null,
                cancellationToken: timeout.Token
            );

            if (!published)
            {
                logger.LogWarning(
                    "Mission Control did not accept {EventType}.",
                    GeminiServiceStartedEvent.EventName
                );
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("HappyGemini startup telemetry publishing was canceled.");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Timed out publishing HappyGemini startup telemetry.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to publish HappyGemini startup telemetry.");
        }
    }

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("HappyGemini server stopping...");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("HappyGemini server stopped.");
        return Task.CompletedTask;
    }

    private static IPAddress ParseListenAddress(string value)
    {
        if (value is "*" or "+" or "0.0.0.0")
        {
            return IPAddress.Any;
        }

        if (value == "::")
        {
            return IPAddress.IPv6Any;
        }

        if (!IPAddress.TryParse(value, out IPAddress? address))
        {
            throw new InvalidOperationException($"Invalid Gemini listen address: {value}.");
        }

        return address;
    }
}
