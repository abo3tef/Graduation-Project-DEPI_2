using System.Reflection;
using FluentValidation;
using Medilog.Application.Features.Appointments;
using Medilog.Application.Features.Auth;
using Medilog.Application.Features.Dashboard;
using Medilog.Application.Features.MedicalRecords;
using Medilog.Application.Features.Patients;
using Medilog.Application.Features.Payments;
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
        services.AddScoped<IVitalSignsService, VitalSignsService>();

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

        // Doctor dashboard service (F5).
        services.AddScoped<IDoctorDashboardService, DoctorDashboardService>();

        // Authentication use cases (F6-C1). Depend on infrastructure primitives registered by
        // AddInfrastructure; the raw password never leaves the process.
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<AuthTokenService>();

        // Payment use cases (F7). Requires an ICurrentUser, which the WebAPI host provides.
        services.AddScoped<IPaymentService, PaymentService>();

        // Patient management use cases (F8). Requires an ICurrentUser, which the WebAPI host provides.
        services.AddScoped<IPatientService, PatientService>();

        return services;
    }
}
