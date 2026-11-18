using Medilog.Application.Common;
using Medilog.Application.Common.Paging;
using Medilog.Domain.Enums;

namespace Medilog.Application.Features.Admin.Requests;

/// <summary>
/// User search with role/status filters and the shared Q-04 paging contract.
/// Role and status are strings so an unsupported filter value is a validation error,
/// not a silently ignored filter.
/// </summary>
public sealed record ListAdminUsersQuery(
    string? Role = null,
    string? Status = null,
    string? Search = null,
    int PageNumber = PagingDefaults.DefaultPageNumber,
    int PageSize = PagingDefaults.DefaultPageSize) : IPagedQuery;

/// <summary>Full replacement of an account's active/inactive flag.</summary>
public sealed record SetAdminUserStatusRequest(AccountStatus Status);

/// <summary>
/// Requests a Doctor↔Secretary transition (Q-22). Admin cannot be assigned here and
/// an existing Admin account is never changed by this endpoint.
/// </summary>
public sealed record ChangeAdminUserRoleRequest(UserRole Role);

/// <summary>
/// Full replacement of a secretary's doctor assignments (Q-16). Duplicate ids are rejected by
/// validation and unique pairs are also enforced by the database join table.
/// </summary>
public sealed record UpdateSecretaryDoctorsRequest(IReadOnlyList<Guid> DoctorIds);
