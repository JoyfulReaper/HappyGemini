using System.Reflection;

namespace HappyGemini.Plugins;

/// <summary>
/// Loads discovered HappyGemini plugins into isolated assembly load contexts.
/// </summary>
public sealed class GeminiPluginLoader
{
    public GeminiLoadedPlugin Load(
        GeminiPluginDescriptor plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        try
        {
            HappyGeminiPluginLoadContext loadContext =
                new(plugin);

            Assembly entryAssembly =
                loadContext.LoadFromAssemblyPath(
                    plugin.EntryAssemblyPath);

            return new GeminiLoadedPlugin(
                plugin,
                entryAssembly);
        }
        catch (Exception exception)
            when (exception is FileNotFoundException
                or FileLoadException
                or BadImageFormatException
                or InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Failed to load Gemini plugin '{plugin.Id}' " +
                $"from '{plugin.EntryAssemblyPath}'.",
                exception);
        }
    }
}