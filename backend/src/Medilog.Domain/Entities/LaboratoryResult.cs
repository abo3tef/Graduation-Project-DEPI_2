using Medilog.Domain.Common;

namespace Medilog.Domain.Entities;

/// <summary>
/// A laboratory test result belonging to a patient.
/// </summary>
public sealed class LaboratoryResult : BaseEntity
{
    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    /// <summary>
    /// Optional link to the visit that produced the result.
    /// </summary>
    public Guid? VisitId { get; set; }

    public MedicalVisit? Visit { get; set; }

    public string TestName { get; set; } = string.Empty;

    public DateOnly TestDate { get; set; }

    public decimal Value { get; set; }

    public string Unit { get; set; } = string.Empty;

    public string? ReferenceRange { get; set; }

    public string? Notes { get; set; }
}
