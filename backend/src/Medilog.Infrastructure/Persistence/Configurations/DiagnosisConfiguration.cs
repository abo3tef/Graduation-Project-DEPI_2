using Medilog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="Diagnosis"/>.
/// </summary>
public sealed class DiagnosisConfiguration : IEntityTypeConfiguration<Diagnosis>
{
    public void Configure(EntityTypeBuilder<Diagnosis> builder)
    {
        builder.ToTable("Diagnoses");

        builder.HasKey(diagnosis => diagnosis.Id);

        builder.Property(diagnosis => diagnosis.MedicalVisitId).IsRequired();
        builder.Property(diagnosis => diagnosis.PatientId).IsRequired();
        builder.Property(diagnosis => diagnosis.DiagnosisName)
            .IsRequired()
            .HasMaxLength(300);
        builder.Property(diagnosis => diagnosis.Description).HasMaxLength(2000);
        builder.Property(diagnosis => diagnosis.DiagnosisDateUtc).IsRequired();

        // Audit values are supplied by the application, never by the store: CreatedAtUtc is set
        // when the entity is created and the F4-C2 audit interceptor makes both values
        // authoritative on save (F4-C1 review finding #1).
        builder.Property(diagnosis => diagnosis.CreatedAtUtc).ValueGeneratedNever();
        builder.Property(diagnosis => diagnosis.UpdatedAtUtc).ValueGeneratedNever();

        builder.HasIndex(diagnosis => diagnosis.MedicalVisitId);
        builder.HasIndex(diagnosis => new { diagnosis.PatientId, diagnosis.DiagnosisDateUtc });

        // Medical history must never be cascade-deleted (AGENTS.md §4.3).
        builder.HasOne<MedicalVisit>()
            .WithMany()
            .HasForeignKey(diagnosis => diagnosis.MedicalVisitId)
            .OnDelete(DeleteBehavior.Restrict);

        // PatientId has no foreign key until the patient aggregate exists (F4-C1 decision D-02).
    }
}
