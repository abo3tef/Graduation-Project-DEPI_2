using Medilog.Domain.Entities;
using Medilog.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

internal sealed class MedicalDocumentConfiguration : BaseEntityConfiguration<MedicalDocument>
{
    public override void Configure(EntityTypeBuilder<MedicalDocument> builder)
    {
        base.Configure(builder);

        builder.ToTable("medical_documents", table =>
            table.HasCheckConstraint(
                "ck_medical_documents_type",
                "`type` IN ('PDF', 'JPG', 'JPEG', 'PNG')"));

        builder.Property(entity => entity.PatientId)
            .HasColumnName("patient_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(entity => entity.UploadedBy)
            .HasColumnName("uploaded_by")
            .HasColumnType("char(36)");

        builder.Property(entity => entity.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(ColumnLengths.FileName)
            .IsRequired();

        builder.Property(entity => entity.Type)
            .HasColumnName("type")
            .HasMaxLength(ColumnLengths.EnumLabel)
            .HasConversion(EnumValueConverters.MedicalDocumentTypeConverter)
            .IsRequired();

        builder.Property(entity => entity.Location)
            .HasColumnName("location")
            .HasMaxLength(ColumnLengths.Location)
            .IsRequired();

        builder.Property(entity => entity.UploadDate)
            .HasColumnName("upload_date")
            .HasColumnType("datetime(6)")
            .HasConversion(ValueConverters.UtcDateTimeOffsetConverter)
            .IsRequired();

        builder.Property(entity => entity.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.HasOne(entity => entity.Patient)
            .WithMany(patient => patient.MedicalDocuments)
            .HasForeignKey(entity => entity.PatientId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_medical_documents_patient");

        builder.HasOne(entity => entity.UploadedByDoctor)
            .WithMany(doctor => doctor.MedicalDocuments)
            .HasForeignKey(entity => entity.UploadedBy)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_medical_documents_doctor");

        builder.HasIndex(entity => new { entity.PatientId, entity.UploadDate })
            .HasDatabaseName("ix_medical_documents_patient_id_upload_date");
    }
}
