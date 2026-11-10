using Medilog.Application.Features.MedicalRecords.Contracts;

namespace Medilog.Application.Features.MedicalRecords;

/// <summary>
/// Use cases for recording and updating medical visits and their diagnoses.
/// </summary>
/// <remarks>
/// Plain application service (decision D-08): MediatR is not adopted. Every method validates its
/// request before touching persistence, so validation cannot be bypassed by another caller.
/// </remarks>
public interface IMedicalVisitService
{
    Task<MedicalVisitResponse> CreateVisitAsync(
        CreateMedicalVisitRequest request,
        CancellationToken cancellationToken = default);

    Task<MedicalVisitResponse> GetVisitAsync(
        Guid visitId,
        CancellationToken cancellationToken = default);

    Task<MedicalVisitResponse> UpdateVisitAsync(
        Guid visitId,
        UpdateMedicalVisitRequest request,
        CancellationToken cancellationToken = default);

    Task<DiagnosisResponse> AddDiagnosisAsync(
        Guid visitId,
        CreateDiagnosisRequest request,
        CancellationToken cancellationToken = default);

    Task<DiagnosisResponse> UpdateDiagnosisAsync(
        Guid visitId,
        Guid diagnosisId,
        UpdateDiagnosisRequest request,
        CancellationToken cancellationToken = default);
}
