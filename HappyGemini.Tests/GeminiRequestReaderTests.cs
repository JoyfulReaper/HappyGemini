using HappyGemini.Server;
using System.Text;

namespace HappyGemini.Tests;

public sealed class GeminiRequestReaderTests
{
    [Fact]
    public async Task ReadAsync_Accepts1024ByteUri()
    {
        string prefix = "gemini://localhost/";

        string uri =
            prefix +
            new string(
                'a',
                1024 - Encoding.UTF8.GetByteCount(prefix));

        Assert.Equal(
            1024,
            Encoding.UTF8.GetByteCount(uri));

        await using MemoryStream stream =
            CreateRequestStream(uri);

        var request =
            await GeminiRequestReader.ReadAsync(
                stream,
                CancellationToken.None);

        Assert.NotNull(request);
        Assert.Equal(uri, request.Url.AbsoluteUri);
    }

    [Fact]
    public async Task ReadAsync_Rejects1025ByteUri()
    {
        string prefix = "gemini://localhost/";

        string uri =
            prefix +
            new string(
                'a',
                1025 - Encoding.UTF8.GetByteCount(prefix));

        Assert.Equal(
            1025,
            Encoding.UTF8.GetByteCount(uri));

        await using MemoryStream stream =
            CreateRequestStream(uri);

        var request =
            await GeminiRequestReader.ReadAsync(
                stream,
                CancellationToken.None);

        Assert.Null(request);
    }

    private static MemoryStream CreateRequestStream(
        string uri)
    {
        byte[] bytes =
            Encoding.UTF8.GetBytes(
                uri + "\r\n");

        return new MemoryStream(bytes);
    }
}