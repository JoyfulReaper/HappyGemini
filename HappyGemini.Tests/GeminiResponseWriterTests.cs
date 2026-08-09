using System.Text;
using HappyGemini.Extensibility;

namespace HappyGemini.Tests;

public sealed class GeminiResponseWriterTests
{
    [Fact]
    public async Task HasStarted_ChangesAfterHeaderIsWritten()
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        Assert.False(writer.HasStarted);

        await writer.WriteHeaderAsync(GeminiStatusCode.Success, "text/plain");

        Assert.True(writer.HasStarted);
    }

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
    public async Task WriteHeaderAsync_AllowsUnicodeInputPrompt()
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await writer.WriteHeaderAsync(GeminiStatusCode.Input, "Search for café 世界 🔍");

        Assert.Equal("10 Search for café 世界 🔍\r\n", ReadStream(stream));
    }

    [Theory]
    [InlineData(GeminiStatusCode.Input, "Prompt\ttext")]
    [InlineData(GeminiStatusCode.TemporaryFailure, "Error\0text")]
    [InlineData(GeminiStatusCode.ClientCertificateRequired, "Error\u007ftext")]
    public async Task WriteHeaderAsync_RejectsControlCharactersInPromptOrErrorMeta(
        GeminiStatusCode status,
        string meta
    )
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await writer.WriteHeaderAsync(status, meta)
        );
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("text/plain; charset=utf-8")]
    [InlineData("text/plain; charset=\"utf-8\"")]
    [InlineData("text/gemini; charset=utf-8")]
    [InlineData("application/json")]
    [InlineData("application/octet-stream")]
    public async Task WriteHeaderAsync_AllowsValidMimeType(string meta)
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await writer.WriteHeaderAsync(GeminiStatusCode.Success, meta);

        Assert.Equal($"20 {meta}\r\n", ReadStream(stream));
    }

    [Theory]
    [InlineData("not a mime")]
    [InlineData("text")]
    [InlineData("/plain")]
    [InlineData("text/")]
    [InlineData(" text/plain")]
    [InlineData("text/plain ")]
    [InlineData("text/plain; charset")]
    [InlineData("text/plain; foo")]
    [InlineData("text/plain; name=\"café\"")]
    [InlineData("text/pläin")]
    public async Task WriteHeaderAsync_RejectsInvalidMimeType(string meta)
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await writer.WriteHeaderAsync(GeminiStatusCode.Success, meta)
        );

        Assert.False(writer.HasStarted);
        Assert.Empty(stream.ToArray());
    }

    [Theory]
    [InlineData("x:opaque")]
    [InlineData("a:/path")]
    [InlineData("scheme:")]
    [InlineData("mailto:user@example.com")]
    [InlineData("urn:isbn:0451450523")]
    [InlineData("gemini://example.com/")]
    [InlineData("gemini://example.com/new-location")]
    [InlineData("gemini://[::1]/new-location")]
    [InlineData("//example.com/path")]
    [InlineData("//user@example.com/path")]
    [InlineData("//[::1]/")]
    [InlineData("//[v1.fe]/")]
    [InlineData("//[vF.example]/")]
    [InlineData("//[v1.a:b]/")]
    [InlineData("//example.com:/")]
    [InlineData("/absolute/path")]
    [InlineData("/new-location")]
    [InlineData("relative/path")]
    [InlineData("../relative")]
    [InlineData("./this:that")]
    [InlineData("?query")]
    [InlineData("#fragment")]
    [InlineData("relative?one?two")]
    [InlineData("relative#frag?still-fragment")]
    [InlineData("%61")]
    [InlineData("/path%20with%20encoding")]
    public async Task WriteHeaderAsync_AllowsValidRedirect(string meta)
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await writer.WriteHeaderAsync(GeminiStatusCode.TemporaryRedirect, meta);

        Assert.Equal($"30 {meta}\r\n", ReadStream(stream));
    }

    [Theory]
    [InlineData("not a uri")]
    [InlineData("%ZZ")]
    [InlineData("bad%escape")]
    [InlineData("%")]
    [InlineData("%1")]
    [InlineData("://bad")]
    [InlineData("1invalid:relative")]
    [InlineData("relative/世界")]
    [InlineData("bad\\path")]
    [InlineData("multiple#fragments#bad")]
    [InlineData("//[v]/")]
    [InlineData("//[v1.]/")]
    [InlineData("//[vXYZ.foo]/")]
    [InlineData("//[::1/")]
    [InlineData("//example.com:invalid/")]
    [InlineData("/bad[path")]
    [InlineData("relative]path")]
    [InlineData("relative?bad[query]")]
    [InlineData("relative#bad[fragment]")]
    public async Task WriteHeaderAsync_RejectsMalformedRedirect(string meta)
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await writer.WriteHeaderAsync(GeminiStatusCode.TemporaryRedirect, meta)
        );

        Assert.False(writer.HasStarted);
        Assert.Empty(stream.ToArray());
    }

    [Theory]
    [InlineData(GeminiStatusCode.TemporaryFailure)]
    [InlineData(GeminiStatusCode.PermanentFailure)]
    [InlineData(GeminiStatusCode.ClientCertificateRequired)]
    public async Task WriteHeaderAsync_AllowsEmptyOptionalErrorMeta(GeminiStatusCode status)
    {
        await using MemoryStream stream = new();

        GeminiResponseWriter writer = new(stream);

        await writer.WriteHeaderAsync(status, string.Empty);

        Assert.Equal($"{(int)status:D2}\r\n", ReadStream(stream));
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
