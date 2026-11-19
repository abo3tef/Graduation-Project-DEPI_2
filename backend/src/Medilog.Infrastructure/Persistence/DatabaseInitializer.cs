using Medilog.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Medilog.Infrastructure.Persistence;

/// <summary>
/// Applies the checked-in EF Core migrations before any other startup database work.
/// </summary>
/// <remarks>
/// <para>
/// The host calls this once, before the development seeder, so the seeder never queries tables that
/// do not exist yet. Migrations can be skipped with <c>Database:ApplyMigrationsOnStartup = false</c>
/// when a contributor prefers to run <c>dotnet ef database update</c> explicitly.
/// </para>
/// <para>
/// Failures are logged with the original exception and rethrown with an actionable message instead
/// of surfacing as an unexplained startup crash. Nothing is swallowed: a database that cannot be
/// migrated must still stop the host.
/// </para>
/// </remarks>
public sealed class DatabaseInitializer
{
    public const string SectionName = "Database";
    public const string ApplyMigrationsKey = "ApplyMigrationsOnStartup";

    private readonly MedilogDbContext _dbContext;
    private readonly bool _applyMigrationsOnStartup;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        MedilogDbContext dbContext,
        IConfiguration configuration,
        ILogger<DatabaseInitializer> logger)
    {
        _dbContext = dbContext;

        // Read manually, like the other option types in this layer: the project does not reference
        // the configuration binder. An absent or invalid value means migrations stay enabled.
        var configuredValue = configuration[$"{SectionName}:{ApplyMigrationsKey}"];
        _applyMigrationsOnStartup = !bool.TryParse(configuredValue, out var enabled) || enabled;

        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!_applyMigrationsOnStartup)
        {
            _logger.LogInformation(
                "Startup migrations are disabled ({Key}=false); assuming the schema is already current.",
                $"{SectionName}:{ApplyMigrationsKey}");
            return;
        }

        // Integration tests replace the MySQL registration with an in-memory provider. Migrations are
        // a relational-only operation, so skip them instead of failing the host with a
        // relational-specific exception. ProviderName is safe to read for every provider.
        if (!_dbContext.Database.IsRelational())
        {
            _logger.LogInformation(
                "Skipping startup migrations for non-relational provider {Provider}.",
                _dbContext.Database.ProviderName ?? "unknown");
            return;
        }

        try
        {
            await _dbContext.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Applying Medilog database migrations failed with provider {Provider}.",
                _dbContext.Database.ProviderName ?? "unknown");

            throw new InvalidOperationException(
                "Database initialization failed. Verify that the MySQL/MariaDB server is running and " +
                $"that 'ConnectionStrings:DefaultConnection' or MEDILOG_CONNECTION_STRING points to it. " +
                "The logged exception contains the provider details.",
                exception);
        }
    }
}
