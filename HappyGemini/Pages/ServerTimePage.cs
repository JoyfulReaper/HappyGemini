using HappyGemini.Extensibility;

namespace HappyGemini.Pages;

[AutoRegisterGeminiPage]
public sealed class ServerTimePage(TimeProvider timeProvider) : IGeminiPage
{
    public const string PagePath = "/server-time";

    public string Path => PagePath;

    public async Task WriteAsync(
        GeminiRequest request,
        GeminiResponseWriter response,
        CancellationToken cancellationToken
    )
    {
        await response.WriteHeaderAsync(
            GeminiStatusCode.Success,
            "text/gemini; charset=utf-8",
            cancellationToken
        );

        string currentTime = timeProvider
            .GetUtcNow()
            .ToString("O", System.Globalization.CultureInfo.InvariantCulture);

        await response.WriteTextAsync(
            $"# HappyGemini server time\r\n\r\nUTC: {currentTime}\r\n",
            cancellationToken
        );
    }
}
