using Medilog.Domain.Entities;
using Medilog.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

internal sealed class LaboratoryResultConfiguration : BaseEntityConfiguration<LaboratoryResult>
{
    public override void Configure(EntityTypeBuilder<LaboratoryResult> builder)
    {
        base.Configure(builder);

        builder.ToTable("laboratory_results");

        builder.Property(entity => entity.PatientId)
            .HasColumnName("patient_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(entity => entity.VisitId)
            .HasColumnName("visit_id")
            .HasColumnType("char(36)");

        builder.Property(entity => entity.TestName)
            .HasColumnName("test_name")
            .HasMaxLength(ColumnLengths.Name)
            .IsRequired();

        builder.Property(entity => entity.TestDate)
            .HasColumnName("test_date")
            .HasColumnType("date")
            .HasConversion(ValueConverters.DateOnlyConverter)
            .IsRequired();

        builder.Property(entity => entity.Value)
            .HasColumnName("value")
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(entity => entity.Unit)
            .HasColumnName("unit")
            .HasMaxLength(ColumnLengths.Unit)
            .IsRequired();

        builder.Property(entity => entity.ReferenceRange)
            .HasColumnName("reference_range")
            .HasMaxLength(ColumnLengths.ReferenceRange);

        builder.Property(entity => entity.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.HasOne(entity => entity.Visit)
            .WithMany(visit => visit.LaboratoryResults)
            .HasForeignKey(entity => new { entity.VisitId, entity.PatientId })
            .HasPrincipalKey(visit => new { visit.Id, visit.PatientId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_laboratory_results_visit");

        builder.HasOne(entity => entity.Patient)
            .WithMany(patient => patient.LaboratoryResults)
            .HasForeignKey(entity => entity.PatientId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_laboratory_results_patient");

        builder.HasIndex(entity => new { entity.PatientId, entity.TestDate })
            .HasDatabaseName("ix_laboratory_results_patient_id_test_date");
    }
}
