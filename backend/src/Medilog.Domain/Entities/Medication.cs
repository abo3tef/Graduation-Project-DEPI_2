using Medilog.Domain.Common;

namespace Medilog.Domain.Entities;

/// <summary>
/// A medication prescribed during a medical visit.
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
public class Medication : AuditableEntity
{
    public Medication()
    {
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Identifier of the visit this medication was prescribed during.</summary>
    public Guid MedicalVisitId { get; set; }

    /// <summary>Identifier of the patient the medication is prescribed for.</summary>
    public Guid PatientId { get; set; }

    /// <summary>Name of the prescribed medication.</summary>
    public string MedicationName { get; set; } = string.Empty;

    /// <summary>Dosage as free text, for example "500 mg".</summary>
    public string? Dosage { get; set; }

    /// <summary>Intake frequency as free text, for example "twice daily".</summary>
    public string? Frequency { get; set; }

    /// <summary>Duration of the treatment in days, when specified.</summary>
    public int? DurationInDays { get; set; }

    /// <summary>Additional instructions for the patient, if any.</summary>
    public string? Instructions { get; set; }
}
