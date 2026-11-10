using Medilog.Domain.Common;

namespace Medilog.Domain.Entities;

/// <summary>
/// A diagnosis recorded during a medical visit.
/// </summary>
public sealed class Diagnosis : BaseEntity
{
    public Guid VisitId { get; set; }

    public MedicalVisit Visit { get; set; } = null!;

    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public string DiagnosisName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly DiagnosisDate { get; set; }
}
