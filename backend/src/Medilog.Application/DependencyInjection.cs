using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Medilog.Application;

/// <summary>
/// Registers Application-layer services, validators, and mapping profiles.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // FluentValidation (AGENTS.md §3.2): every validator in this assembly is
        // registered centrally, close to the use case it validates.
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // AutoMapper (AGENTS.md §3.3): profiles in this assembly are discovered
        // centrally. The commercial license key is supplied through the
        // AUTOMAPPER_LICENSE_KEY environment variable and is never committed.
        services.AddAutoMapper(configuration => { }, Assembly.GetExecutingAssembly());

        // NOTE (F1-C2): IAppointmentService is intentionally NOT registered yet.
        // It needs an ICurrentUser implementation, which arrives with the
        // authentication work (Q-07); registering it earlier would make the host
        // fail its development-time service validation. F1-C3 registers it:
        //   services.AddScoped<IAppointmentService, AppointmentService>();

        return services;
    }
}
