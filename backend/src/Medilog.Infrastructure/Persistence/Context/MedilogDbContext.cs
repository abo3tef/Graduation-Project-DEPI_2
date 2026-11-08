using Microsoft.EntityFrameworkCore;

namespace Medilog.Infrastructure.Persistence.Context;

/// <summary>
/// Entity Framework Core database context for the Medilog platform.
/// Entity configurations are discovered from this assembly, so every
/// <c>IEntityTypeConfiguration&lt;TEntity&gt;</c> in
/// <c>Persistence/Configurations</c> is applied automatically.
/// </summary>
public sealed class MedilogDbContext : DbContext
{
    public MedilogDbContext(DbContextOptions<MedilogDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MedilogDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
