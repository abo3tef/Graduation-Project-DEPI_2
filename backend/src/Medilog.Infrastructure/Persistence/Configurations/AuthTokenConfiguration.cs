using Medilog.Domain.Entities;
using Medilog.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps one-time email-verification and password-reset tokens (decision D-33).
/// </summary>
/// <remarks>
/// There is no foreign key: the owner may be a doctor, a secretary or an admin, and each account
/// kind has its own table. The <c>(user_id, user_type, purpose)</c> index is used to invalidate a
/// previous token when a new one is issued; the unique index on <c>token_hash</c> is what lookup
/// relies on, and it also makes a duplicate hash impossible. The raw token is never stored.
/// </remarks>
internal sealed class AuthTokenConfiguration : CreatedEntityConfiguration<AuthToken>
{
    public override void Configure(EntityTypeBuilder<AuthToken> builder)
    {
        base.Configure(builder);

        builder.ToTable("auth_tokens", table =>
        {
            table.HasCheckConstraint(
                "ck_auth_tokens_user_type",
                "`user_type` IN ('Doctor', 'Secretary', 'Admin')");
            table.HasCheckConstraint(
                "ck_auth_tokens_purpose",
                "`purpose` IN ('EmailVerification', 'PasswordReset')");
        });

        builder.Property(entity => entity.UserId)
            .HasColumnName("user_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(entity => entity.UserType)
            .HasColumnName("user_type")
            .HasMaxLength(ColumnLengths.EnumLabel)
            .HasConversion(EnumValueConverters.UserTypeConverter)
            .IsRequired();

        builder.Property(entity => entity.Purpose)
            .HasColumnName("purpose")
            .HasMaxLength(ColumnLengths.EnumLabel)
            .HasConversion(EnumValueConverters.AuthTokenPurposeConverter)
            .IsRequired();

        builder.Property(entity => entity.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(ColumnLengths.TokenHash)
            .IsRequired();

        builder.Property(entity => entity.ExpiresAtUtc)
            .HasColumnName("expires_at_utc")
            .HasColumnType("datetime(6)")
            .HasConversion(ValueConverters.UtcDateTimeOffsetConverter)
            .IsRequired();

        builder.Property(entity => entity.ConsumedAtUtc)
            .HasColumnName("consumed_at_utc")
            .HasColumnType("datetime(6)")
            .HasConversion(ValueConverters.NullableUtcDateTimeOffsetConverter);

        builder.HasIndex(entity => entity.TokenHash)
            .IsUnique()
            .HasDatabaseName("uq_auth_tokens_token_hash");

        builder.HasIndex(entity => new { entity.UserId, entity.UserType, entity.Purpose })
            .HasDatabaseName("ix_auth_tokens_user_purpose");
    }
}
