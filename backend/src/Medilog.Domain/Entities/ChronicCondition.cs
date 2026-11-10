using Medilog.Domain.Common;
using Medilog.Domain.Enums;

namespace Medilog.Domain.Entities;

/// <summary>
/// A long-term condition belonging to a patient.
/// </summary>
public sealed class ChronicCondition : BaseEntity
{
    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public string ConditionName { get; set; } = string.Empty;

    public DateOnly? DiagnosisDate { get; set; }

    public ChronicConditionStatus? Status { get; set; }

    public string? Notes { get; set; }
}
