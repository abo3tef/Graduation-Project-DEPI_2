using Medilog.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
