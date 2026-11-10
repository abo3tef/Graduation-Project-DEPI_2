using Medilog.Domain.Entities;
using Medilog.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

internal sealed class SecretaryConfiguration : CreatedEntityConfiguration<Secretary>
{
    public override void Configure(EntityTypeBuilder<Secretary> builder)
    {
        base.Configure(builder);

        builder.ToTable("secretaries", table =>
            table.HasCheckConstraint(
                "ck_secretaries_account_status",
                "`account_status` IN ('Active', 'Inactive')"));

        builder.Property(entity => entity.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(ColumnLengths.Name)
            .IsRequired();

        builder.Property(entity => entity.Email)
            .HasColumnName("email")
            .HasMaxLength(ColumnLengths.Email)
            .IsRequired();

        builder.Property(entity => entity.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(ColumnLengths.PasswordHash)
            .IsRequired();

        builder.Property(entity => entity.Phone)
            .HasColumnName("phone")
            .HasMaxLength(ColumnLengths.Phone);

        builder.Property(entity => entity.AccountStatus)
            .HasColumnName("account_status")
            .HasMaxLength(ColumnLengths.EnumLabel)
            .HasConversion(EnumValueConverters.AccountStatusConverter)
            .IsRequired();

        builder.HasIndex(entity => entity.Email)
            .IsUnique()
            .HasDatabaseName("uq_secretaries_email");

        builder.HasMany(secretary => secretary.Doctors)
            .WithMany(doctor => doctor.Secretaries)
            .UsingEntity<Dictionary<string, object>>(
                "doctor_secretaries",
                join => join.HasOne<Doctor>()
                    .WithMany()
                    .HasForeignKey("doctor_id")
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_doctor_secretaries_doctor"),
                join => join.HasOne<Secretary>()
                    .WithMany()
                    .HasForeignKey("secretary_id")
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_doctor_secretaries_secretary"),
                join =>
                {
                    join.ToTable("doctor_secretaries");
                    join.HasKey("doctor_id", "secretary_id");
                    join.HasIndex("secretary_id")
                        .HasDatabaseName("ix_doctor_secretaries_secretary_id");
                });
    }
}
