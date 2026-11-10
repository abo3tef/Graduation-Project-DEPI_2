using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Medilog.Infrastructure.Persistence.Converters;

/// <summary>
/// Provider-facing value conversions for the date/time types used by the
/// domain model. The Oracle MySQL EF Core provider does not materialize
/// <see cref="DateOnly"/> and <see cref="TimeOnly"/> through every reader path,
/// so these converters deliberately expose <see cref="DateTime"/> and
/// <see cref="TimeSpan"/> to the provider.
/// </summary>
internal static class ValueConverters
{
    /// <summary>
    /// Writes a <see cref="DateTimeOffset"/> as its UTC instant and reads it
    /// back with a zero offset. A non-zero source offset is normalized, not
    /// silently discarded.
    /// </summary>
    public static readonly ValueConverter<DateTimeOffset, DateTime> UtcDateTimeOffsetConverter = new(
        value => value.UtcDateTime,
        value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));

    public static readonly ValueConverter<DateTimeOffset?, DateTime?> NullableUtcDateTimeOffsetConverter = new(
        value => value.HasValue ? value.Value.UtcDateTime : null,
        value => value.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc))
            : null);

    public static readonly ValueConverter<DateOnly, DateTime> DateOnlyConverter = new(
        value => value.ToDateTime(TimeOnly.MinValue),
        value => DateOnly.FromDateTime(value));

    public static readonly ValueConverter<DateOnly?, DateTime?> NullableDateOnlyConverter = new(
        value => value.HasValue ? value.Value.ToDateTime(TimeOnly.MinValue) : null,
        value => value.HasValue ? DateOnly.FromDateTime(value.Value) : null);

    public static readonly ValueConverter<TimeOnly, TimeSpan> TimeOnlyConverter = new(
        value => value.ToTimeSpan(),
        value => TimeOnly.FromTimeSpan(value));

    public static readonly ValueConverter<TimeOnly?, TimeSpan?> NullableTimeOnlyConverter = new(
        value => value.HasValue ? value.Value.ToTimeSpan() : null,
        value => value.HasValue ? TimeOnly.FromTimeSpan(value.Value) : null);
}
