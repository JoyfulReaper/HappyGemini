using HappyGemini.Extensibility;
using System.Reflection;
using System.Runtime.Loader;

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

        // Microsoft.Extensions abstractions cross the host/plugin boundary.
        // Reuse the host's loaded assemblies to preserve type identity.
        if (assemblyName.Name?.StartsWith(
                "Microsoft.Extensions.",
                StringComparison.Ordinal) == true)
        {
            Assembly? sharedAssembly =
                AssemblyLoadContext.Default.Assemblies
                    .FirstOrDefault(assembly =>
                        string.Equals(
                            assembly.GetName().Name,
                            assemblyName.Name,
                            StringComparison.OrdinalIgnoreCase));

            if (sharedAssembly is not null)
            {
                return sharedAssembly;
            }
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
