using Medilog.Domain.Entities;
using Medilog.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

internal sealed class AllergyConfiguration : BaseEntityConfiguration<Allergy>
{
    public override void Configure(EntityTypeBuilder<Allergy> builder)
    {
        base.Configure(builder);

        builder.ToTable("allergies", table =>
            table.HasCheckConstraint(
                "ck_allergies_severity",
                "`severity` IS NULL OR `severity` IN ('Mild', 'Moderate', 'Severe')"));

        builder.Property(entity => entity.PatientId)
            .HasColumnName("patient_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(entity => entity.AllergyName)
            .HasColumnName("allergy_name")
            .HasMaxLength(ColumnLengths.Name)
            .IsRequired();

        builder.Property(entity => entity.Severity)
            .HasColumnName("severity")
            .HasMaxLength(ColumnLengths.EnumLabel)
            .HasConversion(EnumValueConverters.AllergySeverityConverter);

        builder.Property(entity => entity.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(entity => entity.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.HasOne(entity => entity.Patient)
            .WithMany(patient => patient.Allergies)
            .HasForeignKey(entity => entity.PatientId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_allergies_patient");
    }
}
