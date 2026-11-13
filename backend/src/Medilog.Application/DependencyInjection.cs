using System.Reflection;
using FluentValidation;
using Medilog.Application.Features.Appointments;
using Medilog.Application.Features.MedicalRecords;
using Microsoft.Extensions.DependencyInjection;

namespace Medilog.Application;

/// <summary>
/// Registers Application-layer services, validators, and mapping profiles.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Use cases (decision D-08): plain application services, no MediatR.
        services.AddScoped<IMedicalVisitService, MedicalVisitService>();

        // FluentValidation (AGENTS.md §3.2): every validator in this assembly is
        // registered centrally, close to the use case it validates.
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // AutoMapper (AGENTS.md §3.3): profiles in this assembly are discovered
        // centrally. The commercial license key is supplied through the
        // AUTOMAPPER_LICENSE_KEY environment variable and is never committed.
        services.AddAutoMapper(configuration => { }, Assembly.GetExecutingAssembly());

        // Appointment use cases. Requires an ICurrentUser, which the WebAPI host provides.
        services.AddScoped<IAppointmentService, AppointmentService>();

        // Medical-record read use cases (F3). Same pattern: requires an
        // ICurrentUser, which the WebAPI host provides. Controllers arrive in the
        // next F3 (API) cycle.
        services.AddScoped<IMedicalRecordService, MedicalRecordService>();

        return services;
    }
}
