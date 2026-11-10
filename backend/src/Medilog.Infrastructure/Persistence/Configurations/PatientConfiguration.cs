using Medilog.Domain.Entities;
using Medilog.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

internal sealed class PatientConfiguration : CreatedEntityConfiguration<Patient>
{
    public override void Configure(EntityTypeBuilder<Patient> builder)
    {
        base.Configure(builder);

        builder.ToTable("patients", table =>
        {
            table.HasCheckConstraint(
                "ck_patients_gender",
                "`gender` IN ('Male', 'Female')");
            table.HasCheckConstraint(
                "ck_patients_blood_type",
                "`blood_type` IS NULL OR `blood_type` IN ('A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-')");
        });

        builder.Property(entity => entity.DoctorId)
            .HasColumnName("doctor_id")
            .HasColumnType("char(36)");

        builder.Property(entity => entity.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(ColumnLengths.Name)
            .IsRequired();

        builder.Property(entity => entity.DateOfBirth)
            .HasColumnName("date_of_birth")
            .HasColumnType("date")
            .HasConversion(ValueConverters.DateOnlyConverter)
            .IsRequired();

        builder.Property(entity => entity.Gender)
            .HasColumnName("gender")
            .HasMaxLength(ColumnLengths.EnumLabel)
            .HasConversion(EnumValueConverters.GenderConverter)
            .IsRequired();

        builder.Property(entity => entity.Phone)
            .HasColumnName("phone")
            .HasMaxLength(ColumnLengths.Phone);

        builder.Property(entity => entity.Address)
            .HasColumnName("address")
            .HasMaxLength(ColumnLengths.Address);

        builder.Property(entity => entity.BloodType)
            .HasColumnName("blood_type")
            .HasMaxLength(ColumnLengths.EnumLabel)
            .HasConversion(EnumValueConverters.BloodTypeConverter);

        builder.Property(entity => entity.EmergencyContact)
            .HasColumnName("emergency_contact")
            .HasMaxLength(ColumnLengths.EmergencyContact);

        builder.HasOne(entity => entity.Doctor)
            .WithMany(doctor => doctor.Patients)
            .HasForeignKey(entity => entity.DoctorId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_patients_doctor");

        builder.HasIndex(entity => new { entity.DoctorId, entity.FullName })
            .HasDatabaseName("ix_patients_doctor_id_full_name");

        builder.HasIndex(entity => entity.Phone)
            .HasDatabaseName("ix_patients_phone");
    }
}
