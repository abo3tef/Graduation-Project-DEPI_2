using Medilog.Domain.Entities;
using Medilog.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : CreatedEntityConfiguration<Payment>
{
    public override void Configure(EntityTypeBuilder<Payment> builder)
    {
        base.Configure(builder);

        builder.ToTable("payments", table =>
        {
            table.HasCheckConstraint(
                "ck_payments_status",
                "`status` IN ('Pending', 'Partial', 'Paid')");
            table.HasCheckConstraint(
                "ck_payments_method",
                "`method` IN ('Cash', 'Card')");
            table.HasCheckConstraint(
                "ck_payments_amount_positive",
                "`amount` > 0");
        });

        builder.Property(entity => entity.AppointmentId)
            .HasColumnName("appointment_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(entity => entity.PatientId)
            .HasColumnName("patient_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(entity => entity.DoctorId)
            .HasColumnName("doctor_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(entity => entity.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(entity => entity.Amount)
            .HasColumnName("amount")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(entity => entity.Method)
            .HasColumnName("method")
            .HasMaxLength(ColumnLengths.EnumLabel)
            .HasConversion(EnumValueConverters.PaymentMethodConverter)
            .IsRequired();

        builder.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasMaxLength(ColumnLengths.EnumLabel)
            .HasConversion(EnumValueConverters.PaymentStatusConverter)
            .IsRequired();

        builder.Property(entity => entity.PaidAtUtc)
            .HasColumnName("paid_at_utc")
            .HasColumnType("datetime(6)")
            .HasConversion(ValueConverters.NullableUtcDateTimeOffsetConverter);

        builder.Property(entity => entity.Notes)
            .HasColumnName("notes")
            .HasMaxLength(Payment.NotesMaxLength);

        builder.HasOne(entity => entity.Appointment)
            .WithMany()
            .HasForeignKey(entity => entity.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_payments_appointment");

        builder.HasOne(entity => entity.Patient)
            .WithMany()
            .HasForeignKey(entity => entity.PatientId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_payments_patient");

        builder.HasOne(entity => entity.Doctor)
            .WithMany()
            .HasForeignKey(entity => entity.DoctorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_payments_doctor");

        // Q-21: exactly one payment per appointment, enforced by the database.
        builder.HasIndex(entity => entity.AppointmentId)
            .IsUnique()
            .HasDatabaseName("ux_payments_appointment_id");

        builder.HasIndex(entity => entity.PatientId)
            .HasDatabaseName("ix_payments_patient_id");

        builder.HasIndex(entity => new { entity.DoctorId, entity.PaidAtUtc })
            .HasDatabaseName("ix_payments_doctor_id_paid_at_utc");
    }
}
