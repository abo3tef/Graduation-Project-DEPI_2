namespace Medilog.Application.Features.MedicalRecords.Contracts;

/// <summary>
/// Request to record a new medical visit for a patient in the doctor's workspace.
/// </summary>
/// <remarks>
/// The acting doctor is resolved from the authenticated identity, never from this request
/// (AGENTS.md §5). <see cref="AppointmentId"/>, when supplied, must belong to the same patient and
/// doctor.
/// </remarks>
public sealed record CreateMedicalVisitRequest(
    Guid PatientId,
    Guid? AppointmentId,
    DateTimeOffset VisitDate,
    string? ChiefComplaint,
    string? Symptoms,
    string? ClinicalNotes);

/// <summary>
/// Request to update the mutable fields of an existing medical visit.
/// </summary>
/// <remarks>
/// The patient, doctor and appointment link are immutable after creation, so they are not part of
/// this contract and can never be overwritten by a client (AGENTS.md §3.3).
/// </remarks>
public sealed record UpdateMedicalVisitRequest(
    DateTimeOffset VisitDate,
    string? ChiefComplaint,
    string? Symptoms,
    string? ClinicalNotes);

/// <summary>
/// Representation of a medical visit returned to the client.
/// </summary>
/// <remarks>
/// Deliberately excludes EF navigations and any doctor or patient contact data (AGENTS.md §4.4, §5).
/// </remarks>
public sealed record MedicalVisitResponse(
    Guid Id,
    Guid PatientId,
    Guid DoctorId,
    Guid? AppointmentId,
    DateTimeOffset VisitDate,
    string? ChiefComplaint,
    string? Symptoms,
    string? ClinicalNotes,
    IReadOnlyList<DiagnosisResponse> Diagnoses,
    IReadOnlyList<MedicationResponse> Medications);
