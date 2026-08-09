using System.Net;
using System.Text;
using HappyGemini.Extensibility;
using HappyGemini.Server;
using Microsoft.Extensions.Logging.Abstractions;

namespace HappyGemini.Tests;

public sealed class GeminiConnectionHandlerTests
{
    [Fact]
    public async Task ExecutePageAsync_WritesTemporaryFailureWhenPageWritesNoResponse()
    {
        TestPage page = new((_, _, _) => Task.CompletedTask);

        (bool shouldShutdownGracefully, MemoryStream output) = await ExecuteAsync(page);

        Assert.True(shouldShutdownGracefully);
        Assert.Equal("40 Temporary failure\r\n", ReadStream(output));
    }

    [Fact]
    public async Task ExecutePageAsync_IsolatesExceptionBeforeResponseStarts()
    {
        TestPage page = new(
            (_, _, _) => Task.FromException(new InvalidOperationException("Sensitive failure"))
        );

        (bool shouldShutdownGracefully, MemoryStream output) = await ExecuteAsync(page);

        Assert.True(shouldShutdownGracefully);
        Assert.Equal("40 Temporary failure\r\n", ReadStream(output));
        Assert.DoesNotContain("Sensitive failure", ReadStream(output));
    }

    [Fact]
    public async Task ExecutePageAsync_DoesNotGracefullyCompleteFailedStartedResponse()
    {
        TestPage page = new(
            async (_, response, cancellationToken) =>
            {
                await response.WriteHeaderAsync(
                    GeminiStatusCode.Success,
                    "text/plain",
                    cancellationToken
                );
                await response.WriteTextAsync("partial body", cancellationToken);
                throw new InvalidOperationException("Page failed");
            }
        );

        (bool shouldShutdownGracefully, MemoryStream output) = await ExecuteAsync(page);

        Assert.False(shouldShutdownGracefully);
        Assert.Equal("20 text/plain\r\npartial body", ReadStream(output));
    }

    [Fact]
    public async Task ExecutePageAsync_GracefullyCompletesSuccessfulResponse()
    {
        TestPage page = new(
            async (_, response, cancellationToken) =>
            {
                await response.WriteHeaderAsync(
                    GeminiStatusCode.Success,
                    "text/plain",
                    cancellationToken
                );
                await response.WriteTextAsync("complete body", cancellationToken);
            }
        );

        (bool shouldShutdownGracefully, MemoryStream output) = await ExecuteAsync(page);

        Assert.True(shouldShutdownGracefully);
        Assert.Equal("20 text/plain\r\ncomplete body", ReadStream(output));
    }

    [Fact]
    public async Task ExecutePageAsync_DoesNotCatchOperationCanceledException()
    {
        TestPage page = new(
            (_, _, _) => Task.FromException(new OperationCanceledException())
        );

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await ExecuteAsync(page)
        );
    }

    [Fact]
    public async Task ExecutePageAsync_DoesNotCatchIOException()
    {
        TestPage page = new((_, _, _) => Task.FromException(new IOException("I/O failed")));

        await Assert.ThrowsAsync<IOException>(async () => await ExecuteAsync(page));
    }

    private static async Task<(bool ShouldShutdownGracefully, MemoryStream Output)> ExecuteAsync(
        IGeminiPage page
    )
    {
        MemoryStream output = new();
        GeminiResponseWriter response = new(output);
        GeminiRequest request = new(new Uri("gemini://example.com/test?sensitive-query"));

        bool shouldShutdownGracefully = await GeminiConnectionHandler.ExecutePageAsync(
            page,
            request,
            response,
            new IPEndPoint(IPAddress.Loopback, 12345),
            NullLogger.Instance,
            CancellationToken.None
        );

        return (shouldShutdownGracefully, output);
    }

    private static string ReadStream(MemoryStream stream)
    {
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private sealed class TestPage(
        Func<GeminiRequest, GeminiResponseWriter, CancellationToken, Task> writeAsync
    ) : IGeminiPage
    {
        public string Path => "/test";

        public Task WriteAsync(
            GeminiRequest request,
            GeminiResponseWriter response,
            CancellationToken cancellationToken
        )
        {
            return writeAsync(request, response, cancellationToken);
        }
    }
}
