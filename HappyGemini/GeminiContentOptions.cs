namespace HappyGemini;

public sealed class GeminiContentOptions
{
    public const string SectionName = "GeminiContent";

    public string ContentDirectory { get; init; } =
        "content";

    public string IndexFile { get; init; } =
        "index.gmi";

    public Dictionary<string, GeminiHostContentOptions> Hosts
    {
        get;
        init;
    } = [];
}