using Microsoft.Extensions.DependencyInjection;

namespace Medilog.Application;

/// <summary>
/// Registers Application-layer services. Use cases, handlers and
/// application abstractions are wired here as they are implemented.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Application services are registered here.
        return services;
    }
}
