using Microsoft.Extensions.Options;

namespace HappyGemini;

/// <summary>
/// Resolves request hostnames to configured Gemini virtual hosts.
/// </summary>
public sealed class GeminiVirtualHostResolver
{
    private readonly Dictionary<string, GeminiVirtualHost>
        _hosts;

    public GeminiVirtualHostResolver(
        IOptions<GeminiServerOptions> serverOptions,
        IOptions<GeminiContentOptions> contentOptions)
    {
        ArgumentNullException.ThrowIfNull(
            serverOptions);

        ArgumentNullException.ThrowIfNull(
            contentOptions);

        GeminiServerOptions server =
            serverOptions.Value;

        GeminiContentOptions content =
            contentOptions.Value;

        Dictionary<string, GeminiHostContentOptions>
            hostContent =
                content.Hosts.ToDictionary(
                    entry =>
                        NormalizeHostname(
                            entry.Key),
                    entry => entry.Value,
                    StringComparer.OrdinalIgnoreCase);

        _hosts =
            new Dictionary<string, GeminiVirtualHost>(
                StringComparer.OrdinalIgnoreCase);

        foreach (string hostname
            in server.Hostnames)
        {
            string normalizedHostname =
                NormalizeHostname(hostname);

            hostContent.TryGetValue(
                normalizedHostname,
                out GeminiHostContentOptions?
                    hostOptions);

            string contentDirectory =
                hostOptions?.ContentDirectory ??
                content.ContentDirectory;

            string indexFile =
                string.IsNullOrWhiteSpace(
                    hostOptions?.IndexFile)
                    ? content.IndexFile
                    : hostOptions.IndexFile;

            string contentRoot =
                ResolveContentRoot(
                    contentDirectory);

            GeminiVirtualHost host =
                new(
                    normalizedHostname,
                    contentRoot,
                    indexFile);

            if (!_hosts.TryAdd(
                    normalizedHostname,
                    host))
            {
                throw new InvalidOperationException(
                    $"Gemini virtual host '{hostname}' is configured more than once.");
            }
        }
    }

    public GeminiVirtualHost? Resolve(
        Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        _hosts.TryGetValue(
            NormalizeHostname(url.IdnHost),
            out GeminiVirtualHost? host);

        return host;
    }

    private static string ResolveContentRoot(
        string contentDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            contentDirectory);

        if (Path.IsPathRooted(
                contentDirectory))
        {
            return Path.GetFullPath(
                contentDirectory);
        }

        return Path.GetFullPath(
            contentDirectory,
            AppContext.BaseDirectory);
    }

    private static string NormalizeHostname(
        string hostname)
    {
        return hostname
            .Trim()
            .TrimEnd('.');
    }
}