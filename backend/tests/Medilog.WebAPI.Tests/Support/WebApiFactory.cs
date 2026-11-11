using Medilog.Application.Common.Security;
using Medilog.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Medilog.WebAPI.Tests.Support;

/// <summary>
/// Hosts the real WebAPI pipeline for integration tests (decision D-19).
/// </summary>
/// <remarks>
/// Only the database registration and the default authentication scheme are replaced. In
/// particular <c>IApplicationDbContext</c> is intentionally left untouched, so every request also
/// proves that the production Infrastructure registration (D-21) resolves the concrete context.
/// </remarks>
public sealed class WebApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"medilog-webapi-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Remove the MySQL registration (and every options configuration that goes with it)
            // before the first resolve, otherwise the missing development connection string throws.
            services.RemoveAll<MedilogDbContext>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<DbContextOptions<MedilogDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<MedilogDbContext>));

            services.AddDbContext<MedilogDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });
        });
    }

    /// <summary>Creates a client that sends the test identity headers instead of a bearer token.</summary>
    public HttpClient CreateClientFor(Guid? userId, string? role = Roles.Doctor)
    {
        var client = CreateClient();

        if (userId is { } id)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, id.ToString());
        }

        if (role is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
        }

        return client;
    }

    /// <summary>Writes synthetic entities into the throwaway database (no real patient data).</summary>
    public async Task SeedAsync(Action<MedilogDbContext> seed)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MedilogDbContext>();

        seed(dbContext);
        await dbContext.SaveChangesAsync();
    }
}
