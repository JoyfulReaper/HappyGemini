namespace HappyGeminiPages.RandomSteam.Integrations.RandomSteamGame;

public sealed record GameDetails
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int PlaytimeForever { get; init; }
}
