using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HappyGemini.Extensibility;

/// <summary>
/// Allows an external HappyGemini plugin to register its own services.
/// </summary>
public interface IHappyGeminiPlugin
{
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
}