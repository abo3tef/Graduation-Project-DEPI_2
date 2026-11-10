using Medilog.Domain.Common;

namespace Medilog.Domain.Entities;

/// <summary>
/// A diagnosis recorded during a medical visit. One visit can carry several diagnoses.
/// </summary>
/// <remarks>
/// <para>
/// The patient is referenced by identifier only, like <see cref="MedicalVisit"/>, because the
/// patient aggregate is owned by another feature (F4-C1 decision D-02).
/// </para>
/// <para>
/// <see cref="AuditableEntity.CreatedAtUtc"/> is initialised on construction so an insert can
/// never carry a default <see cref="DateTimeOffset"/> value. The audit interceptor planned for
/// F4-C2 becomes authoritative on save and maintains
/// <see cref="AuditableEntity.UpdatedAtUtc"/>.
/// </para>
/// </remarks>
public class Diagnosis : AuditableEntity
{
    public Diagnosis()
    {
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Identifier of the visit this diagnosis belongs to.</summary>
    public Guid MedicalVisitId { get; set; }

    /// <summary>Identifier of the patient the diagnosis is recorded for.</summary>
    public Guid PatientId { get; set; }

    /// <summary>Name of the diagnosis.</summary>
    public string DiagnosisName { get; set; } = string.Empty;

    /// <summary>Additional clinical description, if any.</summary>
    public string? Description { get; set; }

    /// <summary>Date and time the diagnosis was made, stored in UTC.</summary>
    public DateTimeOffset DiagnosisDateUtc { get; set; }
}
