using Microsoft.Extensions.Options;

namespace HappyGemini;

/// <summary>
/// Resolves Gemini request paths to files beneath the configured
/// static-content directory.
/// </summary>
public sealed class GeminiContentStore
{
    private readonly string _contentRoot;
    private readonly string _contentRootPrefix;
    private readonly string _indexFile;
    private readonly StringComparison _pathComparison;

    public GeminiContentStore(
        IOptions<GeminiContentOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        GeminiContentOptions value = options.Value;

        _contentRoot = ResolveContentRoot(
            value.ContentDirectory);

        _contentRootPrefix =
            _contentRoot.EndsWith(
                Path.DirectorySeparatorChar)
                ? _contentRoot
                : _contentRoot +
                    Path.DirectorySeparatorChar;

        _indexFile = value.IndexFile;

        _pathComparison =
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
    }

    /// <summary>
    /// Resolves a Gemini URL path to an existing static file.
    /// </summary>
    public bool TryResolve(
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

        // A URL path uses '/', never the platform-specific
        // directory separator. Reject backslashes explicitly so
        // Windows cannot interpret them as path traversal.
        if (decodedPath.Contains('\\'))
        {
            return false;
        }

        string relativePath =
            decodedPath.TrimStart('/');

        if (relativePath.Length == 0)
        {
            relativePath = _indexFile;
        }
        else if (decodedPath.EndsWith(
                     '/',
                     StringComparison.Ordinal))
        {
            relativePath =
                Path.Combine(
                    relativePath,
                    _indexFile);
        }

        relativePath =
            relativePath.Replace(
                '/',
                Path.DirectorySeparatorChar);

        string candidatePath =
            Path.GetFullPath(
                relativePath,
                _contentRoot);

        if (!IsInsideContentRoot(candidatePath))
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

    private bool IsInsideContentRoot(
        string candidatePath)
    {
        return candidatePath.StartsWith(
            _contentRootPrefix,
            _pathComparison);
    }

    private static string ResolveContentRoot(
        string contentDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            contentDirectory);

        if (Path.IsPathRooted(contentDirectory))
        {
            return Path.GetFullPath(
                contentDirectory);
        }

        return Path.GetFullPath(
            contentDirectory,
            AppContext.BaseDirectory);
    }
}