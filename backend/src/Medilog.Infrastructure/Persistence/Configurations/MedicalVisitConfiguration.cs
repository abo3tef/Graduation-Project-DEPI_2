using Medilog.Domain.Entities;
using Medilog.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

internal sealed class MedicalVisitConfiguration : BaseEntityConfiguration<MedicalVisit>
{
    public override void Configure(EntityTypeBuilder<MedicalVisit> builder)
    {
        base.Configure(builder);

        builder.ToTable("medical_visits");

        builder.HasAlternateKey(entity => new { entity.Id, entity.PatientId })
            .HasName("ak_medical_visits_id_patient_id");

        builder.Property(entity => entity.PatientId)
            .HasColumnName("patient_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(entity => entity.DoctorId)
            .HasColumnName("doctor_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(entity => entity.AppointmentId)
            .HasColumnName("appointment_id")
            .HasColumnType("char(36)");

        builder.Property(entity => entity.VisitDate)
            .HasColumnName("visit_date")
            .HasColumnType("datetime(6)")
            .HasConversion(ValueConverters.UtcDateTimeOffsetConverter)
            .IsRequired();

        builder.Property(entity => entity.ChiefComplaint)
            .HasColumnName("chief_complaint")
            .HasColumnType("text");

        builder.Property(entity => entity.Symptoms)
            .HasColumnName("symptoms")
            .HasColumnType("text");

        builder.Property(entity => entity.ClinicalNotes)
            .HasColumnName("clinical_notes")
            .HasColumnType("text");

        builder.HasOne(entity => entity.Patient)
            .WithMany(patient => patient.MedicalVisits)
            .HasForeignKey(entity => entity.PatientId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_medical_visits_patient");

        builder.HasOne(entity => entity.Doctor)
            .WithMany(doctor => doctor.MedicalVisits)
            .HasForeignKey(entity => entity.DoctorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_medical_visits_doctor");

        builder.HasOne(entity => entity.Appointment)
            .WithOne()
            .HasForeignKey<MedicalVisit>(entity => new { entity.AppointmentId, entity.PatientId, entity.DoctorId })
            .HasPrincipalKey<Appointment>(appointment => new { appointment.Id, appointment.PatientId, appointment.DoctorId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_medical_visits_appointment");

        builder.HasIndex(entity => entity.AppointmentId)
            .IsUnique()
            .HasDatabaseName("uq_medical_visits_appointment_id");

        builder.HasIndex(entity => new { entity.PatientId, entity.VisitDate })
            .HasDatabaseName("ix_medical_visits_patient_id_visit_date");
    }
}
