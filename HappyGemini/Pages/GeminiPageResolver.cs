using HappyGemini.Extensibility;

namespace HappyGemini.Pages;

/// <summary>
/// Resolves registered dynamic Gemini pages by
/// virtual host and path.
/// </summary>
public sealed class GeminiPageResolver
{
    private readonly Dictionary<string, IGeminiPage>
        _globalPages =
            new(StringComparer.Ordinal);

    private readonly Dictionary<
        string,
        Dictionary<string, IGeminiPage>>
        _hostPages =
            new(StringComparer.OrdinalIgnoreCase);

    public GeminiPageResolver(
        IEnumerable<IGeminiPage> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        foreach (IGeminiPage page in pages)
        {
            ArgumentNullException.ThrowIfNull(page);

            string path =
                NormalizePath(page.Path);

            if (page is IHostScopedGeminiPage
                hostScopedPage)
            {
                RegisterHostScopedPage(
                    hostScopedPage,
                    path);

                continue;
            }

            if (!_globalPages.TryAdd(
                    path,
                    page))
            {
                throw new InvalidOperationException(
                    $"Multiple global Gemini pages are registered for path '{path}'.");
            }
        }
    }

    public IGeminiPage? Resolve(
        GeminiVirtualHost virtualHost,
        string path)
    {
        ArgumentNullException.ThrowIfNull(
            virtualHost);

        string normalizedPath =
            NormalizePath(path);

        if (_hostPages.TryGetValue(
                virtualHost.Hostname,
                out Dictionary<string, IGeminiPage>?
                    hostPages) &&
            hostPages.TryGetValue(
                normalizedPath,
                out IGeminiPage? hostPage))
        {
            return hostPage;
        }

        return _globalPages.TryGetValue(
            normalizedPath,
            out IGeminiPage? globalPage)
            ? globalPage
            : null;
    }

    private void RegisterHostScopedPage(
        IHostScopedGeminiPage page,
        string path)
    {
        ArgumentNullException.ThrowIfNull(
            page.Hostnames);

        if (page.Hostnames.Count == 0)
        {
            throw new InvalidOperationException(
                $"Host-scoped Gemini page '{page.GetType().FullName}' does not declare any hostnames.");
        }

        HashSet<string> pageHosts =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (string hostname
            in page.Hostnames)
        {
            if (string.IsNullOrWhiteSpace(
                    hostname))
            {
                throw new InvalidOperationException(
                    $"Host-scoped Gemini page '{page.GetType().FullName}' contains an empty hostname.");
            }

            string normalizedHostname =
                NormalizeHostname(
                    hostname);

            if (!pageHosts.Add(
                    normalizedHostname))
            {
                throw new InvalidOperationException(
                    $"Host-scoped Gemini page '{page.GetType().FullName}' declares host '{hostname}' more than once.");
            }

            if (!_hostPages.TryGetValue(
                    normalizedHostname,
                    out Dictionary<string, IGeminiPage>?
                        hostPages))
            {
                hostPages =
                    new Dictionary<string, IGeminiPage>(
                        StringComparer.Ordinal);

                _hostPages.Add(
                    normalizedHostname,
                    hostPages);
            }

            if (!hostPages.TryAdd(
                    path,
                    page))
            {
                throw new InvalidOperationException(
                    $"Multiple Gemini pages are registered for host '{normalizedHostname}' and path '{path}'.");
            }
        }
    }

    private static string NormalizePath(
        string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path.Length == 0)
        {
            return "/";
        }

        return path[0] == '/'
            ? path
            : "/" + path;
    }

    private static string NormalizeHostname(
        string hostname)
    {
        return hostname
            .Trim()
            .TrimEnd('.');
    }
}