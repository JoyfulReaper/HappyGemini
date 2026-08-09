namespace HappyGemini.Events;

public sealed record GeminiServiceStartedEvent(string ListenAddress)
{
    public const string EventName = "happygemini.service.started";
}
