using System.Reflection;

namespace HappyGemini.Plugins;

/// <summary>
/// Represents a discovered plugin whose entry assembly has been loaded.
/// </summary>
public sealed record GeminiLoadedPlugin(
    GeminiPluginDescriptor Descriptor,
    Assembly EntryAssembly);