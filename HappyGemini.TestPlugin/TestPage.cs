using HappyGemini.Extensibility;

namespace HappyGemini.TestPlugin;

[AutoRegisterGeminiPage]
public sealed class TestPage : IGeminiPage
{
    public string Path => "/plugin-test";

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

        await response.WriteTextAsync(
            """
            # External plugin test

            HappyGemini loaded this page from an external plugin.

            If you can read this, the entire plugin pipeline works.

            """.ReplaceLineEndings("\r\n"),
            cancellationToken
        );
    }
}
