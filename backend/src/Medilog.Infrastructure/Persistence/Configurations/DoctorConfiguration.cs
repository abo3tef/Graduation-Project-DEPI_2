using Medilog.Domain.Entities;
using Medilog.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

internal sealed class DoctorConfiguration : CreatedEntityConfiguration<Doctor>
{
    public override void Configure(EntityTypeBuilder<Doctor> builder)
    {
        base.Configure(builder);

        builder.ToTable("doctors", table =>
            table.HasCheckConstraint(
                "ck_doctors_account_status",
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

        builder.Property(entity => entity.Specialization)
            .HasColumnName("specialization")
            .HasMaxLength(ColumnLengths.Specialization);

        builder.Property(entity => entity.AccountStatus)
            .HasColumnName("account_status")
            .HasMaxLength(ColumnLengths.EnumLabel)
            .HasConversion(EnumValueConverters.AccountStatusConverter)
            .IsRequired();

        builder.HasIndex(entity => entity.Email)
            .IsUnique()
            .HasDatabaseName("uq_doctors_email");
    }
}
