using Medilog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

internal sealed class MedicalNoteConfiguration : AuditableEntityConfiguration<MedicalNote>
{
    public override void Configure(EntityTypeBuilder<MedicalNote> builder)
    {
        base.Configure(builder);

        builder.ToTable("medical_notes");

        builder.Property(entity => entity.PatientId)
            .HasColumnName("patient_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(entity => entity.DoctorId)
            .HasColumnName("doctor_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(entity => entity.VisitId)
            .HasColumnName("visit_id")
            .HasColumnType("char(36)");

        builder.Property(entity => entity.NoteContent)
            .HasColumnName("note_content")
            .HasColumnType("text")
            .IsRequired();

        builder.HasOne(entity => entity.Patient)
            .WithMany(patient => patient.MedicalNotes)
            .HasForeignKey(entity => entity.PatientId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_medical_notes_patient");

        builder.HasOne(entity => entity.Doctor)
            .WithMany(doctor => doctor.MedicalNotes)
            .HasForeignKey(entity => entity.DoctorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_medical_notes_doctor");

        builder.HasOne(entity => entity.Visit)
            .WithMany(visit => visit.MedicalNotes)
            .HasForeignKey(entity => new { entity.VisitId, entity.PatientId })
            .HasPrincipalKey(visit => new { visit.Id, visit.PatientId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_medical_notes_visit");
    }
}
