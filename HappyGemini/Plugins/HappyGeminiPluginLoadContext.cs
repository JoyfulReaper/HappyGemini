using System.Reflection;
using System.Runtime.Loader;
using HappyGemini.Extensibility;

namespace HappyGemini.Plugins;

/// <summary>
/// Provides an isolated assembly load context for a HappyGemini plugin.
/// </summary>
internal sealed class HappyGeminiPluginLoadContext : AssemblyLoadContext
{
    private static readonly Assembly ExtensibilityAssembly = typeof(IGeminiPage).Assembly;

    private static readonly string ExtensibilityAssemblyName = ExtensibilityAssembly
        .GetName()
        .Name!;

    private readonly AssemblyDependencyResolver _resolver;

    public HappyGeminiPluginLoadContext(GeminiPluginDescriptor plugin)
        : base(name: GetContextName(plugin), isCollectible: false)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        _resolver = new AssemblyDependencyResolver(plugin.EntryAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // All plugins must use the host's copy of the
        // HappyGemini extensibility contract.
        if (
            string.Equals(
                assemblyName.Name,
                ExtensibilityAssemblyName,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return ExtensibilityAssembly;
        }

        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);

        if (assemblyPath is null)
        {
            return null;
        }

        return LoadFromAssemblyPath(assemblyPath);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

        if (libraryPath is null)
        {
            return nint.Zero;
        }

        return LoadUnmanagedDllFromPath(libraryPath);
    }

    private static string GetContextName(GeminiPluginDescriptor plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        return $"HappyGemini.Plugin:{plugin.Id}";
    }
}
