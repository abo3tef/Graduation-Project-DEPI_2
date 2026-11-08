namespace Medilog.Domain.Common;

/// <summary>
/// Base type for all domain entities. Every entity is identified by a
/// globally unique identifier assigned when the entity is created.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
