using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using HappyGemini.Events;
using HappyGemini.Extensibility;
using HappyGemini.Server;
using HappyGemini.Telemetry;
using JoyfulReaperLib.MissionControl;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HappyGemini.Tests;

public sealed class TelemetryServiceTests
{
    [Fact]
    public async Task PublishPageServedTelemetryAsync_DoesNotPublishWhenDisabled()
    {
        TestMissionControlClient client = new();
        TelemetryService service = CreateService(client, enabled: false);

        await service.PublishPageServedTelemetryAsync(
            1,
            CreateResult("/page", 20, succeeded: true),
            CancellationToken.None
        );

        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task PublishPageServedTelemetryAsync_PublishesSafeRequestData()
    {
        TestMissionControlClient client = new();
        TelemetryService service = CreateService(client);
        await using MemoryStream output = new();
        GeminiResponseWriter response = new(output);
        await response.WriteHeaderAsync(GeminiStatusCode.Success, "text/gemini");

        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        string correlationId = Guid.NewGuid().ToString("N");
        GeminiSessionResult result = GeminiSessionResult.Create(
            new GeminiRequest(new Uri("gemini://example.test/page?secret=value")),
            response,
            new IPEndPoint(IPAddress.Loopback, 12345),
            durationMilliseconds: 7,
            responseCompleted: true,
            occurredAt,
            correlationId
        );

        await service.PublishPageServedTelemetryAsync(42, result, CancellationToken.None);

        Assert.Equal(GeminiPageServedEvent.EventName, client.EventType);
        Assert.Equal(occurredAt, client.OccurredAt);
        Assert.Equal(correlationId, client.CorrelationId);

        GeminiPageServedEvent payload = Assert.IsType<GeminiPageServedEvent>(client.Payload);
        Assert.Equal("example.test", payload.Host);
        Assert.Equal("/page", payload.Path);
        Assert.Equal(20, payload.StatusCode);
        Assert.Equal("127.0.0.1:12345", payload.Remote);
        Assert.Equal(7, payload.DurationMilliseconds);
        Assert.True(payload.Succeeded);

        string json = JsonSerializer.Serialize(
            payload,
            HappyGeminiJsonContext.Default.GeminiPageServedEvent
        );
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("value", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(GeminiStatusCode.Input, true, true)]
    [InlineData(GeminiStatusCode.Success, true, true)]
    [InlineData(GeminiStatusCode.TemporaryRedirect, true, true)]
    [InlineData(GeminiStatusCode.TemporaryFailure, true, false)]
    [InlineData(GeminiStatusCode.NotFound, true, false)]
    [InlineData(GeminiStatusCode.ProxyRequestRefused, true, false)]
    [InlineData(GeminiStatusCode.Success, false, false)]
    public async Task GeminiSessionResult_UsesStatusClassAndCompletionForSuccess(
        GeminiStatusCode statusCode,
        bool responseCompleted,
        bool expectedSucceeded
    )
    {
        await using MemoryStream output = new();
        GeminiResponseWriter response = new(output);
        await response.WriteHeaderAsync(statusCode, MetaFor(statusCode));

        GeminiSessionResult result = GeminiSessionResult.Create(
            new GeminiRequest(new Uri("gemini://example.test/resource")),
            response,
            new IPEndPoint(IPAddress.Loopback, 12345),
            durationMilliseconds: 0,
            responseCompleted,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N")
        );

        Assert.Equal((int)statusCode, result.StatusCode);
        Assert.Equal(expectedSucceeded, result.Succeeded);
    }

    [Fact]
    public async Task GeminiSessionResult_CompletedStaticSuccessIsSuccessful()
    {
        await using MemoryStream output = new();
        GeminiResponseWriter response = new(output);
        await response.WriteHeaderAsync(GeminiStatusCode.Success, "text/plain");

        GeminiSessionResult result = GeminiSessionResult.Create(
            new GeminiRequest(new Uri("gemini://example.test/static.txt")),
            response,
            new IPEndPoint(IPAddress.Loopback, 12345),
            durationMilliseconds: 1,
            responseCompleted: true,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N")
        );

        Assert.Equal("/static.txt", result.Path);
        Assert.Equal(20, result.StatusCode);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task GeminiSessionResult_StartedPartialDynamicResponseIsNotSuccessful()
    {
        await using MemoryStream output = new();
        GeminiResponseWriter response = new(output);
        await response.WriteHeaderAsync(GeminiStatusCode.Success, "text/gemini");
        await response.WriteTextAsync("partial");

        GeminiSessionResult result = GeminiSessionResult.Create(
            new GeminiRequest(new Uri("gemini://example.test/dynamic")),
            response,
            new IPEndPoint(IPAddress.IPv6Loopback, 12345),
            durationMilliseconds: 0,
            responseCompleted: false,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N")
        );

        Assert.Equal(20, result.StatusCode);
        Assert.Equal("[::1]:12345", result.Remote);
        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(PublishBehavior.ReturnFalse)]
    [InlineData(PublishBehavior.Throw)]
    [InlineData(PublishBehavior.Timeout)]
    public async Task PublishPageServedTelemetryAsync_SwallowsPublishingFailure(
        PublishBehavior behavior
    )
    {
        TestMissionControlClient client = new(behavior);
        TelemetryService service = CreateService(client);

        await service.PublishPageServedTelemetryAsync(
            1,
            CreateResult("/page", 20, succeeded: true),
            CancellationToken.None
        );

        Assert.Equal(1, client.CallCount);
    }

    private static TelemetryService CreateService(
        IMissionControlClient client,
        bool enabled = true
    ) =>
        new(
            client,
            Options.Create(new MissionControlClientOptions { Enabled = enabled }),
            NullLogger<TelemetryService>.Instance
        );

    private static GeminiSessionResult CreateResult(
        string path,
        int statusCode,
        bool succeeded
    ) =>
        new(
            Host: "example.test",
            Path: path,
            StatusCode: statusCode,
            Remote: "127.0.0.1:12345",
            DurationMilliseconds: 0,
            Succeeded: succeeded,
            OccurredAt: DateTimeOffset.UtcNow,
            CorrelationId: Guid.NewGuid().ToString("N")
        );

    private static string MetaFor(GeminiStatusCode statusCode) =>
        ((int)statusCode / 10) switch
        {
            1 => "Enter input",
            2 => "text/gemini",
            3 => "/redirected",
            _ => "Request failed",
        };

    public enum PublishBehavior
    {
        Succeed,
        ReturnFalse,
        Throw,
        Timeout,
    }

    private sealed class TestMissionControlClient(
        PublishBehavior behavior = PublishBehavior.Succeed
    ) : IMissionControlClient
    {
        public int CallCount { get; private set; }
        public string? EventType { get; private set; }
        public object? Payload { get; private set; }
        public DateTimeOffset OccurredAt { get; private set; }
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
            OccurredAt = occurredAt;
            CorrelationId = correlationId;

            return behavior switch
            {
                PublishBehavior.Succeed => Task.FromResult(true),
                PublishBehavior.ReturnFalse => Task.FromResult(false),
                PublishBehavior.Throw => Task.FromException<bool>(
                    new InvalidOperationException("Mission Control failed.")
                ),
                PublishBehavior.Timeout => WaitForCancellationAsync(cancellationToken),
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        private static async Task<bool> WaitForCancellationAsync(
            CancellationToken cancellationToken
        )
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return true;
        }
    }
}
