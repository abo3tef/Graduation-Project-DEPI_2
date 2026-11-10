using Medilog.Domain.Entities;
using Medilog.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

internal sealed class DiagnosisConfiguration : BaseEntityConfiguration<Diagnosis>
{
    public override void Configure(EntityTypeBuilder<Diagnosis> builder)
    {
        base.Configure(builder);

        builder.ToTable("diagnoses");

        builder.Property(entity => entity.VisitId)
            .HasColumnName("visit_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(entity => entity.PatientId)
            .HasColumnName("patient_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(entity => entity.DiagnosisName)
            .HasColumnName("diagnosis_name")
            .HasMaxLength(ColumnLengths.Name)
            .IsRequired();

        builder.Property(entity => entity.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(entity => entity.DiagnosisDate)
            .HasColumnName("diagnosis_date")
            .HasColumnType("date")
            .HasConversion(ValueConverters.DateOnlyConverter)
            .IsRequired();

        builder.HasOne(entity => entity.Visit)
            .WithMany(visit => visit.Diagnoses)
            .HasForeignKey(entity => new { entity.VisitId, entity.PatientId })
            .HasPrincipalKey(visit => new { visit.Id, visit.PatientId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_diagnoses_visit");

        builder.HasOne(entity => entity.Patient)
            .WithMany(patient => patient.Diagnoses)
            .HasForeignKey(entity => entity.PatientId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_diagnoses_patient");
    }
}
