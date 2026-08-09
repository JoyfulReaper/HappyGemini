using HappyGemini.Server;
using System.Text;

namespace HappyGemini.Tests;

public sealed class GeminiRequestReaderTests
{
    [Fact]
    public async Task ReadAsync_AcceptsValidGeminiUri()
    {
        const string uri = "gemini://example.com/path?query=value";

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
    public async Task ReadAsync_MatchesSchemeCaseInsensitively()
    {
        const string uri = "GEMINI://example.com/path";

        await using MemoryStream stream =
            CreateRequestStream(uri);

        var request =
            await GeminiRequestReader.ReadAsync(
                stream,
                CancellationToken.None);

        Assert.NotNull(request);
        Assert.Equal("gemini", request.Url.Scheme);
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("relative/path")]
    [InlineData("gemini:///path")]
    [InlineData("gemini://user@example.com/")]
    [InlineData("gemini://example.com/#fragment")]
    [InlineData("")]
    public async Task ReadAsync_RejectsInvalidRequestUri(
        string uri)
    {
        await using MemoryStream stream =
            CreateRequestStream(uri);

        var request =
            await GeminiRequestReader.ReadAsync(
                stream,
                CancellationToken.None);

        Assert.Null(request);
    }

    [Theory]
    [InlineData("gemini://example.com/")]
    [InlineData("gemini://example.com/\n")]
    [InlineData("gemini://exam")]
    [InlineData("")]
    public async Task ReadAsync_RejectsIncompleteRequest(
        string requestBytes)
    {
        await using MemoryStream stream =
            CreateStream(requestBytes);

        var request =
            await GeminiRequestReader.ReadAsync(
                stream,
                CancellationToken.None);

        Assert.Null(request);
    }

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

    [Fact]
    public async Task ReadAsync_UsesUtf8ByteCountForLimit()
    {
        string prefix = "gemini://localhost/";

        string uri =
            prefix +
            new string('é', 503);

        Assert.True(uri.Length < 1024);
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

    [Fact]
    public async Task ReadAsync_HandlesCrLfSplitAcrossReads()
    {
        const string uri = "gemini://example.com/path";

        await using ChunkedReadStream stream =
            new(
                Encoding.UTF8.GetBytes(uri + "\r"),
                Encoding.UTF8.GetBytes("\n"));

        var request =
            await GeminiRequestReader.ReadAsync(
                stream,
                CancellationToken.None);

        Assert.NotNull(request);
        Assert.Equal(uri, request.Url.AbsoluteUri);
    }

    private static MemoryStream CreateRequestStream(
        string uri)
    {
        return CreateStream(uri + "\r\n");
    }

    private static MemoryStream CreateStream(
        string contents)
    {
        return new MemoryStream(
            Encoding.UTF8.GetBytes(contents));
    }

    private sealed class ChunkedReadStream(
        params byte[][] chunks) : Stream
    {
        private int chunkIndex;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length =>
            throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count)
        {
            return Read(
                buffer.AsSpan(offset, count));
        }

        public override int Read(
            Span<byte> buffer)
        {
            if (chunkIndex == chunks.Length)
            {
                return 0;
            }

            byte[] chunk = chunks[chunkIndex++];
            chunk.CopyTo(buffer);
            return chunk.Length;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span));
        }

        public override void Flush()
        {
        }

        public override long Seek(
            long offset,
            SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(
            long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(
            byte[] buffer,
            int offset,
            int count)
        {
            throw new NotSupportedException();
        }
    }
}
