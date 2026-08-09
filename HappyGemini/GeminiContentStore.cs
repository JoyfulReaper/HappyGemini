namespace HappyGemini;

/// <summary>
/// Resolves Gemini request paths to static files beneath a virtual host's content root.
/// </summary>
public sealed class GeminiContentStore
{
    private readonly StringComparison _pathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public bool TryResolve(GeminiVirtualHost virtualHost, string requestPath, out string? filePath)
    {
        ArgumentNullException.ThrowIfNull(virtualHost);

        filePath = null;

        if (string.IsNullOrWhiteSpace(requestPath) || requestPath[0] != '/')
        {
            return false;
        }

        if (ContainsEncodedPathSeparator(requestPath))
        {
            return false;
        }

        string decodedPath;

        try
        {
            decodedPath = Uri.UnescapeDataString(requestPath);
        }
        catch (UriFormatException)
        {
            return false;
        }

        if (decodedPath.Contains('\\'))
        {
            return false;
        }

        string relativePath = decodedPath.TrimStart('/');

        if (OperatingSystem.IsWindows() && relativePath.Contains(':'))
        {
            return false;
        }

        if (relativePath.Length == 0)
        {
            relativePath = virtualHost.IndexFile;
        }
        else if (decodedPath.EndsWith('/'))
        {
            relativePath = Path.Combine(relativePath, virtualHost.IndexFile);
        }

        relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);

        string candidatePath;

        try
        {
            candidatePath = Path.GetFullPath(relativePath, virtualHost.ContentRoot);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }

        if (!candidatePath.StartsWith(virtualHost.ContentRootPrefix, _pathComparison))
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

    private static bool ContainsEncodedPathSeparator(string path)
    {
        for (int i = 0; i + 2 < path.Length; i++)
        {
            if (path[i] != '%')
            {
                continue;
            }

            if (
                path[i + 1] == '2' && path[i + 2] is 'F' or 'f'
                || path[i + 1] == '5' && path[i + 2] is 'C' or 'c'
            )
            {
                return true;
            }
        }

        return false;
    }
}
