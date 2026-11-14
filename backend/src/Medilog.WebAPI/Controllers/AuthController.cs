using Medilog.Application.Features.Auth;
using Medilog.Application.Features.Auth.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Medilog.WebAPI.Controllers;

/// <summary>
/// Anonymous authentication surface: sign in with email/password or Google, register, verify an
/// email address and recover a password (F6-C1).
/// </summary>
/// <remarks>
/// Every action is a thin adapter over <see cref="IAuthService"/>: no credential, token or role
/// rule lives here (AGENTS.md §4.1, §5). The routes are anonymous by design; each use case decides
/// what the caller may do from the data it receives.
/// </remarks>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Signs in with an email address and password.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AuthResponse>> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(request, cancellationToken);

        return Ok(response);
    }
}
