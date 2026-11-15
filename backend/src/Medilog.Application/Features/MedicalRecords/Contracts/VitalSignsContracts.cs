using Medilog.Application.Common.Paging;

namespace Medilog.Application.Features.MedicalRecords.Contracts;

/// <summary>Seven measurements only; ownership and visit identity come from the server.</summary>
/// <remarks>PUT replaces every measurement; null clears the previous value.</remarks>
public sealed record SaveVitalSignsRequest(
    string? BloodPressure,
    int? HeartRate,
    decimal? Temperature,
    int? RespiratoryRate,
    decimal? Weight,
    decimal? Height,
    int? SpO2);

public sealed record ListVitalSignsQuery(
    int PageNumber = PagingDefaults.DefaultPageNumber,
    int PageSize = PagingDefaults.DefaultPageSize) : IPagedQuery;
