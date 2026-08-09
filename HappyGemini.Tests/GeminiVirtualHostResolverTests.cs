using Microsoft.Extensions.Options;

namespace HappyGemini.Tests;

public sealed class GeminiVirtualHostResolverTests
{
    [Fact]
    public void Resolve_UsesGlobalContentConfigurationByDefault()
    {
        string contentDirectory = Path.Combine(
            Path.GetTempPath(),
            "HappyGemini.Tests",
            "global-content"
        );

        GeminiVirtualHostResolver resolver = CreateResolver(
            new GeminiServerOptions { Hostnames = ["one.example"] },
            new GeminiContentOptions
            {
                ContentDirectory = contentDirectory,
                IndexFile = "home.gmi",
            }
        );

        GeminiVirtualHost? host = resolver.Resolve(new Uri("gemini://one.example/"));

        Assert.NotNull(host);
        Assert.Equal("one.example", host.Hostname);
        Assert.Equal(Path.GetFullPath(contentDirectory), host.ContentRoot);
        Assert.Equal("home.gmi", host.IndexFile);
        Assert.True(host.UseGlobalPages);
    }

    [Fact]
    public void Resolve_AppliesNormalizedPerHostConfiguration()
    {
        string globalContentDirectory = Path.Combine(
            Path.GetTempPath(),
            "HappyGemini.Tests",
            "global-content"
        );

        string hostContentDirectory = Path.Combine(
            Path.GetTempPath(),
            "HappyGemini.Tests",
            "host-content"
        );

        GeminiVirtualHostResolver resolver = CreateResolver(
            new GeminiServerOptions { Hostnames = [" ONE.EXAMPLE. "] },
            new GeminiContentOptions
            {
                ContentDirectory = globalContentDirectory,
                IndexFile = "index.gmi",
                Hosts = new Dictionary<string, GeminiHostContentOptions>
                {
                    ["one.example"] = new()
                    {
                        ContentDirectory = hostContentDirectory,
                        IndexFile = "host-index.gmi",
                        UseGlobalPages = false,
                    },
                },
            }
        );

        GeminiVirtualHost? host = resolver.Resolve(new Uri("gemini://ONE.EXAMPLE./"));

        Assert.NotNull(host);
        Assert.Equal("ONE.EXAMPLE", host.Hostname);
        Assert.Equal(Path.GetFullPath(hostContentDirectory), host.ContentRoot);
        Assert.Equal("host-index.gmi", host.IndexFile);
        Assert.False(host.UseGlobalPages);
    }

    [Fact]
    public void Resolve_FallsBackForBlankPerHostContentValues()
    {
        string contentDirectory = Path.Combine(
            Path.GetTempPath(),
            "HappyGemini.Tests",
            "global-content"
        );

        GeminiVirtualHostResolver resolver = CreateResolver(
            new GeminiServerOptions { Hostnames = ["one.example"] },
            new GeminiContentOptions
            {
                ContentDirectory = contentDirectory,
                IndexFile = "index.gmi",
                Hosts = new Dictionary<string, GeminiHostContentOptions>
                {
                    ["one.example"] = new()
                    {
                        ContentDirectory = " ",
                        IndexFile = " ",
                        UseGlobalPages = false,
                    },
                },
            }
        );

        GeminiVirtualHost? host = resolver.Resolve(new Uri("gemini://one.example/"));

        Assert.NotNull(host);
        Assert.Equal(Path.GetFullPath(contentDirectory), host.ContentRoot);
        Assert.Equal("index.gmi", host.IndexFile);
        Assert.False(host.UseGlobalPages);
    }

    [Fact]
    public void Resolve_ReturnsNullForUnservedHostname()
    {
        GeminiVirtualHostResolver resolver = CreateResolver(
            new GeminiServerOptions { Hostnames = ["one.example"] },
            new GeminiContentOptions()
        );

        GeminiVirtualHost? host = resolver.Resolve(new Uri("gemini://two.example/"));

        Assert.Null(host);
    }

    [Fact]
    public void Constructor_RejectsHostnamesDuplicatedAfterNormalization()
    {
        GeminiServerOptions serverOptions = new() { Hostnames = ["one.example", " ONE.EXAMPLE. "] };

        Assert.Throws<InvalidOperationException>(() =>
            CreateResolver(serverOptions, new GeminiContentOptions())
        );
    }

    private static GeminiVirtualHostResolver CreateResolver(
        GeminiServerOptions serverOptions,
        GeminiContentOptions contentOptions
    )
    {
        return new GeminiVirtualHostResolver(
            Options.Create(serverOptions),
            Options.Create(contentOptions)
        );
    }
}
