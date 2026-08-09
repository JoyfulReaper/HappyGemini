using System.Net;
using HappyGemini.Extensibility;

namespace HappyGemini.Server;

internal sealed record GeminiSessionResult(
    string Host,
    string Path,
    int? StatusCode,
    string Remote,
    long DurationMilliseconds,
    bool Succeeded,
    DateTimeOffset OccurredAt,
    string CorrelationId
)
{
    internal static GeminiSessionResult Create(
        GeminiRequest request,
        GeminiResponseWriter response,
        EndPoint? remote,
        long durationMilliseconds,
        bool responseCompleted,
        DateTimeOffset occurredAt,
        string correlationId
    )
    {
        int? statusCode = response.StatusCode is GeminiStatusCode status
            ? (int)status
            : null;

        bool succeeded =
            responseCompleted && statusCode is >= 10 and < 40;

        return new GeminiSessionResult(
            Host: request.Url.IdnHost,
            Path: request.Url.AbsolutePath,
            StatusCode: statusCode,
            Remote: remote?.ToString() ?? "unknown",
            DurationMilliseconds: durationMilliseconds,
            Succeeded: succeeded,
            OccurredAt: occurredAt,
            CorrelationId: correlationId
        );
    }
}
