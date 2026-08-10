namespace HappyGemini.Events;

internal sealed record GeminiPageServedEvent(
    string Host,
    string Path,
    int? StatusCode,
    string Remote,
    long DurationMilliseconds,
    bool Succeeded
)
{
    public const string EventName = "happygemini.page.served";
}
