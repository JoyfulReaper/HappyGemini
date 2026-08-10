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
        Assert.Equal("one.example", host.Hostname);
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

    [Theory]
    [InlineData("example.com", "example.com")]
    [InlineData("Example.COM", "example.com")]
    [InlineData("example.com.", "example.com")]
    [InlineData("example.com。", "example.com")]
    [InlineData("bücher.example", "xn--bcher-kva.example")]
    [InlineData("xn--bcher-kva.example", "xn--bcher-kva.example")]
    public void Resolve_CanonicalizesConfiguredDnsHostname(
        string configuredHostname,
        string canonicalHostname
    )
    {
        GeminiVirtualHostResolver resolver = CreateResolver(
            new GeminiServerOptions { Hostnames = [configuredHostname] },
            new GeminiContentOptions()
        );

        GeminiVirtualHost? host = resolver.Resolve(
            new Uri($"gemini://{canonicalHostname}/")
        );

        Assert.NotNull(host);
        Assert.Equal(canonicalHostname, host.Hostname);
    }

    [Fact]
    public void Resolve_CanonicalizesUnicodeHostnameToPunycode()
    {
        GeminiVirtualHostResolver resolver = CreateResolver(
            new GeminiServerOptions { Hostnames = ["bücher.example"] },
            new GeminiContentOptions
            {
                Hosts = new Dictionary<string, GeminiHostContentOptions>
                {
                    ["xn--bcher-kva.example"] = new() { UseGlobalPages = false },
                },
            }
        );

        GeminiVirtualHost? host = resolver.Resolve(
            new Uri("gemini://xn--bcher-kva.example/")
        );

        Assert.NotNull(host);
        Assert.Equal("xn--bcher-kva.example", host.Hostname);
        Assert.False(host.UseGlobalPages);
    }

    [Fact]
    public void Resolve_CanonicalizesEquivalentIpv6Addresses()
    {
        GeminiVirtualHostResolver resolver = CreateResolver(
            new GeminiServerOptions { Hostnames = ["0:0:0:0:0:0:0:1"] },
            new GeminiContentOptions()
        );

        GeminiVirtualHost? host = resolver.Resolve(new Uri("gemini://[::1]/"));

        Assert.NotNull(host);
        Assert.Equal("::1", host.Hostname);
    }

    [Theory]
    [InlineData("[127.0.0.1]", "gemini://127.0.0.1/", "127.0.0.1")]
    [InlineData("[0:0:0:0:0:0:0:1]", "gemini://[::1]/", "::1")]
    public void Resolve_PreservesBracketedIpConfiguration(
        string configuredHostname,
        string requestUri,
        string canonicalHostname
    )
    {
        GeminiVirtualHostResolver resolver = CreateResolver(
            new GeminiServerOptions { Hostnames = [configuredHostname] },
            new GeminiContentOptions()
        );

        GeminiVirtualHost? host = resolver.Resolve(new Uri(requestUri));

        Assert.NotNull(host);
        Assert.Equal(canonicalHostname, host.Hostname);
    }

    [Fact]
    public void Constructor_RejectsInvalidConfiguredHostname()
    {
        GeminiServerOptions serverOptions = new() { Hostnames = ["invalid host"] };

        Assert.Throws<ArgumentException>(() =>
            CreateResolver(serverOptions, new GeminiContentOptions())
        );
    }

    [Fact]
    public void Constructor_RejectsUnicodeAndPunycodeDuplicateHostnames()
    {
        GeminiServerOptions serverOptions = new()
        {
            Hostnames = ["bücher.example", "xn--bcher-kva.example"],
        };

        Assert.Throws<InvalidOperationException>(() =>
            CreateResolver(serverOptions, new GeminiContentOptions())
        );
    }

    [Fact]
    public void Constructor_RejectsUnicodeRootDotAndAsciiRootDotDuplicateHostnames()
    {
        GeminiServerOptions serverOptions = new()
        {
            Hostnames = ["example.com。", "example.com."],
        };

        Assert.Throws<InvalidOperationException>(() =>
            CreateResolver(serverOptions, new GeminiContentOptions())
        );
    }

    [Theory]
    [InlineData("example.com..")]
    [InlineData("example.com。。")]
    [InlineData("xn--a.example")]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("。")]
    public void Constructor_RejectsInvalidDnsHostname(string hostname)
    {
        GeminiServerOptions serverOptions = new() { Hostnames = [hostname] };

        Assert.Throws<ArgumentException>(() =>
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
