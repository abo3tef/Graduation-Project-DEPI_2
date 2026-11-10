using Medilog.Domain.Common;
using Medilog.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the creation timestamp shared by entities that inherit
/// <see cref="CreatedEntity"/>.
/// </summary>
internal abstract class CreatedEntityConfiguration<TEntity> : BaseEntityConfiguration<TEntity>
    where TEntity : CreatedEntity
{
    public override void Configure(EntityTypeBuilder<TEntity> builder)
    {
        base.Configure(builder);

        builder.Property(entity => entity.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("datetime(6)")
            .HasConversion(ValueConverters.UtcDateTimeOffsetConverter)
            .IsRequired();
    }
}
