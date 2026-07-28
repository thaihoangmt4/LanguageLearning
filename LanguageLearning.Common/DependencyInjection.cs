using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Common;

/// <summary>
/// Registers Common-layer services into the dependency injection container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Common project services to the service collection.
    /// </summary>
    public static IServiceCollection AddCommon(this IServiceCollection services)
    {
        return services;
    }
}
