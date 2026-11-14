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
/// <summary>
/// One patient-level clinical note, linked or unlinked (Class Spec §4.10).
/// <see cref="VisitId"/> is null when the note is not attached to a visit.
/// </summary>
public sealed record PatientNoteData(
    Guid Id,
    Guid? VisitId,
    Guid DoctorId,
    string DoctorName,
    string NoteContent,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

/// <summary>
/// One patient-level laboratory result, linked or unlinked (Class Spec §4.9).
/// <see cref="VisitId"/> is null when the result is not attached to a visit.
/// </summary>
public sealed record PatientLabResultData(
    Guid Id,
    Guid? VisitId,
    string TestName,
    DateOnly TestDate,
    decimal Value,
    string Unit,
    string? ReferenceRange,
    string? Notes);

/// <summary>
/// The timeline event discriminators (F3-C3b Q-12). The values are part of the
/// API contract (`eventType` in §3.5) and also serve as the deterministic
/// cross-source tie-breaker, compared with the ordinal string order:
/// document &lt; lab_result &lt; note &lt; visit.
/// </summary>
public static class TimelineEventTypes
{
    public const string Visit = "visit";
    public const string Note = "note";
    public const string LabResult = "lab_result";
    public const string Document = "document";
}

/// <summary>
/// One event of the patient's medical timeline (Class Spec §4.2 getTimeline;
/// F3-C3b Q-12/D-T1…D-T3). A flat projection of exactly one source — the fields
/// not belonging to <see cref="EventType"/> carry their zero values and are
/// mapped to the typed response payload by the service.
/// </summary>
/// <remarks>
/// For lab results <see cref="OccurredAtUtc"/> is midnight UTC of the test date
/// (the date column carries no time); this convention is documented in the API
/// contract and is deterministic because the tie-breakers cover equal instants.
/// </remarks>
public sealed record PatientTimelineEventData(
    Guid Id,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    Guid? VisitId,
    Guid? DoctorId,
    string? DoctorName,
    // Visit payload (event type "visit" only).
    string? ChiefComplaint,
    int DiagnosesCount,
    int MedicationsCount,
    int NotesCount,
    int LabResultsCount,
    // Note payload (event type "note" only).
    string? NoteContent,
    // Lab payload (event type "lab_result" only).
    string? TestName,
    decimal? TestValue,
    string? TestUnit,
    // Document payload (event type "document" only). Deliberately no location.
    string? FileName,
    MedicalDocumentType? FileType,
    string? DocumentDescription);

/// <summary>
/// The metadata of one medical document of the patient (Class Spec §4.11;
/// F3-C3b Q-13). The read model deliberately has **no** location field: the
/// storage path/URL of <c>medical_documents.location</c> must never leave the
/// store (R-3 — enforced by shape, not by serialization filtering).
/// </summary>
public sealed record PatientDocumentData(
    Guid Id,
    string FileName,
    MedicalDocumentType Type,
    DateTimeOffset UploadDateUtc,
    string? Description,
    Guid? UploadedByDoctorId,
    string? UploadedByDoctorName);

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

    /// <summary>
    /// Every clinical note of the patient — visit-linked and unlinked — newest
    /// first (F3-C3a C-2); the ordering is applied here because a flat list can
    /// be ordered in SQL (unlike the visit children with string-stored enums).
    /// </summary>
    Task<PagedResult<PatientNoteData>> ListNotesAsync(
        Guid patientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every laboratory result of the patient — visit-linked and unlinked —
    /// newest test first (F3-C3a C-2).
    /// </summary>
    Task<PagedResult<PatientLabResultData>> ListLabResultsAsync(
        Guid patientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// One page of the patient's medical timeline (F3-C3b Q-12): visits, notes,
    /// lab results and document metadata in one feed, newest first
    /// (<see cref="PatientTimelineEventData.OccurredAtUtc"/>) with the event
    /// type and id as tie-breakers.
    /// </summary>
    Task<PagedResult<PatientTimelineEventData>> ListTimelineAsync(
        Guid patientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// One page of the patient's medical document metadata (F3-C3b Q-13),
    /// newest upload first. The read models carry no storage location.
    /// </summary>
    Task<PagedResult<PatientDocumentData>> ListDocumentsAsync(
        Guid patientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
}
