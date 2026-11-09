
using HappyGemini.Extensibility;
using HappyGeminiPages.RandomSteam.Integrations.RandomSteamGame;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;

namespace HappyGeminiPages.RandomSteam.Tests;

public sealed class RandomSteamGamePageTests
{
    private const long SteamId = 76561197988408972;

    [Fact]
    public async Task NoSteamId_ReturnsInputPrompt()
    {
        var client = new FakeRandomSteamClient();
        string response = await ExecuteAsync(
            "gemini://localhost/randomsteam", client);

        Assert.StartsWith("10 Enter your 17-digit SteamID64\r\n", response);
        Assert.Equal(0, client.CallCount);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("12345")]
    [InlineData("00000000000000000")]
    public async Task InvalidSteamId_ReturnsInputPrompt(string input)
    {
        var client = new FakeRandomSteamClient();
        string response = await ExecuteAsync(
            $"gemini://localhost/randomsteam?{input}", client);

        Assert.StartsWith("10 Invalid Steam ID.", response);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task ValidSteamId_ReturnsGameWithRerollLink()
    {
        var client = new FakeRandomSteamClient
        {
            Game = new GameDetails
            {
                Id = 123456,
                Name = "Test Game",
                Description = "A game about testing.",
                PlaytimeForever = 90
            }
        };

        string response = await ExecuteAsync(
            $"gemini://localhost/randomsteam?{SteamId}", client);

        Assert.StartsWith("20 text/gemini; charset=utf-8\r\n", response);
        Assert.Contains("## Your game: Test Game", response);
        Assert.Contains("Steam App ID: 123456", response);
        Assert.Contains("Playtime: 1.5 hours", response);
        Assert.Contains("> A game about testing.", response);
        Assert.Contains("=> https://store.steampowered.com/app/123456/", response);
        Assert.Contains($"=> /randomsteam?{SteamId} Pick another random game", response);
        Assert.Equal(SteamId, client.LastSteamId);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task ApiRateLimit_ReturnsGeminiSlowDown()
    {
        var client = new FakeRandomSteamClient
        {
            Exception = new HttpRequestException(
                "Too many requests",
                null,
                HttpStatusCode.TooManyRequests)
        };

        string response = await ExecuteAsync(
            $"gemini://localhost/randomsteam?{SteamId}", client);

        Assert.StartsWith("44 RandomSteam is rate limited.", response);
        Assert.Equal(1, client.CallCount);
    }

    private static async Task<string> ExecuteAsync(
        string url, IRandomSteamClient client)
    {
        var page = new RandomSteamGamePage(
            client,
            NullLogger<RandomSteamGamePage>.Instance);

        using var stream = new MemoryStream();
        var writer = new GeminiResponseWriter(stream);
        var request = new GeminiRequest(new Uri(url));

        await page.WriteAsync(request, writer, CancellationToken.None);

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private sealed class FakeRandomSteamClient : IRandomSteamClient
    {
        public GameDetails? Game { get; init; }
        public Exception? Exception { get; init; }
        public long? LastSteamId { get; private set; }
        public int CallCount { get; private set; }

        public Task<GameDetails?> GetRandomGameAsync(
            long steamId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastSteamId = steamId;

            if (Exception is not null)
                return Task.FromException<GameDetails?>(Exception);

            return Task.FromResult(Game);
        }
    }
}
