
using HappyGemini.Extensibility;
using HappyGeminiPages.RandomSteam.Integrations.RandomSteamGame;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;

namespace HappyGeminiPages.RandomSteam;

[AutoRegisterGeminiPage]
public sealed class RandomSteamGamePage(
    IRandomSteamClient randomSteamClient,
    ILogger<RandomSteamGamePage> logger) : IGeminiPage
{
    public string Path => "/randomsteam";

    public async Task WriteAsync(
        GeminiRequest request,
        GeminiResponseWriter response,
        CancellationToken cancellationToken)
    {
        string input = request.Url.Query.TrimStart('?');

        if (input.Length == 0)
        {
            await response.WriteHeaderAsync(
                GeminiStatusCode.Input,
                "Enter your 17-digit SteamID64",
                cancellationToken);
            return;
        }

        if (!SteamIdInputParser.TryParse(input, out long steamId))
        {
            await response.WriteHeaderAsync(
                GeminiStatusCode.Input,
                "Invalid Steam ID. Enter a 17-digit SteamID64",
                cancellationToken);
            return;
        }

        GameDetails? game;

        try
        {
            game = await randomSteamClient.GetRandomGameAsync(
                steamId, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            await response.WriteHeaderAsync(
                GeminiStatusCode.SlowDown,
                "RandomSteam is rate limited. Try again shortly.",
                cancellationToken);
            return;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "RandomSteam API request failed.");

            await response.WriteHeaderAsync(
                GeminiStatusCode.TemporaryFailure,
                "RandomSteam is temporarily unavailable.",
                cancellationToken);
            return;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "RandomSteam returned invalid JSON.");

            await response.WriteHeaderAsync(
                GeminiStatusCode.TemporaryFailure,
                "RandomSteam returned an invalid response.",
                cancellationToken);
            return;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("RandomSteam API request timed out.");

            await response.WriteHeaderAsync(
                GeminiStatusCode.TemporaryFailure,
                "RandomSteam request timed out.",
                cancellationToken);
            return;
        }

        var text = new StringBuilder();

        text.AppendLine("# Random Steam Game Picker");
        text.AppendLine();

        if (game is null)
        {
            text.AppendLine("No game found. Check your Steam library privacy settings.");
        }
        else
        {
            text.AppendLine($"## Your game: {SanitizeLine(game.Name)}");
            text.AppendLine();
            text.AppendLine($"Steam App ID: {game.Id}");
            text.AppendLine($"Playtime: {game.PlaytimeForever / 60.0:0.0} hours");

            string[] description = SteamDescriptionFormatter.Format(game.Description).ToArray();

            if (description.Length > 0)
            {
                text.AppendLine();
                text.AppendLine("### Description");
                text.AppendLine();

                foreach (string line in description)
                {
                    text.AppendLine($"> {line}");
                }
            }

            if (game.Id > 0)
            {
                text.AppendLine();
                text.AppendLine($"=> https://store.steampowered.com/app/{game.Id}/ View game on Steam");
            }
        }

        text.AppendLine();
        text.AppendLine($"=> /randomsteam?{steamId} Pick another random game");
        text.AppendLine("=> /randomsteam Enter a different Steam ID");
        text.AppendLine("=> / Back to main menu");

        await response.WriteHeaderAsync(
            GeminiStatusCode.Success,
            "text/gemini; charset=utf-8",
            cancellationToken);

        await response.WriteTextAsync(text.ToString(), cancellationToken);
    }

    private static string SanitizeLine(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
