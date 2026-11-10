using Medilog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

internal sealed class VitalSignsConfiguration : BaseEntityConfiguration<VitalSigns>
{
    public override void Configure(EntityTypeBuilder<VitalSigns> builder)
    {
        base.Configure(builder);

        builder.ToTable("vital_signs");

        builder.Property(entity => entity.VisitId)
            .HasColumnName("visit_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(entity => entity.BloodPressure)
            .HasColumnName("blood_pressure")
            .HasMaxLength(ColumnLengths.BloodPressure);

        builder.Property(entity => entity.HeartRate)
            .HasColumnName("heart_rate");

        builder.Property(entity => entity.Temperature)
            .HasColumnName("temperature")
            .HasPrecision(5, 2);

        builder.Property(entity => entity.RespiratoryRate)
            .HasColumnName("respiratory_rate");

        builder.Property(entity => entity.Weight)
            .HasColumnName("weight")
            .HasPrecision(6, 2);

        builder.Property(entity => entity.Height)
            .HasColumnName("height")
            .HasPrecision(5, 2);

        builder.Property(entity => entity.SpO2)
            .HasColumnName("spo2");

        builder.HasOne(entity => entity.Visit)
            .WithMany(visit => visit.VitalSigns)
            .HasForeignKey(entity => entity.VisitId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_vital_signs_visit");
    }
}
