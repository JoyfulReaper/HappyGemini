namespace HappyGemini.Extensibility;

/// <summary>
/// Represents a dynamic Gemini resource registered for an exact path.
/// </summary>
public interface IGeminiPage
{
    /// <summary>
    /// Gets the exact path handled by this page.
    /// </summary>
    string Path { get; }

    /// <summary>
    /// Writes the Gemini response.
    /// </summary>
    Task WriteAsync(
        GeminiRequest request,
        GeminiResponseWriter response,
        CancellationToken cancellationToken);
}