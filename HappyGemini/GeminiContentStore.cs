using Microsoft.Extensions.Options;

namespace HappyGemini;

/// <summary>
/// Resolves Gemini request paths to files beneath the configured
/// static-content directory for the requested host.
/// </summary>
public sealed class GeminiContentStore
{
    private readonly ContentRoot _defaultRoot;

    private readonly Dictionary<string, ContentRoot>
        _hostRoots;

    private readonly StringComparison _pathComparison;

    public GeminiContentStore(
        IOptions<GeminiContentOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        GeminiContentOptions value =
            options.Value;

        _pathComparison =
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        _defaultRoot =
            CreateContentRoot(
                value.ContentDirectory,
                value.IndexFile);

        _hostRoots =
            new Dictionary<string, ContentRoot>(
                StringComparer.OrdinalIgnoreCase);

        foreach ((
            string hostname,
            GeminiHostContentOptions hostOptions)
            in value.Hosts)
        {
            string normalizedHostname =
                NormalizeHostname(hostname);

            if (normalizedHostname.Length == 0)
            {
                throw new InvalidOperationException(
                    "Gemini content hostname must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(
                    hostOptions.ContentDirectory))
            {
                throw new InvalidOperationException(
                    $"Content directory for Gemini host '{hostname}' must not be empty.");
            }

            string indexFile =
                string.IsNullOrWhiteSpace(
                    hostOptions.IndexFile)
                    ? value.IndexFile
                    : hostOptions.IndexFile;

            ContentRoot contentRoot =
                CreateContentRoot(
                    hostOptions.ContentDirectory,
                    indexFile);

            if (!_hostRoots.TryAdd(
                    normalizedHostname,
                    contentRoot))
            {
                throw new InvalidOperationException(
                    $"Static content is already configured for Gemini host '{hostname}'.");
            }
        }
    }

    /// <summary>
    /// Resolves a Gemini URL path to an existing static file
    /// using the content root configured for the requested host.
    /// </summary>
    public bool TryResolve(
        string hostname,
        string requestPath,
        out string? filePath)
    {
        filePath = null;

        ArgumentException.ThrowIfNullOrWhiteSpace(
            hostname);

        ContentRoot contentRoot =
            _hostRoots.TryGetValue(
                NormalizeHostname(hostname),
                out ContentRoot? hostRoot)
                ? hostRoot
                : _defaultRoot;

        return TryResolve(
            contentRoot,
            requestPath,
            out filePath);
    }

    private bool TryResolve(
        ContentRoot contentRoot,
        string requestPath,
        out string? filePath)
    {
        filePath = null;

        if (string.IsNullOrWhiteSpace(requestPath) ||
            requestPath[0] != '/')
        {
            return false;
        }

        string decodedPath;

        try
        {
            decodedPath =
                Uri.UnescapeDataString(requestPath);
        }
        catch (UriFormatException)
        {
            return false;
        }

        if (decodedPath.Contains('\\'))
        {
            return false;
        }

        string relativePath =
            decodedPath.TrimStart('/');

        if (relativePath.Length == 0)
        {
            relativePath =
                contentRoot.IndexFile;
        }
        else if (decodedPath.EndsWith('/'))
        {
            relativePath =
                Path.Combine(
                    relativePath,
                    contentRoot.IndexFile);
        }

        relativePath =
            relativePath.Replace(
                '/',
                Path.DirectorySeparatorChar);

        string candidatePath =
            Path.GetFullPath(
                relativePath,
                contentRoot.Root);

        if (!candidatePath.StartsWith(
                contentRoot.RootPrefix,
                _pathComparison))
        {
            return false;
        }

        if (!File.Exists(candidatePath))
        {
            return false;
        }

        filePath = candidatePath;
        return true;
    }

    private static ContentRoot CreateContentRoot(
        string contentDirectory,
        string indexFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            contentDirectory);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            indexFile);

        string root =
            ResolveContentRoot(
                contentDirectory);

        string rootPrefix =
            root.EndsWith(
                Path.DirectorySeparatorChar)
                ? root
                : root +
                    Path.DirectorySeparatorChar;

        return new ContentRoot(
            root,
            rootPrefix,
            indexFile);
    }

    private static string ResolveContentRoot(
        string contentDirectory)
    {
        if (Path.IsPathRooted(contentDirectory))
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

    private sealed record ContentRoot(
        string Root,
        string RootPrefix,
        string IndexFile);
}