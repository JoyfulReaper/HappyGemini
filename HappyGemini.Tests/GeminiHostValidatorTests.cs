namespace HappyGemini.Tests;

public sealed class GeminiHostValidatorTests
{
    [Theory]
    [InlineData("gemini://example.com/", 1965, true)]
    [InlineData("gemini://example.com:1965/", 1965, true)]
    [InlineData("gemini://example.com:1966/", 1965, false)]
    [InlineData("gemini://example.com:1966/", 1966, true)]
    [InlineData("gemini://example.com/", 1966, false)]
    [InlineData("gemini://127.0.0.1:1966/", 1966, true)]
    [InlineData("gemini://[::1]:1966/", 1966, true)]
    public void TargetsServerPort_UsesGeminiDefaultOrExplicitPort(
        string url,
        int serverPort,
        bool expected
    )
    {
        GeminiHostValidator validator = new();

        bool matches = validator.TargetsServerPort(new Uri(url), serverPort);

        Assert.Equal(expected, matches);
    }

    [Theory]
    [InlineData("gemini://one.example/", "one.example", true)]
    [InlineData("gemini://one.example/", " ONE.EXAMPLE. ", true)]
    [InlineData("gemini://one.example/", "two.example", false)]
    [InlineData("gemini://one.example/", null, false)]
    [InlineData("gemini://127.0.0.1/", null, true)]
    public void MatchesServerName_ValidatesDnsRequestAgainstSni(
        string url,
        string? serverName,
        bool expected
    )
    {
        GeminiHostValidator validator = new();

        bool matches = validator.MatchesServerName(new Uri(url), serverName);

        Assert.Equal(expected, matches);
    }
}
