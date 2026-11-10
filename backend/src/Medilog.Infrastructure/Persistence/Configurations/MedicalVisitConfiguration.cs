using Medilog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="MedicalVisit"/>.
/// </summary>
public sealed class MedicalVisitConfiguration : IEntityTypeConfiguration<MedicalVisit>
{
    public void Configure(EntityTypeBuilder<MedicalVisit> builder)
    {
        builder.ToTable("MedicalVisits");

        builder.HasKey(visit => visit.Id);

        builder.Property(visit => visit.PatientId).IsRequired();
        builder.Property(visit => visit.DoctorId).IsRequired();
        builder.Property(visit => visit.VisitDateUtc).IsRequired();
        builder.Property(visit => visit.ChiefComplaint).HasMaxLength(1000);
        builder.Property(visit => visit.ClinicalNotes).HasMaxLength(4000);

        // Audit values are supplied by the application, never by the store: CreatedAtUtc is set
        // when the entity is created and the F4-C2 audit interceptor makes both values
        // authoritative on save (F4-C1 review finding #1).
        builder.Property(visit => visit.CreatedAtUtc).ValueGeneratedNever();
        builder.Property(visit => visit.UpdatedAtUtc).ValueGeneratedNever();

        // The patient and doctor aggregates are owned by other features. Until they exist,
        // the identifiers are stored as plain indexed columns and no foreign-key constraint is
        // configured (F4-C1 decision D-02, option C).
        builder.HasIndex(visit => new { visit.DoctorId, visit.VisitDateUtc });
        builder.HasIndex(visit => new { visit.PatientId, visit.VisitDateUtc });
    }
}
