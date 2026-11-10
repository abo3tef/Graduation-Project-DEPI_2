using Medilog.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Medilog.Infrastructure.Persistence;

/// <summary>
/// Creates the <see cref="MedilogDbContext"/> for EF Core design-time commands
/// such as <c>dotnet ef migrations add</c>.
/// </summary>
/// <remarks>
/// The real connection string is expected in the
/// <c>MEDILOG_CONNECTION_STRING</c> environment variable and must never be
/// committed to source control. The fallback below is a clearly local
/// placeholder: model building and migration scaffolding do not open a
/// database connection, and actual database commands must override it.
/// </remarks>
public sealed class MedilogDbContextFactory : IDesignTimeDbContextFactory<MedilogDbContext>
{
    private const string PlaceholderConnectionString =
        "Server=127.0.0.1;Port=3306;Database=medilog_design;User=medilog;";

    public MedilogDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MEDILOG_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = PlaceholderConnectionString;
        }

        var optionsBuilder = new DbContextOptionsBuilder<MedilogDbContext>();
        optionsBuilder.UseMySQL(connectionString);

        return new MedilogDbContext(optionsBuilder.Options);
    }
}
