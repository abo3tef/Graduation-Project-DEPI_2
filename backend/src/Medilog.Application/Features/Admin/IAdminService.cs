using Medilog.Application.Common.Paging;
using Medilog.Application.Features.Admin.Requests;
using Medilog.Application.Features.Admin.Responses;

namespace Medilog.Application.Features.Admin;

/// <summary>
/// Account administration use cases (F11-C1). Every method resolves the acting Admin from
/// <see cref="Common.Abstractions.ICurrentUser"/> and re-checks the account in the database;
/// no role or ownership value is accepted from the caller.
/// </summary>
public interface IAdminService
{
    Task<PagedResult<AdminUserResponse>> ListAsync(
        ListAdminUsersQuery query,
        CancellationToken cancellationToken);

    Task<AdminUserResponse> SetStatusAsync(
        Guid userId,
        SetAdminUserStatusRequest request,
        CancellationToken cancellationToken);

    Task<AdminUserResponse> ChangeRoleAsync(
        Guid userId,
        ChangeAdminUserRoleRequest request,
        CancellationToken cancellationToken);

    Task<SecretaryDoctorsResponse> GetDoctorsAsync(
        Guid secretaryId,
        CancellationToken cancellationToken);

    Task<SecretaryDoctorsResponse> ReplaceDoctorsAsync(
        Guid secretaryId,
        UpdateSecretaryDoctorsRequest request,
        CancellationToken cancellationToken);
}
