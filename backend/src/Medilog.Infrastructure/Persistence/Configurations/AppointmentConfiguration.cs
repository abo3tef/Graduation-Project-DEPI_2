using Medilog.Domain.Entities;
using Medilog.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentConfiguration : CreatedEntityConfiguration<Appointment>
{
    public override void Configure(EntityTypeBuilder<Appointment> builder)
    {
        base.Configure(builder);

        builder.ToTable("appointments", table =>
        {
            table.HasCheckConstraint(
                "ck_appointments_status",
                "`status` IN ('Scheduled', 'Confirmed', 'Completed', 'Cancelled', 'No-Show')");
            table.HasCheckConstraint(
                "ck_appointments_end_time_after_start_time",
                "`end_time` IS NULL OR `end_time` > `start_time`");
        });

        builder.HasAlternateKey(entity => new { entity.Id, entity.PatientId, entity.DoctorId })
            .HasName("ak_appointments_id_patient_id_doctor_id");

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

        builder.Property(entity => entity.AppointmentDate)
            .HasColumnName("appointment_date")
            .HasColumnType("date")
            .HasConversion(ValueConverters.DateOnlyConverter)
            .IsRequired();

        builder.Property(entity => entity.StartTime)
            .HasColumnName("start_time")
            .HasColumnType("time(0)")
            .HasConversion(ValueConverters.TimeOnlyConverter)
            .IsRequired();

        builder.Property(entity => entity.EndTime)
            .HasColumnName("end_time")
            .HasColumnType("time(0)")
            .HasConversion(ValueConverters.NullableTimeOnlyConverter);

        builder.Property(entity => entity.Reason)
            .HasColumnName("reason")
            .HasColumnType("text");

        builder.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasMaxLength(ColumnLengths.EnumLabel)
            .HasConversion(EnumValueConverters.AppointmentStatusConverter)
            .IsRequired();

        builder.Property(entity => entity.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(entity => entity.CancellationReason)
            .HasColumnName("cancellation_reason")
            .HasColumnType("text");

        builder.HasOne(entity => entity.Patient)
            .WithMany(patient => patient.Appointments)
            .HasForeignKey(entity => entity.PatientId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_appointments_patient");

        builder.HasOne(entity => entity.Doctor)
            .WithMany(doctor => doctor.Appointments)
            .HasForeignKey(entity => entity.DoctorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_appointments_doctor");

        builder.HasIndex(entity => new { entity.DoctorId, entity.AppointmentDate, entity.StartTime })
            .HasDatabaseName("ix_appointments_doctor_id_appointment_date_start_time");

        builder.HasIndex(entity => new { entity.PatientId, entity.AppointmentDate })
            .HasDatabaseName("ix_appointments_patient_id_appointment_date");
    }
}
