namespace HappyGemini.Plugins;

public sealed record GeminiPluginDescriptor(
    string Id,
    string DirectoryPath,
    string EntryAssemblyPath);