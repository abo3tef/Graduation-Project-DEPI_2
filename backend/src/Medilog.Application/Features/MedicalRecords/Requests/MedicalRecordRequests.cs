using Medilog.Application.Common.Paging;

namespace Medilog.Application.Features.MedicalRecords.Requests;

/// <summary>Input of the medical profile use case. The actor is never part of the request.</summary>
public sealed record GetMedicalProfileRequest(Guid PatientId);

/// <summary>
/// Input of the medical history use case. Paging follows the shared contract
/// (Q-04 / CONVENTIONS.md): the request carries <c>pageNumber</c>/<c>pageSize</c>,
/// bounded by <c>PagingDefaults</c>. The optional parameters carry the shared
/// defaults (F3-C2 P-3, mirroring F1's <c>ListAppointmentsQuery</c>) so a caller
/// that omits paging gets the Q-04 defaults; no F3-specific limits exist.
/// </summary>
public sealed record GetMedicalHistoryRequest(
    Guid PatientId,
    int PageNumber = PagingDefaults.DefaultPageNumber,
    int PageSize = PagingDefaults.DefaultPageSize) : IPagedQuery;

/// <summary>
/// Input of the patient-notes use case (F3-C3a). Every note of the patient is
/// returned — visit-linked and unlinked (contributor decision C-2).
/// </summary>
public sealed record ListPatientNotesRequest(
    Guid PatientId,
    int PageNumber = PagingDefaults.DefaultPageNumber,
    int PageSize = PagingDefaults.DefaultPageSize) : IPagedQuery;

/// <summary>
/// Input of the patient-lab-results use case (F3-C3a). Every laboratory result
/// of the patient is returned — visit-linked and unlinked (contributor decision C-2).
/// </summary>
public sealed record ListPatientLabResultsRequest(
    Guid PatientId,
    int PageNumber = PagingDefaults.DefaultPageNumber,
    int PageSize = PagingDefaults.DefaultPageSize) : IPagedQuery;
