using HappyGemini.Events;
using HappyGemini.Server;
using JoyfulReaperLib.MissionControl;
using Microsoft.Extensions.Options;

namespace HappyGemini.Telemetry;

public sealed class TelemetryService(
    IMissionControlClient missionControlClient,
    IOptions<MissionControlClientOptions> missionControlOptions,
    ILogger<TelemetryService> logger
)
{
    private static readonly TimeSpan TelemetryPublishTimeout = TimeSpan.FromSeconds(2);

    internal async ValueTask PublishPageServedTelemetryAsync(
        long connectionId,
        GeminiSessionResult result,
        CancellationToken cancellationToken
    )
    {
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
                eventType: GeminiPageServedEvent.EventName,
                payload: new GeminiPageServedEvent(
                    result.Host,
                    result.Path,
                    result.StatusCode,
                    result.Remote,
                    result.DurationMilliseconds,
                    result.Succeeded
                ),
                payloadTypeInfo: HappyGeminiJsonContext.Default.GeminiPageServedEvent,
                occurredAt: result.OccurredAt,
                correlationId: result.CorrelationId,
                cancellationToken: timeout.Token
            );

            if (!published)
            {
                logger.LogWarning(
                    "Mission Control did not accept telemetry for host {Host}, path {Path}, on connection {ConnectionId}.",
                    result.Host,
                    result.Path,
                    connectionId
                );
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(
                "Telemetry publishing stopped for host {Host}, path {Path}, on connection {ConnectionId}.",
                result.Host,
                result.Path,
                connectionId
            );
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "Timed out publishing telemetry for host {Host}, path {Path}, on connection {ConnectionId}.",
                result.Host,
                result.Path,
                connectionId
            );
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to publish telemetry for host {Host}, path {Path}, on connection {ConnectionId}.",
                result.Host,
                result.Path,
                connectionId
            );
        }
    }
}
