
using System.Globalization;

namespace HappyGeminiPages.RandomSteam;

internal static class SteamIdInputParser
{
    public static bool TryParse(string? input, out long steamId)
    {
        steamId = default;

        string value = input?.Trim() ?? string.Empty;

        if (value.Length != 17 || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        return long.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out steamId) && steamId > 0;
    }
}

