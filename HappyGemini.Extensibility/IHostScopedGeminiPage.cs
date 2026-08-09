namespace HappyGemini.Extensibility;

/// <summary>
/// Represents a Gemini page that is available only
/// on specific virtual hosts.
/// </summary>
public interface IHostScopedGeminiPage :
    IGeminiPage
{
    /// <summary>
    /// Gets the hostnames on which this page is available.
    /// </summary>
    IReadOnlyCollection<string> Hostnames { get; }
}