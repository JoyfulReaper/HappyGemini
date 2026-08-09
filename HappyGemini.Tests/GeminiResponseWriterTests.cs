using HappyGemini.Extensibility;
using System.Text;

namespace HappyGemini.Tests;

public sealed class GeminiResponseWriterTests
{
    [Fact]
    public async Task WriteHeaderAsync_WritesSuccessHeader()
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await writer.WriteHeaderAsync(
            GeminiStatusCode.Success,
            "text/gemini; charset=utf-8");

        Assert.Equal(
            "20 text/gemini; charset=utf-8\r\n",
            ReadStream(stream));
    }

    [Fact]
    public async Task WriteHeaderAsync_AllowsFailureWithoutMeta()
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await writer.WriteHeaderAsync(
            GeminiStatusCode.PermanentFailure);

        Assert.Equal(
            "50\r\n",
            ReadStream(stream));
    }

    [Fact]
    public async Task WriteHeaderAsync_RequiresMetaForSuccess()
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await writer.WriteHeaderAsync(
                    GeminiStatusCode.Success));
    }

    [Fact]
    public async Task WriteHeaderAsync_RequiresMetaForInput()
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await writer.WriteHeaderAsync(
                    GeminiStatusCode.Input));
    }

    [Fact]
    public async Task WriteHeaderAsync_RequiresMetaForRedirect()
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await writer.WriteHeaderAsync(
                    GeminiStatusCode.TemporaryRedirect));
    }

    [Fact]
    public async Task WriteHeaderAsync_RejectsLineBreaksInMeta()
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await writer.WriteHeaderAsync(
                    GeminiStatusCode.Success,
                    "text/plain\r\nEVIL"));
    }

    private static string ReadStream(
        MemoryStream stream)
    {
        return Encoding.UTF8.GetString(
            stream.ToArray());
    }
}