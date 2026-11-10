namespace Medilog.Domain.Common;

/// <summary>
/// Base type for entities that keep track of when they were created and last updated.
/// Timestamp persistence is implemented by the Infrastructure layer.
/// </summary>
public abstract class AuditableEntity : CreatedEntity
{
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
