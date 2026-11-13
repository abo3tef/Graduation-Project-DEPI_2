namespace Medilog.Application.Features.MedicalRecords.Contracts;

/// <summary>
/// Request to record a diagnosis on an existing medical visit.
/// </summary>
/// <remarks>
/// The visit and the patient are taken from the route and the persisted visit, never from this
/// request, so a client cannot attach a diagnosis to another patient's visit.
/// </remarks>
public sealed record CreateDiagnosisRequest(
    string DiagnosisName,
    string? Description,
    DateOnly DiagnosisDate);

/// <summary>Request to update the mutable fields of a recorded diagnosis.</summary>
public sealed record UpdateDiagnosisRequest(
    string DiagnosisName,
    string? Description,
    DateOnly DiagnosisDate);

/// <summary>Representation of a diagnosis returned to the client.</summary>
public sealed record DiagnosisResponse(
    Guid Id,
    Guid VisitId,
    Guid PatientId,
    string DiagnosisName,
    string? Description,
    DateOnly DiagnosisDate);
