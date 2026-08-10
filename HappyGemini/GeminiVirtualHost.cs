namespace HappyGemini;

/// <summary>
/// Represents a Gemini virtual host served by the server.
/// </summary>
public sealed class GeminiVirtualHost
{
    internal GeminiVirtualHost(
        string hostname,
        string contentRoot,
        string indexFile,
        bool useGlobalPages
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);

        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);

        ArgumentException.ThrowIfNullOrWhiteSpace(indexFile);

        Hostname = hostname;
        ContentRoot = contentRoot;
        IndexFile = indexFile;
        UseGlobalPages = useGlobalPages;

        ContentRootPrefix = contentRoot.EndsWith(Path.DirectorySeparatorChar)
            ? contentRoot
            : contentRoot + Path.DirectorySeparatorChar;
    }

    public string Hostname { get; }

    public string ContentRoot { get; }

    public string IndexFile { get; }

    public bool UseGlobalPages { get; }

    internal string ContentRootPrefix { get; }
}
