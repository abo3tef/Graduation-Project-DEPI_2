namespace Medilog.Application.Features.MedicalRecords.Requests;

/// <summary>Input of the medical profile use case. The actor is never part of the request.</summary>
public sealed record GetMedicalProfileRequest(Guid PatientId);

/// <summary>
/// Input of the medical history use case. Paging defaults come from
/// <c>PagingDefaults</c> at the API boundary; the service validates the values.
/// </summary>
public sealed record GetMedicalHistoryRequest(Guid PatientId, int PageNumber, int PageSize);
