namespace HappyGemini;

public sealed class GeminiHostContentOptions
{
    public string? ContentDirectory { get; init; }

    public string? IndexFile { get; init; }

    public bool UseGlobalPages { get; init; } =
        true;
}