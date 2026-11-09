namespace HappyGeminiPages.RandomSteam.Integrations.RandomSteamGame;

public sealed class RandomSteamOptions
{
    public const string SectionName = "RandomSteam";

    public string BaseUrl { get; set; } = "https://randomsteam.kgivler.com/";
    public int TimeoutMilliseconds { get; set; } = 5000;
}
