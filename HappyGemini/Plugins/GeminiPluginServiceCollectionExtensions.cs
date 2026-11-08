using HappyGemini.Extensibility;
using HappyGemini.Pages;
using System.Reflection;

namespace HappyGemini.Plugins;

/// <summary>
/// Registers external HappyGemini plugins and their pages.
/// </summary>
public static class GeminiPluginServiceCollectionExtensions
{
    public static IServiceCollection AddGeminiPlugins(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        GeminiPluginOptions options =
            configuration.GetSection(GeminiPluginOptions.SectionName).Get<GeminiPluginOptions>()
            ?? new GeminiPluginOptions();

        if (string.IsNullOrWhiteSpace(options.PluginDirectory))
        {
            throw new InvalidOperationException(
                $"{GeminiPluginOptions.SectionName}:PluginDirectory " + "must not be empty."
            );
        }

        GeminiPluginDiscovery discovery = new();
        GeminiPluginLoader loader = new();

        IReadOnlyList<GeminiPluginDescriptor> descriptors = discovery.Discover(
            options.PluginDirectory
        );

        foreach (GeminiPluginDescriptor descriptor in descriptors)
        {
            GeminiLoadedPlugin loaded = loader.Load(descriptor);

            ConfigurePluginServices(loaded, services, configuration);

            services.AddGeminiPagesFromAssemblies(loaded.EntryAssembly);

            // Retain metadata about loaded plugins for diagnostics
            // and future status/reporting features.
            services.AddSingleton(loaded);
        }

        return services;
    }

    private static void ConfigurePluginServices(
        GeminiLoadedPlugin loaded,
        IServiceCollection services,
        IConfiguration configuration)
    {
        IEnumerable<TypeInfo> pluginTypes = loaded.EntryAssembly.DefinedTypes
            .Where(type =>
                type.IsPublic &&
                type.IsClass &&
                !type.IsAbstract &&
                !type.ContainsGenericParameters &&
                typeof(IHappyGeminiPlugin).IsAssignableFrom(type))
            .OrderBy(type => type.FullName ?? type.Name, StringComparer.Ordinal);

        foreach (TypeInfo pluginType in pluginTypes)
        {
            if (Activator.CreateInstance(pluginType.AsType()) is not IHappyGeminiPlugin plugin)
            {
                throw new InvalidOperationException(
                    $"Could not create plugin entry point '{pluginType.FullName}' " +
                    $"for '{loaded.Descriptor.Id}'.");
            }

            plugin.ConfigureServices(services, configuration);
        }
    }
}
