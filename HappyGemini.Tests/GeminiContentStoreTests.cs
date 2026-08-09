using Microsoft.Extensions.Options;

namespace HappyGemini.Tests;

public sealed class GeminiContentStoreTests :
    IDisposable
{
    private readonly string _contentRoot;
    private readonly string _outsideRoot;

    public GeminiContentStoreTests()
    {
        string testRoot =
            Path.Combine(
                Path.GetTempPath(),
                "HappyGemini.Tests",
                Guid.NewGuid().ToString("N"));

        _contentRoot =
            Path.Combine(
                testRoot,
                "content");

        _outsideRoot =
            Path.Combine(
                testRoot,
                "outside");

        Directory.CreateDirectory(
            _contentRoot);

        Directory.CreateDirectory(
            _outsideRoot);
    }

    [Fact]
    public void TryResolve_ResolvesFile()
    {
        string expectedPath =
            Path.Combine(
                _contentRoot,
                "hello.gmi");

        File.WriteAllText(
            expectedPath,
            "# Hello");

        GeminiContentStore store = new();
        GeminiVirtualHost host =
            CreateVirtualHost();

        bool resolved =
            store.TryResolve(
                host,
                "/hello.gmi",
                out string? filePath);

        Assert.True(resolved);
        Assert.Equal(
            expectedPath,
            filePath);
    }

    [Fact]
    public void TryResolve_ResolvesRootIndex()
    {
        string expectedPath =
            Path.Combine(
                _contentRoot,
                "index.gmi");

        File.WriteAllText(
            expectedPath,
            "# Index");

        GeminiContentStore store = new();
        GeminiVirtualHost host =
            CreateVirtualHost();

        bool resolved =
            store.TryResolve(
                host,
                "/",
                out string? filePath);

        Assert.True(resolved);
        Assert.Equal(
            expectedPath,
            filePath);
    }

    [Fact]
    public void TryResolve_ResolvesDirectoryIndex()
    {
        string directory =
            Path.Combine(
                _contentRoot,
                "docs");

        Directory.CreateDirectory(
            directory);

        string expectedPath =
            Path.Combine(
                directory,
                "index.gmi");

        File.WriteAllText(
            expectedPath,
            "# Docs");

        GeminiContentStore store = new();
        GeminiVirtualHost host =
            CreateVirtualHost();

        bool resolved =
            store.TryResolve(
                host,
                "/docs/",
                out string? filePath);

        Assert.True(resolved);
        Assert.Equal(
            expectedPath,
            filePath);
    }

    [Fact]
    public void TryResolve_RejectsTraversal()
    {
        string outsidePath =
            Path.Combine(
                _outsideRoot,
                "secret.gmi");

        File.WriteAllText(
            outsidePath,
            "secret");

        GeminiContentStore store = new();
        GeminiVirtualHost host =
            CreateVirtualHost();

        bool resolved =
            store.TryResolve(
                host,
                "/../outside/secret.gmi",
                out string? filePath);

        Assert.False(resolved);
        Assert.Null(filePath);
    }

    [Fact]
    public void TryResolve_RejectsEncodedTraversal()
    {
        string outsidePath =
            Path.Combine(
                _outsideRoot,
                "secret.gmi");

        File.WriteAllText(
            outsidePath,
            "secret");

        GeminiContentStore store = new();
        GeminiVirtualHost host =
            CreateVirtualHost();

        bool resolved =
            store.TryResolve(
                host,
                "/%2e%2e/outside/secret.gmi",
                out string? filePath);

        Assert.False(resolved);
        Assert.Null(filePath);
    }

    [Fact]
    public void TryResolve_RejectsTraversalIntoSiblingWithSharedPrefix()
    {
        string siblingRoot =
            _contentRoot + "-private";

        Directory.CreateDirectory(
            siblingRoot);

        string outsidePath =
            Path.Combine(
                siblingRoot,
                "secret.gmi");

        File.WriteAllText(
            outsidePath,
            "secret");

        GeminiContentStore store = new();
        GeminiVirtualHost host =
            CreateVirtualHost();

        bool resolved =
            store.TryResolve(
                host,
                "/../content-private/secret.gmi",
                out string? filePath);

        Assert.False(resolved);
        Assert.Null(filePath);
    }

    [Theory]
    [InlineData("/..\\outside\\secret.gmi")]
    [InlineData("/..%5coutside%5csecret.gmi")]
    public void TryResolve_RejectsBackslashes(
        string requestPath)
    {
        GeminiContentStore store = new();
        GeminiVirtualHost host =
            CreateVirtualHost();

        bool resolved =
            store.TryResolve(
                host,
                requestPath,
                out string? filePath);

        Assert.False(resolved);
        Assert.Null(filePath);
    }

    [Fact]
    public void TryResolve_ReturnsFalseForMissingFile()
    {
        GeminiContentStore store = new();
        GeminiVirtualHost host =
            CreateVirtualHost();

        bool resolved =
            store.TryResolve(
                host,
                "/missing.gmi",
                out string? filePath);

        Assert.False(resolved);
        Assert.Null(filePath);
    }

    [Fact]
    public void TryResolve_ReturnsFalseForMalformedPath()
    {
        GeminiContentStore store = new();
        GeminiVirtualHost host =
            CreateVirtualHost();

        bool resolved =
            store.TryResolve(
                host,
                "/bad\0path.gmi",
                out string? filePath);

        Assert.False(resolved);
        Assert.Null(filePath);
    }

    private GeminiVirtualHost CreateVirtualHost()
    {
        GeminiServerOptions serverOptions =
            new()
            {
                Hostnames =
                    ["test.example"]
            };

        GeminiContentOptions contentOptions =
            new()
            {
                ContentDirectory =
                    _contentRoot,

                IndexFile =
                    "index.gmi"
            };

        GeminiVirtualHostResolver resolver =
            new(
                Options.Create(
                    serverOptions),
                Options.Create(
                    contentOptions));

        return resolver.Resolve(
            new Uri(
                "gemini://test.example/"))!;
    }

    public void Dispose()
    {
        string? root =
            Directory.GetParent(
                _contentRoot)?
                .FullName;

        if (root is not null &&
            Directory.Exists(root))
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }
}
