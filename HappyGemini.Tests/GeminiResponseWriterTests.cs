using System.Text;
using HappyGemini.Extensibility;

namespace HappyGemini.Tests;

public sealed class GeminiResponseWriterTests
{
    [Fact]
    public async Task WriteHeaderAsync_WritesSuccessHeader()
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await writer.WriteHeaderAsync(GeminiStatusCode.Success, "text/gemini; charset=utf-8");

        Assert.Equal("20 text/gemini; charset=utf-8\r\n", ReadStream(stream));
    }

    [Fact]
    public async Task WriteHeaderAsync_AllowsFailureWithoutMeta()
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await writer.WriteHeaderAsync(GeminiStatusCode.PermanentFailure);

        Assert.Equal("50\r\n", ReadStream(stream));
    }

    [Fact]
    public async Task WriteHeaderAsync_RequiresMetaForSuccess()
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await writer.WriteHeaderAsync(GeminiStatusCode.Success)
        );
    }

    [Fact]
    public async Task WriteHeaderAsync_RequiresMetaForInput()
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await writer.WriteHeaderAsync(GeminiStatusCode.Input)
        );
    }

    [Fact]
    public async Task WriteHeaderAsync_RequiresMetaForRedirect()
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await writer.WriteHeaderAsync(GeminiStatusCode.TemporaryRedirect)
        );
    }

    [Fact]
    public async Task WriteHeaderAsync_RejectsLineBreaksInMeta()
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await writer.WriteHeaderAsync(GeminiStatusCode.Success, "text/plain\r\nEVIL")
        );
    }

    [Fact]
    public async Task WriteHeaderAsync_RejectsSecondHeader()
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await writer.WriteHeaderAsync(GeminiStatusCode.Success, "text/plain");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await writer.WriteHeaderAsync(GeminiStatusCode.Success, "text/plain")
        );
    }

    [Fact]
    public async Task WriteHeaderAsync_RejectsUndefinedStatus()
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await writer.WriteHeaderAsync((GeminiStatusCode)99)
        );
    }

    [Fact]
    public async Task WriteTextAsync_RejectsBodyBeforeHeader()
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await writer.WriteTextAsync("body")
        );
    }

    [Fact]
    public async Task WriteTextAsync_RejectsBodyForNonSuccessResponse()
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await writer.WriteHeaderAsync(GeminiStatusCode.NotFound, "Not found");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await writer.WriteTextAsync("body")
        );
    }

    [Fact]
    public async Task WriteTextAsync_WritesUtf8BodyForSuccessResponse()
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await writer.WriteHeaderAsync(GeminiStatusCode.Success, "text/plain; charset=utf-8");

        await writer.WriteTextAsync("héllo");

        Assert.Equal("20 text/plain; charset=utf-8\r\nhéllo", ReadStream(stream));
    }

    private static string ReadStream(MemoryStream stream)
    {
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
