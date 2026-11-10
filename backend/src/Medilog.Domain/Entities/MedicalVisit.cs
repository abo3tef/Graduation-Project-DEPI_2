using Medilog.Domain.Common;

namespace Medilog.Domain.Entities;

/// <summary>
/// A medical visit recorded by a doctor for a patient.
/// </summary>
/// <remarks>
/// <para>
/// The patient and the doctor are referenced by identifier only. Those aggregates are owned by
/// other features, so no navigation properties or foreign keys are declared here
/// (F4-C1 decision D-02).
/// </para>
/// <para>
/// <see cref="AuditableEntity.CreatedAtUtc"/> is initialised on construction so an insert can
/// never carry a default <see cref="DateTimeOffset"/> value. The audit interceptor planned for
/// F4-C2 becomes authoritative on save and maintains
/// <see cref="AuditableEntity.UpdatedAtUtc"/>.
/// </para>
/// </remarks>
public class MedicalVisit : AuditableEntity
{
    public MedicalVisit()
    {
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Identifier of the patient this visit belongs to.</summary>
    public Guid PatientId { get; set; }

    /// <summary>Identifier of the doctor who recorded the visit.</summary>
    public Guid DoctorId { get; set; }

    /// <summary>Date and time of the visit, stored in UTC.</summary>
    public DateTimeOffset VisitDateUtc { get; set; }

    /// <summary>The patient's reported reason for the visit, if provided.</summary>
    public string? ChiefComplaint { get; set; }

    /// <summary>Free-text clinical notes recorded during the visit, if any.</summary>
    public string? ClinicalNotes { get; set; }
}
