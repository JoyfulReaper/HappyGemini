using System.Text.Json;

namespace HappyGemini.Plugins;

/// <summary>
/// Discovers HappyGemini plugins from a plugin directory.
/// </summary>
public sealed class GeminiPluginDiscovery
{
    private const string ManifestFileName = "happygemini.plugin.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Discovers valid plugins from the immediate child directories of
    /// <paramref name="pluginDirectory"/>.
    /// </summary>
    public IReadOnlyList<GeminiPluginDescriptor> Discover(string pluginDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);

        string pluginRoot = ResolvePluginRoot(pluginDirectory);

        if (!Directory.Exists(pluginRoot))
        {
            return [];
        }

        List<GeminiPluginDescriptor> plugins = [];

        foreach (string directory in Directory.EnumerateDirectories(pluginRoot))
        {
            string manifestPath = Path.Combine(directory, ManifestFileName);

            if (!File.Exists(manifestPath))
            {
                continue;
            }

            GeminiPluginManifest manifest = ReadManifest(manifestPath);

            ValidateManifest(manifest, manifestPath);

            string pluginPath = Path.GetFullPath(directory);

            string entryAssemblyPath = Path.GetFullPath(manifest.EntryAssembly, pluginPath);

            EnsurePathIsInsidePlugin(pluginPath, entryAssemblyPath, manifest.Id);

            if (!File.Exists(entryAssemblyPath))
            {
                throw new FileNotFoundException(
                    $"Entry assembly for Gemini plugin '{manifest.Id}' "
                        + $"was not found: '{entryAssemblyPath}'.",
                    entryAssemblyPath
                );
            }

            plugins.Add(new GeminiPluginDescriptor(manifest.Id, pluginPath, entryAssemblyPath));
        }

        EnsureUniquePluginIds(plugins);

        return plugins.OrderBy(plugin => plugin.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string ResolvePluginRoot(string pluginDirectory)
    {
        if (Path.IsPathRooted(pluginDirectory))
        {
            return Path.GetFullPath(pluginDirectory);
        }

        return Path.GetFullPath(pluginDirectory, AppContext.BaseDirectory);
    }

    private static GeminiPluginManifest ReadManifest(string manifestPath)
    {
        try
        {
            string json = File.ReadAllText(manifestPath);

            return JsonSerializer.Deserialize<GeminiPluginManifest>(json, JsonOptions)
                ?? throw new InvalidDataException(
                    $"Gemini plugin manifest '{manifestPath}' was empty."
                );
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Gemini plugin manifest '{manifestPath}' contains invalid JSON.",
                exception
            );
        }
    }

    private static void ValidateManifest(GeminiPluginManifest manifest, string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            throw new InvalidDataException(
                $"Gemini plugin manifest '{manifestPath}' " + "must specify a non-empty plugin id."
            );
        }

        if (string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            throw new InvalidDataException(
                $"Gemini plugin manifest '{manifestPath}' "
                    + "must specify a non-empty entry assembly."
            );
        }
    }

    private static void EnsurePathIsInsidePlugin(
        string pluginDirectory,
        string entryAssemblyPath,
        string pluginId
    )
    {
        string relativePath = Path.GetRelativePath(pluginDirectory, entryAssemblyPath);

        if (
            Path.IsPathRooted(relativePath)
            || relativePath.Equals("..", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativePath.StartsWith(
                $"..{Path.AltDirectorySeparatorChar}",
                StringComparison.Ordinal
            )
        )
        {
            throw new InvalidDataException(
                $"Entry assembly for Gemini plugin '{pluginId}' "
                    + "must be contained within its plugin directory."
            );
        }
    }

    private static void EnsureUniquePluginIds(IEnumerable<GeminiPluginDescriptor> plugins)
    {
        HashSet<string> pluginIds = new(StringComparer.OrdinalIgnoreCase);

        foreach (GeminiPluginDescriptor plugin in plugins)
        {
            if (!pluginIds.Add(plugin.Id))
            {
                throw new InvalidDataException($"Duplicate Gemini plugin id '{plugin.Id}'.");
            }
        }
    }
}
