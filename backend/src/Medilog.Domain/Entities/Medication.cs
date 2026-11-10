using Medilog.Domain.Common;

namespace Medilog.Domain.Entities;

/// <summary>
/// A medication prescribed during a medical visit (a prescription item).
/// </summary>
public sealed class Medication : BaseEntity
{
    public Guid VisitId { get; set; }

    public MedicalVisit Visit { get; set; } = null!;

    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public string MedicationName { get; set; } = string.Empty;

    public string Dosage { get; set; } = string.Empty;

    public string Frequency { get; set; } = string.Empty;

    public string? Duration { get; set; }

    public string? Instructions { get; set; }
}
