namespace HappyGemini.Tests;

public sealed class GeminiHostValidatorTests
{
    [Theory]
    [InlineData("gemini://one.example/", "one.example", true)]
    [InlineData("gemini://one.example/", " ONE.EXAMPLE. ", true)]
    [InlineData("gemini://one.example/", "two.example", false)]
    [InlineData("gemini://one.example/", null, false)]
    [InlineData("gemini://127.0.0.1/", null, true)]
    public void MatchesServerName_ValidatesDnsRequestAgainstSni(
        string url,
        string? serverName,
        bool expected)
    {
        GeminiHostValidator validator = new();

        bool matches =
            validator.MatchesServerName(
                new Uri(url),
                serverName);

        Assert.Equal(
            expected,
            matches);
    }
}
