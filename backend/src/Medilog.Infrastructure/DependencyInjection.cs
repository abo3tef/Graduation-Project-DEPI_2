using Medilog.Application.Common.Abstractions;
using Medilog.Application.Interfaces;
using Medilog.Infrastructure.Persistence.Appointments;
using Medilog.Infrastructure.Persistence.Context;
using Medilog.Infrastructure.Persistence.MedicalRecords;
using Medilog.Infrastructure.Security;
using Medilog.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Medilog.Infrastructure;

/// <summary>
/// Registers Infrastructure-layer services, including the EF Core database context.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = Environment.GetEnvironmentVariable("MEDILOG_CONNECTION_STRING");
        }

        services.AddDbContext<MedilogDbContext>(options =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "No MySQL connection string is configured. Set " +
                    "'ConnectionStrings:DefaultConnection' with User Secrets or the " +
                    "MEDILOG_CONNECTION_STRING environment variable.");
            }

            options.UseMySQL(connectionString);
        });

        // Appointment persistence port (Application-owned) and clinic clock.
        services.AddScoped<IAppointmentStore, AppointmentStore>();
        services.AddScoped<IMedicalRecordStore, MedicalRecordStore>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IClinicClock, ClinicClock>();

        // Authentication primitives (F6-C1, decisions D-34/D-35). The JWT section is bound here;
        // only the signing key is secret and it stays in user-secrets / environment variables.
        services.AddSingleton(JwtOptions.FromConfiguration(configuration));
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        // Medical Records database abstraction.
        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<MedilogDbContext>());

        return services;
    }
}
