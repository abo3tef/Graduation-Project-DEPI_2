using Medilog.Application.Common.Paging;
using Medilog.Application.Common.Security;
using Medilog.Application.Features.Admin;
using Medilog.Application.Features.Admin.Requests;
using Medilog.Application.Features.Admin.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Medilog.WebAPI.Controllers;

/// <summary>
/// Account administration endpoints (F11-C1). Thin by design: bind HTTP input, call
/// <see cref="IAdminService"/>, shape the response. Self/last-Admin protection, Q-22 role
/// transitions and Q-16 assignments are enforced in the Application service.
/// </summary>
/// <remarks>
/// Already-issued JWTs are not revoked here: their signature and lifetime stay valid until expiry.
/// Every admin use case still re-checks that the acting Admin account is active, and deactivated
/// user accounts cannot sign in again until reactivated.
/// </remarks>
[ApiController]
[Route("api/admin")]
[Produces("application/json")]
[Authorize(Roles = Roles.Admin)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class AdminController : ControllerBase
{
    private readonly IAdminService _admin;

    public AdminController(IAdminService admin)
    {
        _admin = admin;
    }

    /// <summary>Lists accounts with role/status filters, search and shared Q-04 paging.</summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(PagedResult<AdminUserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AdminUserResponse>>> ListUsers(
        [FromQuery] ListAdminUsersQuery query,
        CancellationToken cancellationToken) =>
        await _admin.ListAsync(query, cancellationToken);

    /// <summary>Activates or deactivates an account.</summary>
    [HttpPatch("users/{id:guid}/status")]
    [ProducesResponseType(typeof(AdminUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminUserResponse>> SetStatus(
        Guid id,
        [FromBody] SetAdminUserStatusRequest request,
        CancellationToken cancellationToken) =>
        await _admin.SetStatusAsync(id, request, cancellationToken);

    /// <summary>Changes a Doctor account to Secretary or vice versa (Q-22).</summary>
    [HttpPut("users/{id:guid}/role")]
    [ProducesResponseType(typeof(AdminUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminUserResponse>> ChangeRole(
        Guid id,
        [FromBody] ChangeAdminUserRoleRequest request,
        CancellationToken cancellationToken) =>
        await _admin.ChangeRoleAsync(id, request, cancellationToken);

    /// <summary>Returns the doctors currently assigned to one secretary (Q-16).</summary>
    [HttpGet("secretaries/{secretaryId:guid}/doctors")]
    [ProducesResponseType(typeof(SecretaryDoctorsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SecretaryDoctorsResponse>> GetDoctors(
        Guid secretaryId,
        CancellationToken cancellationToken) =>
        await _admin.GetDoctorsAsync(secretaryId, cancellationToken);

    /// <summary>Replaces the doctors assigned to one secretary (Q-16).</summary>
    [HttpPut("secretaries/{secretaryId:guid}/doctors")]
    [ProducesResponseType(typeof(SecretaryDoctorsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SecretaryDoctorsResponse>> ReplaceDoctors(
        Guid secretaryId,
        [FromBody] UpdateSecretaryDoctorsRequest request,
        CancellationToken cancellationToken) =>
        await _admin.ReplaceDoctorsAsync(secretaryId, request, cancellationToken);
}
