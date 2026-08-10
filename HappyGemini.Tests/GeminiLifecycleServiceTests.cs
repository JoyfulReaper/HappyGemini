using System.Text.Json.Serialization.Metadata;
using HappyGemini.Events;
using HappyGemini.Server;
using JoyfulReaperLib.MissionControl;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HappyGemini.Tests;

public sealed class GeminiLifecycleServiceTests
{
    [Fact]
    public async Task StartedAsync_DoesNotPublishWhenMissionControlIsDisabled()
    {
        RecordingClient client = new();
        GeminiLifecycleService service = CreateService(client, enabled: false);

        await service.StartedAsync(CancellationToken.None);

        Assert.Equal(0, client.CallCount);
    }

    [Theory]
    [InlineData("127.0.0.1", "127.0.0.1:1965")]
    [InlineData("::", "[::]:1965")]
    public async Task StartedAsync_PublishesFormattedListenEndpoint(
        string listenAddress,
        string expectedEndpoint
    )
    {
        RecordingClient client = new();
        GeminiLifecycleService service = CreateService(
            client,
            enabled: true,
            listenAddress
        );

        await service.StartedAsync(CancellationToken.None);

        Assert.Equal(1, client.CallCount);
        Assert.Equal(GeminiServiceStartedEvent.EventName, client.EventType);
        Assert.Null(client.CorrelationId);
        GeminiServiceStartedEvent payload = Assert.IsType<GeminiServiceStartedEvent>(
            client.Payload
        );
        Assert.Equal(expectedEndpoint, payload.ListenAddress);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartedAsync_DoesNotFailWhenPublishingFails(bool throwException)
    {
        RecordingClient client = new(throwException);
        GeminiLifecycleService service = CreateService(client, enabled: true);

        await service.StartedAsync(CancellationToken.None);

        Assert.Equal(1, client.CallCount);
    }

    private static GeminiLifecycleService CreateService(
        IMissionControlClient client,
        bool enabled,
        string listenAddress = "127.0.0.1"
    ) =>
        new(
            NullLogger<GeminiLifecycleService>.Instance,
            client,
            Options.Create(new MissionControlClientOptions { Enabled = enabled }),
            Options.Create(
                new GeminiServerOptions
                {
                    ListenAddress = listenAddress,
                    Port = 1965,
                    Hostnames = ["example.test"],
                }
            )
        );

    private sealed class RecordingClient(bool throwException = false)
        : IMissionControlClient
    {
        public int CallCount { get; private set; }
        public string? EventType { get; private set; }
        public object? Payload { get; private set; }
        public string? CorrelationId { get; private set; }

        public Task<bool> TryPublishAsync<TPayload>(
            string eventType,
            TPayload payload,
            JsonTypeInfo<TPayload> payloadTypeInfo,
            DateTimeOffset occurredAt,
            string? correlationId = null,
            CancellationToken cancellationToken = default
        )
        {
            CallCount++;
            EventType = eventType;
            Payload = payload;
            CorrelationId = correlationId;

            return throwException
                ? Task.FromException<bool>(new InvalidOperationException("Publish failed."))
                : Task.FromResult(true);
        }
    }
}
