using System.Net;
using System.Net.Http.Json;

namespace HappyGeminiPages.RandomSteam.Integrations.RandomSteamGame;

public sealed class RandomSteamClient(HttpClient httpClient) : IRandomSteamClient
{
    public async Task<GameDetails?> GetRandomGameAsync(
        long steamId,
        CancellationToken cancellationToken = default)
    {
        if (steamId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(steamId));
        }

        using HttpResponseMessage response = await httpClient.GetAsync(
            $"api/steam/random-game/details?userId={steamId}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<GameDetails>(
            cancellationToken);
    }
}
