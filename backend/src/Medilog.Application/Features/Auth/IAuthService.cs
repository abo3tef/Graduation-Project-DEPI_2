using Medilog.Application.Features.Auth.Contracts;

namespace Medilog.Application.Features.Auth;

/// <summary>
/// Authentication use cases (F6-C1).
/// </summary>
/// <remarks>
/// The WebAPI controller stays a thin adapter over this interface (AGENTS.md §4.1). Failures are
/// raised as typed <c>AppException</c>s so the shared error envelope carries a stable code.
/// </remarks>
public interface IAuthService
{
    /// <summary>
    /// Authenticates an email/password pair and issues an access token.
    /// </summary>
    /// <exception cref="Common.Exceptions.AuthenticationFailedException">
    /// Unknown email or wrong password (401, identical for both).
    /// </exception>
    /// <exception cref="Common.Exceptions.ForbiddenException">
    /// The account is inactive or its email is not verified (403, Q-11).
    /// </exception>
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}
