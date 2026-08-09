using HappyGemini.Extensibility;

namespace HappyGemini.Pages;

public sealed class HomePage : IGeminiPage
{
    public string Path => "/";

    public async Task WriteAsync(
        GeminiRequest request,
        GeminiResponseWriter response,
        CancellationToken cancellationToken)
    {
        await response.WriteHeaderAsync(
            GeminiStatusCode.Success,
            "text/gemini; charset=utf-8",
            cancellationToken);

        await response.WriteTextAsync(
            "# HappyGemini\r\n\r\nIt lives.\r\n",
            cancellationToken);
    }
}