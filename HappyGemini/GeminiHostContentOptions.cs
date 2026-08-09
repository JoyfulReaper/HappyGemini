namespace HappyGemini;

public sealed class GeminiHostContentOptions
{
    public string ContentDirectory { get; init; } =
        string.Empty;

    public string? IndexFile { get; init; }
}