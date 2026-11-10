using Medilog.Domain.Common;
using Medilog.Domain.Enums;

namespace Medilog.Domain.Entities;

/// <summary>
/// A known allergy belonging to a patient.
/// </summary>
public sealed class Allergy : BaseEntity
{
    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public string AllergyName { get; set; } = string.Empty;

    public AllergySeverity? Severity { get; set; }

    public string? Description { get; set; }

    public string? Notes { get; set; }
}
