using Medilog.Application.Common.Paging;
using Medilog.Application.Features.MedicalRecords.Requests;
using Medilog.Application.Features.MedicalRecords.Responses;

namespace Medilog.Application.Features.MedicalRecords;

/// <summary>
/// Read-only medical-record use cases for doctors (F3). Every method derives the
/// acting doctor only from <c>ICurrentUser</c> and scopes all reads to that
/// doctor's own patients. A secretary (or any non-doctor role) is rejected
/// before any validation or data access happens.
/// </summary>
public interface IMedicalRecordService
{
    /// <summary>The medical profile of one of the acting doctor's own patients.</summary>
    Task<PatientMedicalProfileResponse> GetMedicalProfileAsync(
        GetMedicalProfileRequest request,
        CancellationToken cancellationToken);

    /// <summary>One page of the patient's previous visits with their linked clinical records.</summary>
    Task<PagedResult<MedicalVisitResponse>> GetMedicalHistoryAsync(
        GetMedicalHistoryRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every clinical note of one of the acting doctor's own patients, linked
    /// and unlinked (F3-C3a), paged.
    /// </summary>
    Task<PagedResult<PatientNoteResponse>> ListPatientNotesAsync(
        ListPatientNotesRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every laboratory result of one of the acting doctor's own patients,
    /// linked and unlinked (F3-C3a), paged.
    /// </summary>
    Task<PagedResult<PatientLabResultResponse>> ListPatientLabResultsAsync(
        ListPatientLabResultsRequest request,
        CancellationToken cancellationToken);
}
