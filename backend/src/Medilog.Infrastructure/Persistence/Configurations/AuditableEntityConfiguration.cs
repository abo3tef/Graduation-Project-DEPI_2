using Medilog.Domain.Common;
using Medilog.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the update timestamp shared by entities that inherit
/// <see cref="AuditableEntity"/>.
/// </summary>
internal abstract class AuditableEntityConfiguration<TEntity> : CreatedEntityConfiguration<TEntity>
    where TEntity : AuditableEntity
{
    public override void Configure(EntityTypeBuilder<TEntity> builder)
    {
        base.Configure(builder);

        builder.Property(entity => entity.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("datetime(6)")
            .HasConversion(ValueConverters.NullableUtcDateTimeOffsetConverter);
    }
}
