using HappyGemini.Extensibility;

namespace HappyGemini.Pages;

/// <summary>
/// Resolves registered dynamic Gemini pages by path.
/// </summary>
public sealed class GeminiPageResolver
{
    private readonly Dictionary<string, IGeminiPage> _pages =
        new(StringComparer.Ordinal);

    public GeminiPageResolver(IEnumerable<IGeminiPage> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        foreach (IGeminiPage page in pages)
        {
            ArgumentNullException.ThrowIfNull(page);

            string path = NormalizePath(page.Path);

            if (!_pages.TryAdd(path, page))
            {
                throw new InvalidOperationException(
                    $"Multiple Gemini pages are registered for path '{path}'.");
            }
        }
    }

    public IGeminiPage? Resolve(string path)
    {
        string normalizedPath = NormalizePath(path);

        return _pages.TryGetValue(
            normalizedPath,
            out IGeminiPage? page)
            ? page
            : null;
    }

    private static string NormalizePath(string path)
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
}