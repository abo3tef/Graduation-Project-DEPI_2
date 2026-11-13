using Medilog.Application.Common.Paging;

namespace Medilog.Application.Features.MedicalRecords.Requests;

/// <summary>Input of the medical profile use case. The actor is never part of the request.</summary>
public sealed record GetMedicalProfileRequest(Guid PatientId);

/// <summary>
/// Input of the medical history use case. Paging follows the shared contract
/// (Q-04 / CONVENTIONS.md): the request carries <c>pageNumber</c>/<c>pageSize</c>,
/// bounded by <c>PagingDefaults</c>.
/// </summary>
public sealed record GetMedicalHistoryRequest(Guid PatientId, int PageNumber, int PageSize) : IPagedQuery;
