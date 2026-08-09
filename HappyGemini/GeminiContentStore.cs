namespace HappyGemini;

/// <summary>
/// Resolves Gemini request paths to static files
/// beneath a virtual host's content root.
/// </summary>
public sealed class GeminiContentStore
{
    private readonly StringComparison _pathComparison =
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    public bool TryResolve(
        GeminiVirtualHost virtualHost,
        string requestPath,
        out string? filePath)
    {
        ArgumentNullException.ThrowIfNull(
            virtualHost);

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
                Uri.UnescapeDataString(
                    requestPath);
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
                virtualHost.IndexFile;
        }
        else if (decodedPath.EndsWith('/'))
        {
            relativePath =
                Path.Combine(
                    relativePath,
                    virtualHost.IndexFile);
        }

        relativePath =
            relativePath.Replace(
                '/',
                Path.DirectorySeparatorChar);

        string candidatePath =
            Path.GetFullPath(
                relativePath,
                virtualHost.ContentRoot);

        if (!candidatePath.StartsWith(
                virtualHost.ContentRootPrefix,
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
}