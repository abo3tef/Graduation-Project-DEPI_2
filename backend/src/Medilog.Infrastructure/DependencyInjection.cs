using Medilog.Application.Common.Abstractions;
using Medilog.Application.Features.Auth;
using Medilog.Application.Interfaces;
using Medilog.Infrastructure.Email;
using Medilog.Infrastructure.Persistence.Appointments;
using Medilog.Infrastructure.Persistence.Context;
using Medilog.Infrastructure.Persistence.MedicalRecords;
using Medilog.Infrastructure.Persistence.Seed;
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
    /// <param name="contentRootPath">
    /// Base directory for relative local paths (the development email sink). The WebAPI host passes
    /// its content root; when omitted, the application base directory is used.
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string? contentRootPath = null)
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
        // Options are created from IConfiguration at resolution time so test hosts and command
        // line overrides are honored.
        services.AddSingleton(provider =>
            JwtOptions.FromConfiguration(provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton(provider =>
            CreateTokenLifetimes(provider.GetRequiredService<IConfiguration>()));

        // Transactional email (F6-C1-T9, decisions D-39/D-41). The SMTP password is a secret and
        // stays in user-secrets / environment variables; the dev sink path is resolved against the
        // host content root so it lands in the repository's gitignored logs directory.
        services.AddSingleton(provider =>
            EmailOptions.FromConfiguration(provider.GetRequiredService<IConfiguration>(), contentRootPath));
        services.AddSingleton(provider =>
            AuthLinkOptions.FromConfiguration(provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton<IAuthLinkBuilder, AuthLinkBuilder>();
        services.AddSingleton<IEmailService, EmailService>();

        // Google Sign-In (F6-C1-T8, Q-12). The project id is public configuration; the signing
        // certificates are fetched from Google by the validator.
        services.AddSingleton(provider =>
            FirebaseOptions.FromConfiguration(provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton<IGoogleTokenValidator, FirebaseGoogleTokenValidator>();

        // Development-only demo accounts (decision D-40). The host decides whether to call this.
        services.AddSingleton(provider =>
            SeedOptions.FromConfiguration(provider.GetRequiredService<IConfiguration>()));
        services.AddScoped<DevelopmentSeeder>();

        // Medical Records database abstraction.
        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<MedilogDbContext>());

        return services;
    }

    /// <summary>
    /// Reads the approved token lifetimes from the <c>Auth</c> section (decision D-36), falling back
    /// to the demo defaults when a value is missing or invalid.
    /// </summary>
    private static AuthTokenLifetimes CreateTokenLifetimes(IConfiguration configuration)
    {
        var section = configuration.GetSection(AuthLinkOptions.SectionName);
        var lifetimes = new AuthTokenLifetimes();

        if (int.TryParse(section["EmailVerificationTokenMinutes"], out var verificationMinutes) &&
            verificationMinutes > 0)
        {
            lifetimes.EmailVerification = TimeSpan.FromMinutes(verificationMinutes);
        }

        if (int.TryParse(section["PasswordResetTokenMinutes"], out var resetMinutes) && resetMinutes > 0)
        {
            lifetimes.PasswordReset = TimeSpan.FromMinutes(resetMinutes);
        }

        return lifetimes;
    }
}
