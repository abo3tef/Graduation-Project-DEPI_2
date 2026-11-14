using Medilog.Application.Common;

namespace Medilog.Application.Features.Auth.Contracts;

/// <summary>
/// Credentials for signing in with an email address and password.
/// </summary>
/// <remarks>
/// The password is only ever compared against a stored hash and is never logged, returned or
/// persisted in raw form (AGENTS.md §5).
/// </remarks>
public sealed record LoginRequest(string Email, string Password);

/// <summary>
/// The account summary returned alongside an access token.
/// </summary>
/// <remarks>
/// Deliberately contains no password hash, token or medical data. <see cref="DoctorId"/> is present
/// only for doctors and equals <see cref="Id"/>, matching the <c>doctorId</c> claim (D-35).
/// </remarks>
public sealed record AuthUserResponse(
    Guid Id,
    string FullName,
    string Email,
    UserRole Role,
    Guid? DoctorId);

/// <summary>
/// A successful sign-in: the access token, its expiry and the acting account.
/// </summary>
/// <param name="AccessToken">Compact JWT. Returned once and never logged; there is no refresh token (Q-13).</param>
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    AuthUserResponse User);
