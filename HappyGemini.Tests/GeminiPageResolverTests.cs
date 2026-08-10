using HappyGemini.Extensibility;
using HappyGemini.Pages;
using Microsoft.Extensions.Options;

namespace HappyGemini.Tests;

public sealed class GeminiPageResolverTests
{
    [Fact]
    public void Resolve_ReturnsGlobalPage_WhenGlobalPagesEnabled()
    {
        TestPage globalPage = new("/test");

        GeminiPageResolver resolver = new([globalPage]);

        GeminiVirtualHost host = CreateHost("one.example", useGlobalPages: true);

        IGeminiPage? resolved = resolver.Resolve(host, "/test");

        Assert.Same(globalPage, resolved);
    }

    [Fact]
    public void Resolve_DoesNotReturnGlobalPage_WhenGlobalPagesDisabled()
    {
        TestPage globalPage = new("/test");

        GeminiPageResolver resolver = new([globalPage]);

        GeminiVirtualHost host = CreateHost("one.example", useGlobalPages: false);

        IGeminiPage? resolved = resolver.Resolve(host, "/test");

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_HostScopedPageOverridesGlobalPage()
    {
        TestPage globalPage = new("/test");

        HostScopedTestPage hostPage = new("/test", ["one.example"]);

        GeminiPageResolver resolver = new([globalPage, hostPage]);

        GeminiVirtualHost host = CreateHost("one.example");

        IGeminiPage? resolved = resolver.Resolve(host, "/test");

        Assert.Same(hostPage, resolved);
    }

    [Fact]
    public void Resolve_HostScopedPageDoesNotApplyToOtherHost()
    {
        HostScopedTestPage hostPage = new("/test", ["one.example"]);

        GeminiPageResolver resolver = new([hostPage]);

        GeminiVirtualHost host = CreateHost("two.example");

        IGeminiPage? resolved = resolver.Resolve(host, "/test");

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_HostScopedPageWorksWhenGlobalPagesDisabled()
    {
        HostScopedTestPage hostPage = new("/test", ["one.example"]);

        GeminiPageResolver resolver = new([hostPage]);

        GeminiVirtualHost host = CreateHost("one.example", useGlobalPages: false);

        IGeminiPage? resolved = resolver.Resolve(host, "/test");

        Assert.Same(hostPage, resolved);
    }

    [Fact]
    public void Resolve_NormalizesHostScopedHostname()
    {
        HostScopedTestPage hostPage = new("/test", ["ONE.EXAMPLE."]);

        GeminiPageResolver resolver = new([hostPage]);

        GeminiVirtualHost host = CreateHost("one.example");

        IGeminiPage? resolved = resolver.Resolve(host, "/test");

        Assert.Same(hostPage, resolved);
    }

    [Fact]
    public void Constructor_RejectsDuplicateGlobalPaths()
    {
        TestPage first = new("/test");

        TestPage second = new("/test");

        Assert.Throws<InvalidOperationException>(() => new GeminiPageResolver([first, second]));
    }

    [Fact]
    public void Constructor_RejectsDuplicateHostScopedPaths()
    {
        HostScopedTestPage first = new("/test", ["one.example"]);

        HostScopedTestPage second = new("/test", ["one.example"]);

        Assert.Throws<InvalidOperationException>(() => new GeminiPageResolver([first, second]));
    }

    [Fact]
    public void Constructor_AllowsSamePathOnDifferentHosts()
    {
        HostScopedTestPage first = new("/test", ["one.example"]);

        HostScopedTestPage second = new("/test", ["two.example"]);

        GeminiPageResolver resolver = new([first, second]);

        GeminiVirtualHost firstHost = CreateHost("one.example");

        GeminiVirtualHost secondHost = CreateHost("two.example");

        Assert.Same(first, resolver.Resolve(firstHost, "/test"));

        Assert.Same(second, resolver.Resolve(secondHost, "/test"));
    }

    [Fact]
    public void Constructor_RejectsHostScopedPageWithoutHosts()
    {
        HostScopedTestPage page = new("/test", []);

        Assert.Throws<InvalidOperationException>(() => new GeminiPageResolver([page]));
    }

    [Fact]
    public void Constructor_RejectsDuplicateHostsOnPage()
    {
        HostScopedTestPage page = new("/test", ["one.example", "ONE.EXAMPLE."]);

        Assert.Throws<InvalidOperationException>(() => new GeminiPageResolver([page]));
    }

    [Fact]
    public void Constructor_RejectsUnicodeAndPunycodeDuplicateHostsOnPage()
    {
        HostScopedTestPage page = new(
            "/test",
            ["bücher.example", "xn--bcher-kva.example"]
        );

        Assert.Throws<InvalidOperationException>(() => new GeminiPageResolver([page]));
    }

    [Fact]
    public void Constructor_RejectsInvalidHostOnPage()
    {
        HostScopedTestPage page = new("/test", ["invalid host"]);

        Assert.Throws<InvalidOperationException>(() => new GeminiPageResolver([page]));
    }

    private static GeminiVirtualHost CreateHost(string hostname, bool useGlobalPages = true)
    {
        GeminiServerOptions serverOptions = new() { Hostnames = [hostname] };

        GeminiContentOptions contentOptions = new()
        {
            Hosts = new Dictionary<string, GeminiHostContentOptions>
            {
                [hostname] = new() { UseGlobalPages = useGlobalPages },
            },
        };

        GeminiVirtualHostResolver hostResolver = new(
            Options.Create(serverOptions),
            Options.Create(contentOptions)
        );

        return hostResolver.Resolve(new Uri($"gemini://{hostname}/"))!;
    }

    private sealed class TestPage(string path) : IGeminiPage
    {
        public string Path { get; } = path;

        public Task WriteAsync(
            GeminiRequest request,
            GeminiResponseWriter response,
            CancellationToken cancellationToken
        )
        {
            return Task.CompletedTask;
        }
    }

    private sealed class HostScopedTestPage(string path, IReadOnlyCollection<string> hostnames)
        : IHostScopedGeminiPage
    {
        public string Path { get; } = path;

        public IReadOnlyCollection<string> Hostnames { get; } = hostnames;

        public Task WriteAsync(
            GeminiRequest request,
            GeminiResponseWriter response,
            CancellationToken cancellationToken
        )
        {
            return Task.CompletedTask;
        }
    }
}
