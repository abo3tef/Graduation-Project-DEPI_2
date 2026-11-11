namespace Medilog.Application.Features.MedicalRecords.Contracts;

/// <summary>
/// Request to record a prescribed medication line on an existing medical visit.
/// </summary>
/// <remarks>
/// The visit and the patient are taken from the route and the persisted visit, never from this
/// request, so a client cannot attach a medication to another patient's visit (AGENTS.md §5).
/// </remarks>
public sealed record CreateMedicationRequest(
    string MedicationName,
    string Dosage,
    string Frequency,
    string? Duration,
    string? Instructions);

/// <summary>Request to update the mutable fields of a recorded medication line.</summary>
public sealed record UpdateMedicationRequest(
    string MedicationName,
    string Dosage,
    string Frequency,
    string? Duration,
    string? Instructions);

/// <summary>Representation of a prescribed medication line returned to the client.</summary>
public sealed record MedicationResponse(
    Guid Id,
    Guid VisitId,
    Guid PatientId,
    string MedicationName,
    string Dosage,
    string Frequency,
    string? Duration,
    string? Instructions);
