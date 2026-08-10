using System.Text;
using HappyGemini.Extensibility;
using HappyGemini.Pages;

namespace HappyGemini.Tests;

public sealed class ServerTimePageTests
{
    [Fact]
    public async Task WriteAsync_WritesCurrentUtcTimeAsGemtextResponse()
    {
        DateTimeOffset currentTime = new(
            2026,
            8,
            10,
            0,
            0,
            0,
            TimeSpan.Zero
        );
        ServerTimePage page = new(new FixedTimeProvider(currentTime));
        await using MemoryStream output = new();
        GeminiResponseWriter response = new(output);

        await page.WriteAsync(
            new GeminiRequest(new Uri("gemini://example.test/server-time")),
            response,
            CancellationToken.None
        );

        Assert.Equal("/server-time", page.Path);
        Assert.Equal(
            "20 text/gemini; charset=utf-8\r\n"
                + "# HappyGemini server time\r\n\r\n"
                + "UTC: 2026-08-10T00:00:00.0000000+00:00\r\n",
            Encoding.UTF8.GetString(output.ToArray())
        );
    }

    [Fact]
    public void Resolver_LeavesRootRouteAvailableForStaticContent()
    {
        ServerTimePage page = new(new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        GeminiPageResolver resolver = new([page]);
        GeminiVirtualHost virtualHost = new(
            "example.test",
            Path.GetTempPath(),
            "index.gmi",
            useGlobalPages: true
        );

        Assert.Null(resolver.Resolve(virtualHost, "/"));
        Assert.Same(page, resolver.Resolve(virtualHost, "/server-time"));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
