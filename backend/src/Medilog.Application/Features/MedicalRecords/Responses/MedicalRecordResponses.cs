using Medilog.Domain.Enums;

namespace Medilog.Application.Features.MedicalRecords.Responses;

/// <summary>
/// One allergy in the medical profile (Class Spec §4.6; FR-23).
/// </summary>
public sealed record PatientAllergyResponse(
    Guid Id,
    string Name,
    AllergySeverity? Severity,
    string? Description,
    string? Notes);

/// <summary>One chronic condition in the medical profile (Class Spec §4.7).</summary>
public sealed record PatientChronicConditionResponse(
    Guid Id,
    string Name,
    DateOnly? DiagnosisDate,
    ChronicConditionStatus? Status,
    string? Notes);

/// <summary>
/// The doctor's view of one patient's medical profile. Carries every Class Spec
/// §4.2 Patient attribute (including <c>BloodType</c> and
/// <c>EmergencyContact</c>) and never any document, timeline or visit data.
/// </summary>
public sealed record PatientMedicalProfileResponse(
    Guid PatientId,
    string FullName,
    DateOnly DateOfBirth,
    int Age,
    Gender Gender,
    string? Phone,
    string? Address,
    BloodType? BloodType,
    string? EmergencyContact,
    IReadOnlyList<PatientAllergyResponse> Allergies,
    IReadOnlyList<PatientChronicConditionResponse> ChronicConditions);

/// <summary>A diagnosis recorded during a visit (Class Spec §4.4).</summary>
public sealed record VisitDiagnosisResponse(
    Guid Id,
    string Name,
    string? Description,
    DateOnly DiagnosisDate);

/// <summary>A prescribed medication line; the visit's prescription is its collection (Class Spec §4.5).</summary>
public sealed record VisitMedicationResponse(
    Guid Id,
    string Name,
    string Dosage,
    string Frequency,
    string? Duration,
    string? Instructions);

/// <summary>Measurements taken during a visit (Class Spec §4.8); all nullable.</summary>
public sealed record VisitVitalSignsResponse(
    Guid Id,
    string? BloodPressure,
    int? HeartRate,
    decimal? Temperature,
    int? RespiratoryRate,
    decimal? Weight,
    decimal? Height,
    int? SpO2);

/// <summary>A visit-linked clinical note with its authoring doctor (Class Spec §4.10).</summary>
public sealed record VisitNoteResponse(
    Guid Id,
    Guid DoctorId,
    string DoctorName,
    string Content,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

/// <summary>A visit-linked laboratory result (Class Spec §4.9).</summary>
public sealed record VisitLabResultResponse(
    Guid Id,
    string TestName,
    DateOnly TestDate,
    decimal Value,
    string Unit,
    string? ReferenceRange,
    string? Notes);

/// <summary>
/// One patient-level clinical note, linked or unlinked (Class Spec §4.10;
/// F3-C3a). <see cref="VisitId"/> is null for an unlinked note.
/// </summary>
public sealed record PatientNoteResponse(
    Guid Id,
    Guid? VisitId,
    Guid DoctorId,
    string DoctorName,
    string NoteContent,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

/// <summary>
/// One patient-level laboratory result, linked or unlinked (Class Spec §4.9;
/// F3-C3a). <see cref="VisitId"/> is null for an unlinked result.
/// </summary>
public sealed record PatientLabResultResponse(
    Guid Id,
    Guid? VisitId,
    string TestName,
    DateOnly TestDate,
    decimal Value,
    string Unit,
    string? ReferenceRange,
    string? Notes);

/// <summary>Summary payload of a timeline visit event (F3-C3b D-T1): header fields and child counts, never the full visit.</summary>
public sealed record PatientTimelineVisitPayloadResponse(
    string DoctorName,
    string? ChiefComplaint,
    int DiagnosesCount,
    int MedicationsCount,
    int NotesCount,
    int LabResultsCount);

/// <summary>Payload of a timeline note event (F3-C3b D-T2: full content).</summary>
public sealed record PatientTimelineNotePayloadResponse(
    string DoctorName,
    string NoteContent);

/// <summary>Payload of a timeline lab-result event.</summary>
public sealed record PatientTimelineLabPayloadResponse(
    string TestName,
    decimal Value,
    string Unit);

/// <summary>
/// Metadata payload of a timeline document event (F3-C3b Q-13). Deliberately
/// carries no storage location, path or URL.
/// </summary>
public sealed record PatientTimelineDocumentPayloadResponse(
    string FileName,
    MedicalDocumentType FileType,
    string? Description,
    string? UploadedByDoctorName);

/// <summary>
/// One event of the patient's medical timeline (Class Spec §4.2 getTimeline;
/// F3-C3b Q-12). Exactly one payload is non-null, matching
/// <see cref="EventType"/> (<c>visit</c>, <c>note</c>, <c>lab_result</c>,
/// <c>document</c>); <see cref="VisitId"/> carries the F4 deep-link target
/// where the event belongs to a visit.
/// </summary>
public sealed record PatientTimelineEventResponse(
    Guid Id,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    Guid? VisitId,
    PatientTimelineVisitPayloadResponse? Visit,
    PatientTimelineNotePayloadResponse? Note,
    PatientTimelineLabPayloadResponse? LabResult,
    PatientTimelineDocumentPayloadResponse? Document);

/// <summary>
/// The metadata of one medical document of the patient (Class Spec §4.11;
/// F3-C3b Q-13). <b>No</b> storage location, path or URL exists on this
/// response — file viewing/downloading is deferred until an approved secure
/// storage and file-serving flow exists, and <c>upload()</c> is out of F3.
/// </summary>
public sealed record PatientDocumentResponse(
    Guid Id,
    string FileName,
    MedicalDocumentType Type,
    DateTimeOffset UploadDateUtc,
    string? Description,
    string? UploadedByDoctorName);

/// <summary>
/// One previous visit of the patient with its linked clinical records
/// (Class Spec §4.3). Patient-level (unlinked) notes and lab results, documents
/// and the timeline are deferred to F3-C2 and are never part of this response.
/// </summary>
public sealed record MedicalVisitResponse(
    Guid VisitId,
    DateTimeOffset VisitDate,
    Guid DoctorId,
    string DoctorName,
    string? ChiefComplaint,
    string? Symptoms,
    string? ClinicalNotes,
    IReadOnlyList<VisitDiagnosisResponse> Diagnoses,
    IReadOnlyList<VisitMedicationResponse> Medications,
    IReadOnlyList<VisitVitalSignsResponse> VitalSigns,
    IReadOnlyList<VisitNoteResponse> Notes,
    IReadOnlyList<VisitLabResultResponse> LabResults);
