using HappyGemini.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HappyGemini.TestPlugin;

public sealed class TestPlugin : IHappyGeminiPlugin
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<TestPluginDependency>();
    }
}

public sealed class TestPluginDependency
{
}