namespace HappyGemini.Plugins;

public sealed record GeminiPluginManifest
{
    public required string Id { get; init; }

    public required string EntryAssembly { get; init; }
}
