using HappyGemini.Pages;

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

            services.AddGeminiPagesFromAssemblies(loaded.EntryAssembly);

            // Retain metadata about loaded plugins for diagnostics
            // and future status/reporting features.
            services.AddSingleton(loaded);
        }

        return services;
    }
}
