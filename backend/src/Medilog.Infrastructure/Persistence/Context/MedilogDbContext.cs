using Medilog.Domain.Common;
using Medilog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MySql.EntityFrameworkCore.Extensions;

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

    public DbSet<Doctor> Doctors => Set<Doctor>();

    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<MedicalVisit> MedicalVisits => Set<MedicalVisit>();

    public DbSet<Diagnosis> Diagnoses => Set<Diagnosis>();

    public DbSet<Medication> Medications => Set<Medication>();

    public DbSet<Allergy> Allergies => Set<Allergy>();

    public DbSet<ChronicCondition> ChronicConditions => Set<ChronicCondition>();

    public DbSet<VitalSigns> VitalSigns => Set<VitalSigns>();

    public DbSet<LaboratoryResult> LaboratoryResults => Set<LaboratoryResult>();

    public DbSet<MedicalNote> MedicalNotes => Set<MedicalNote>();

    public DbSet<MedicalDocument> MedicalDocuments => Set<MedicalDocument>();

    public DbSet<Secretary> Secretaries => Set<Secretary>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MedilogDbContext).Assembly);

        // MySQL tables are created with an explicit character set and collation
        // so that patient names and free-text notes are stored consistently.
        // Applying this per entity also covers the secretary-doctor join table.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var entityBuilder = modelBuilder.Entity(entityType.Name);
            entityBuilder.ForMySQLHasCharset("utf8mb4");
            entityBuilder.ForMySQLHasCollation("utf8mb4_0900_ai_ci");
        }

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Applies the approved audit behavior without a second audit mechanism:
    /// creation timestamps are assigned on insert and protected afterwards,
    /// and update timestamps are assigned on every update.
    /// </summary>
    private void ApplyAuditRules()
    {
        var utcNow = DateTimeOffset.UtcNow;

        foreach (EntityEntry<CreatedEntity> entry in ChangeTracker.Entries<CreatedEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = utcNow;
                    break;
                case EntityState.Modified:
                    entry.Property(nameof(CreatedEntity.CreatedAtUtc)).IsModified = false;
                    break;
            }
        }

        foreach (EntityEntry<AuditableEntity> entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.UpdatedAtUtc = null;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = utcNow;
                    break;
            }
        }
    }
}
