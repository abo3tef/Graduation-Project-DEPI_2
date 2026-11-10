using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Migrations;

/// <summary>
/// Compatibility shim for MySql.EntityFrameworkCore 10.0.12.
/// </summary>
/// <remarks>
/// The provider's design-time code generator emits
/// <c>MySQLEntityTypeBuilderExtensions.HasCharSet(builder, "...")</c> inside
/// generated model snapshots and migration designers, but the provider type
/// with that name is not public and its namespace is not imported. Keeping this
/// shim in the migrations namespace makes every generated file compile without
/// hand-editing auto-generated code.
///
/// The shim only writes the same <c>MySQL:Charset</c> annotation that the
/// provider reads when it generates table DDL and when it diffs a future
/// migration against the snapshot.
/// </remarks>
internal static class MySQLEntityTypeBuilderExtensions
{
    public static EntityTypeBuilder HasCharSet(EntityTypeBuilder builder, string charSet)
    {
        builder.Metadata.SetAnnotation("MySQL:Charset", charSet);
        return builder;
    }
}
