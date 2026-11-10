using Medilog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="Medication"/>.
/// </summary>
public sealed class MedicationConfiguration : IEntityTypeConfiguration<Medication>
{
    public void Configure(EntityTypeBuilder<Medication> builder)
    {
        builder.ToTable("Medications");

        builder.HasKey(medication => medication.Id);

        builder.Property(medication => medication.MedicalVisitId).IsRequired();
        builder.Property(medication => medication.PatientId).IsRequired();
        builder.Property(medication => medication.MedicationName)
            .IsRequired()
            .HasMaxLength(200);
        builder.Property(medication => medication.Dosage).HasMaxLength(100);
        builder.Property(medication => medication.Frequency).HasMaxLength(100);
        builder.Property(medication => medication.Instructions).HasMaxLength(500);

        // Audit values are supplied by the application, never by the store: CreatedAtUtc is set
        // when the entity is created and the F4-C2 audit interceptor makes both values
        // authoritative on save (F4-C1 review finding #1).
        builder.Property(medication => medication.CreatedAtUtc).ValueGeneratedNever();
        builder.Property(medication => medication.UpdatedAtUtc).ValueGeneratedNever();

        builder.HasIndex(medication => medication.MedicalVisitId);
        builder.HasIndex(medication => medication.PatientId);
        builder.HasIndex(medication => medication.MedicationName);

        // Medical history must never be cascade-deleted (AGENTS.md §4.3).
        builder.HasOne<MedicalVisit>()
            .WithMany()
            .HasForeignKey(medication => medication.MedicalVisitId)
            .OnDelete(DeleteBehavior.Restrict);

        // PatientId has no foreign key until the patient aggregate exists (F4-C1 decision D-02).
        //
        // DurationInDays >= 1 is enforced in the entity/application flow for now; the check
        // constraint needs the provider identifier syntax and is added once the provider is
        // chosen (F4-C1 decision D-01).
    }
}
