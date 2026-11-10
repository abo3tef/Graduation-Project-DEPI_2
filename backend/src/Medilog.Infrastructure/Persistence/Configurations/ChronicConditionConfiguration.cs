using Medilog.Domain.Entities;
using Medilog.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

internal sealed class ChronicConditionConfiguration : BaseEntityConfiguration<ChronicCondition>
{
    public override void Configure(EntityTypeBuilder<ChronicCondition> builder)
    {
        base.Configure(builder);

        builder.ToTable("chronic_conditions", table =>
            table.HasCheckConstraint(
                "ck_chronic_conditions_status",
                "`status` IS NULL OR `status` IN ('Active', 'Controlled', 'Resolved')"));

        builder.Property(entity => entity.PatientId)
            .HasColumnName("patient_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(entity => entity.ConditionName)
            .HasColumnName("condition_name")
            .HasMaxLength(ColumnLengths.Name)
            .IsRequired();

        builder.Property(entity => entity.DiagnosisDate)
            .HasColumnName("diagnosis_date")
            .HasColumnType("date")
            .HasConversion(ValueConverters.NullableDateOnlyConverter);

        builder.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasMaxLength(ColumnLengths.EnumLabel)
            .HasConversion(EnumValueConverters.ChronicConditionStatusConverter);

        builder.Property(entity => entity.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.HasOne(entity => entity.Patient)
            .WithMany(patient => patient.ChronicConditions)
            .HasForeignKey(entity => entity.PatientId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_chronic_conditions_patient");
    }
}
