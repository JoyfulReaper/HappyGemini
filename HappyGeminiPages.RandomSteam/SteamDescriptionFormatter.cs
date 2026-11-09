
using System.Net;
using System.Text.RegularExpressions;

namespace HappyGeminiPages.RandomSteam;

internal static class SteamDescriptionFormatter
{
    private const int LineWidth = 70;
    private const int MaxCharacters = 420;

    public static IEnumerable<string> Format(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            yield break;
        }

        string plain = Regex.Replace(
            description, "<[^>]*>", " ",
            RegexOptions.Singleline, TimeSpan.FromMilliseconds(250));

        plain = WebUtility.HtmlDecode(plain);

        plain = Regex.Replace(
            plain, @"\s+", " ",
            RegexOptions.None, TimeSpan.FromMilliseconds(250)).Trim();

        plain = Regex.Replace(
            plain, @"\s+([.,!?;:])", "$1",
            RegexOptions.None, TimeSpan.FromMilliseconds(250));

        if (plain.Length == 0)
        {
            yield break;
        }

        if (plain.Length > MaxCharacters)
        {
            plain = plain[..MaxCharacters].TrimEnd() + "...";
        }

        while (plain.Length > LineWidth)
        {
            int split = plain.LastIndexOf(' ', LineWidth);

            if (split <= 0)
            {
                split = LineWidth;
            }

            yield return plain[..split];
            plain = plain[split..].TrimStart();
        }

        if (plain.Length > 0)
        {
            yield return plain;
        }
    }
}

