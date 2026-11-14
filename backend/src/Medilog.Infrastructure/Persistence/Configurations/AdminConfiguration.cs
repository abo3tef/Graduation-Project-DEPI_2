using Medilog.Domain.Entities;
using Medilog.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the administrator account to the <c>admins</c> table (decision D-32).
/// </summary>
/// <remarks>
/// The shape deliberately mirrors <c>doctors</c>/<c>secretaries</c>: one account table per role,
/// with the same credentials, status and email-verification columns. The unique email index is the
/// per-table half of the "one account per email" rule (D-43); the cross-table check is enforced by
/// the authentication service because the three tables cannot share a unique constraint.
/// </remarks>
internal sealed class AdminConfiguration : CreatedEntityConfiguration<Admin>
{
    public override void Configure(EntityTypeBuilder<Admin> builder)
    {
        base.Configure(builder);

        builder.ToTable("admins", table =>
            table.HasCheckConstraint(
                "ck_admins_account_status",
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

        builder.Property(entity => entity.AccountStatus)
            .HasColumnName("account_status")
            .HasMaxLength(ColumnLengths.EnumLabel)
            .HasConversion(EnumValueConverters.AccountStatusConverter)
            .IsRequired();

        builder.Property(entity => entity.EmailVerifiedAtUtc)
            .HasColumnName("email_verified_at_utc")
            .HasColumnType("datetime(6)")
            .HasConversion(ValueConverters.NullableUtcDateTimeOffsetConverter);

        builder.HasIndex(entity => entity.Email)
            .IsUnique()
            .HasDatabaseName("uq_admins_email");
    }
}
