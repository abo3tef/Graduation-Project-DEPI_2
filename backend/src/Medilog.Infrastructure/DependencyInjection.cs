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
        // The database provider has not been selected yet. The BRD does not
        // require a specific database, so no provider package is installed.
        //
        // Once the team chooses a provider, install its EF Core package and
        // configure it below, for example:
        //
        //   options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
        //   options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
        services.AddDbContext<MedilogDbContext>(options =>
        {
            // Provider configuration goes here.
        });

        return services;
    }
}
