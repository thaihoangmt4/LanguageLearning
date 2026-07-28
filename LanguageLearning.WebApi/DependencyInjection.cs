namespace LanguageLearning.WebApi;

/// <summary>
/// Registers WebApi-layer services into the dependency injection container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds WebApi project services to the service collection.
    /// </summary>
    public static IServiceCollection AddWebApi(this IServiceCollection services)
    {
        return services;
    }
}
