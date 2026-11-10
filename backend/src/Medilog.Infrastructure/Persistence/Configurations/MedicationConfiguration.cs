using Medilog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

internal sealed class MedicationConfiguration : BaseEntityConfiguration<Medication>
{
    public override void Configure(EntityTypeBuilder<Medication> builder)
    {
        base.Configure(builder);

        builder.ToTable("medications");

        builder.Property(entity => entity.VisitId)
            .HasColumnName("visit_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(entity => entity.PatientId)
            .HasColumnName("patient_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(entity => entity.MedicationName)
            .HasColumnName("medication_name")
            .HasMaxLength(ColumnLengths.Name)
            .IsRequired();

        builder.Property(entity => entity.Dosage)
            .HasColumnName("dosage")
            .HasMaxLength(ColumnLengths.ShortLabel)
            .IsRequired();

        builder.Property(entity => entity.Frequency)
            .HasColumnName("frequency")
            .HasMaxLength(ColumnLengths.ShortLabel)
            .IsRequired();

        builder.Property(entity => entity.Duration)
            .HasColumnName("duration")
            .HasMaxLength(ColumnLengths.ShortLabel);

        builder.Property(entity => entity.Instructions)
            .HasColumnName("instructions")
            .HasColumnType("text");

        builder.HasOne(entity => entity.Visit)
            .WithMany(visit => visit.Medications)
            .HasForeignKey(entity => new { entity.VisitId, entity.PatientId })
            .HasPrincipalKey(visit => new { visit.Id, visit.PatientId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_medications_visit");

        builder.HasOne(entity => entity.Patient)
            .WithMany(patient => patient.Medications)
            .HasForeignKey(entity => entity.PatientId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_medications_patient");
    }
}
