namespace HappyGeminiPages.RandomSteam.Integrations.RandomSteamGame;

public interface IRandomSteamClient
{
    Task<GameDetails?> GetRandomGameAsync(long steamId, CancellationToken cancellationToken = default);
}
