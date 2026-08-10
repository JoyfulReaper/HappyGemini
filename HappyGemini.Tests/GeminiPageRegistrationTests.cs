using HappyGemini.Extensibility;
using HappyGemini.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace HappyGemini.Tests;

public sealed class GeminiPageRegistrationTests
{
    [Fact]
    public void AddGeminiPagesFromAssemblies_RegistersOnlyAttributedPagesAsScoped()
    {
        ServiceCollection services = new();

        services.AddGeminiPagesFromAssemblies(typeof(GeminiPageRegistrationTests).Assembly);

        ServiceDescriptor descriptor = Assert.Single(
            services,
            service => service.ServiceType == typeof(IGeminiPage)
        );

        Assert.Equal(typeof(RegisteredPage), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [AutoRegisterGeminiPage]
    public sealed class RegisteredPage : IGeminiPage
    {
        public string Path => "/registered";

        public Task WriteAsync(
            GeminiRequest request,
            GeminiResponseWriter response,
            CancellationToken cancellationToken
        )
        {
            return Task.CompletedTask;
        }
    }

    public sealed class UnregisteredPage : IGeminiPage
    {
        public string Path => "/unregistered";

        public Task WriteAsync(
            GeminiRequest request,
            GeminiResponseWriter response,
            CancellationToken cancellationToken
        )
        {
            return Task.CompletedTask;
        }
    }
}
