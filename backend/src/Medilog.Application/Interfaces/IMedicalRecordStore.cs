using Medilog.Application.Common.Paging;
using Medilog.Domain.Enums;

namespace Medilog.Application.Interfaces;

/// <summary>Minimal facts about an acting doctor (F3 is doctor-only).</summary>
public sealed record MedicalDoctorInfo(Guid Id, bool IsActive);

/// <summary>The patient's owning doctor, used for the workspace scope check (BRULE-03/04).</summary>
public sealed record PatientOwnerInfo(Guid PatientId, Guid? DoctorId);

/// <summary>
/// One allergy of a patient, projected from the store. Collections are returned
/// unsorted; the service applies the deterministic response ordering because enum
/// values are stored as their string labels and cannot be ranked in SQL.
/// </summary>
public sealed record PatientAllergyData(
    Guid Id,
    string Name,
    AllergySeverity? Severity,
    string? Description,
    string? Notes);

/// <summary>One chronic condition of a patient (unsorted; see <see cref="PatientAllergyData"/>).</summary>
public sealed record PatientConditionData(
    Guid Id,
    string Name,
    DateOnly? DiagnosisDate,
    ChronicConditionStatus? Status,
    string? Notes);

/// <summary>
/// Everything the medical profile needs for one patient. Carries only Class
/// Specification §4.2 Patient attributes plus the allergy/condition collections;
/// never any visit, document or timeline data.
/// </summary>
public sealed record PatientProfileData(
    Guid PatientId,
    string FullName,
    DateOnly DateOfBirth,
    Gender Gender,
    string? Phone,
    string? Address,
    BloodType? BloodType,
    string? EmergencyContact,
    IReadOnlyList<PatientAllergyData> Allergies,
    IReadOnlyList<PatientConditionData> ChronicConditions);

/// <summary>A diagnosis linked to one visit (keyed for grouping by the store).</summary>
public sealed record VisitDiagnosisData(
    Guid VisitId,
    Guid Id,
    string Name,
    string? Description,
    DateOnly DiagnosisDate);

/// <summary>A prescribed medication line linked to one visit.</summary>
public sealed record VisitMedicationData(
    Guid VisitId,
    Guid Id,
    string Name,
    string Dosage,
    string Frequency,
    string? Duration,
    string? Instructions);

/// <summary>
/// Vital signs of one visit. The table has no patient column; the rows are
/// scoped by the visit ids of the patient's visits only.
/// </summary>
public sealed record VisitVitalSignsData(
    Guid VisitId,
    Guid Id,
    string? BloodPressure,
    int? HeartRate,
    decimal? Temperature,
    int? RespiratoryRate,
    decimal? Weight,
    decimal? Height,
    int? SpO2);

/// <summary>A visit-linked clinical note with its authoring doctor (unlinked notes are out of F3-C1 scope).</summary>
public sealed record VisitNoteData(
    Guid VisitId,
    Guid Id,
    Guid DoctorId,
    string DoctorName,
    string Content,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

/// <summary>A visit-linked laboratory result (unlinked results are out of F3-C1 scope).</summary>
public sealed record VisitLabResultData(
    Guid VisitId,
    Guid Id,
    string TestName,
    DateOnly TestDate,
    decimal Value,
    string Unit,
    string? ReferenceRange,
    string? Notes);

/// <summary>One visit of a patient with its linked clinical records (children unsorted; see D-13).</summary>
public sealed record MedicalVisitData(
    Guid VisitId,
    DateTimeOffset VisitDate,
    Guid DoctorId,
    string DoctorName,
    string? ChiefComplaint,
    string? Symptoms,
    string? ClinicalNotes,
    IReadOnlyList<VisitDiagnosisData> Diagnoses,
    IReadOnlyList<VisitMedicationData> Medications,
    IReadOnlyList<VisitVitalSignsData> VitalSigns,
    IReadOnlyList<VisitNoteData> Notes,
    IReadOnlyList<VisitLabResultData> LabResults);

/// <summary>
/// Persistence port for the F3 medical-record read use cases. Implemented in
/// Infrastructure so the Application layer stays free of EF Core. Read-only by
/// design: F3-C1 never writes, so the port has no save/transaction members.
/// </summary>
public interface IMedicalRecordStore
{
    /// <summary>Active status of a doctor, for the identity check.</summary>
    Task<MedicalDoctorInfo?> GetDoctorAsync(Guid doctorId, CancellationToken cancellationToken);

    /// <summary>Returns only the patient's owning doctor id, without loading the patient graph.</summary>
    Task<PatientOwnerInfo?> GetPatientOwnerAsync(Guid patientId, CancellationToken cancellationToken);

    /// <summary>
    /// The profile read model of one patient (allergies and conditions unsorted),
    /// or null when the patient does not exist.
    /// </summary>
    Task<PatientProfileData?> GetProfileDataAsync(Guid patientId, CancellationToken cancellationToken);

    /// <summary>
    /// One page of the patient's visits, newest first with the id as tie-breaker.
    /// Children are unsorted and must be ordered by the service.
    /// </summary>
    Task<PagedResult<MedicalVisitData>> ListVisitsAsync(
        Guid patientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
}
