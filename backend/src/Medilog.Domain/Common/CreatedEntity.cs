namespace Medilog.Domain.Common;

/// <summary>
/// Base type for entities that keep track of when they were created.
/// The timestamp is stored as a UTC instant by the Infrastructure layer.
/// </summary>
public abstract class CreatedEntity : BaseEntity
{
    public DateTimeOffset CreatedAtUtc { get; set; }
}
