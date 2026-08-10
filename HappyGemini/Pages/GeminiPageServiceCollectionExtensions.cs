using System.Reflection;
using HappyGemini.Extensibility;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HappyGemini.Pages;

/// <summary>
/// Dependency-injection registration helpers for dynamic Gemini pages.
/// </summary>
public static class GeminiPageServiceCollectionExtensions
{
    /// <summary>
    /// Registers automatically discoverable Gemini pages from the assembly
    /// containing <typeparamref name="TMarker"/>.
    /// </summary>
    public static IServiceCollection AddGeminiPagesFromAssemblyContaining<TMarker>(
        this IServiceCollection services
    )
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddGeminiPagesFromAssemblies(typeof(TMarker).Assembly);
    }

    /// <summary>
    /// Registers concrete, public <see cref="IGeminiPage"/> implementations
    /// marked with <see cref="AutoRegisterGeminiPageAttribute"/>.
    /// </summary>
    public static IServiceCollection AddGeminiPagesFromAssemblies(
        this IServiceCollection services,
        params Assembly[] assemblies
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (Assembly assembly in assemblies.Distinct())
        {
            ArgumentNullException.ThrowIfNull(assembly);

            IEnumerable<TypeInfo> pageTypes = assembly
                .DefinedTypes.Where(IsAutoRegisteredPage)
                .OrderBy(static type => type.FullName ?? type.Name, StringComparer.Ordinal);

            foreach (TypeInfo pageType in pageTypes)
            {
                services.TryAddEnumerable(
                    ServiceDescriptor.Scoped(typeof(IGeminiPage), pageType.AsType())
                );
            }
        }

        return services;
    }

    private static bool IsAutoRegisteredPage(TypeInfo type)
    {
        bool isPublic = type.IsPublic || type.IsNestedPublic;

        return isPublic
            && type.IsClass
            && !type.IsAbstract
            && !type.ContainsGenericParameters
            && typeof(IGeminiPage).IsAssignableFrom(type)
            && type.IsDefined(typeof(AutoRegisterGeminiPageAttribute), inherit: false);
    }
}
