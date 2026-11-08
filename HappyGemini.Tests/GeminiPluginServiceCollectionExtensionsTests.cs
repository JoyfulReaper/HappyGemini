
using HappyGemini.Extensibility;
using HappyGemini.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HappyGemini.Tests;

public sealed class GeminiPluginServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGeminiPlugins_RegistersPluginServicesAndResolvesPage()
    {
        string pluginDirectory = Path.Combine(AppContext.BaseDirectory, "plugins");

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GeminiPages:PluginDirectory"] = pluginDirectory
            })
            .Build();

        ServiceCollection services = new();

        services.AddGeminiPlugins(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        IGeminiPage page = Assert.Single(
            scope.ServiceProvider.GetServices<IGeminiPage>());

        Assert.Equal("/plugin-test", page.Path);

        GeminiLoadedPlugin loaded = Assert.Single(
            provider.GetServices<GeminiLoadedPlugin>());

        Assert.Equal("HappyGemini.Tests.TestPlugin", loaded.Descriptor.Id);
    }
}
