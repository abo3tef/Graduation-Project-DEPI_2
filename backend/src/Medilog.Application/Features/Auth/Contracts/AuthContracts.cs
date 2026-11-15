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
/// A request to create a doctor or secretary account (FR-01, F6-C1-T4).
/// </summary>
/// <remarks>
/// <see cref="Role"/> is a closed allow-list: only <see cref="UserRole.Doctor"/> and
/// <see cref="UserRole.Secretary"/> are accepted, so an Admin can never be created this way
/// (AGENTS.md §5). The account summary is returned without a token; the user must verify the email
/// address first (Q-11).
/// </remarks>
public sealed record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    string? Phone,
    UserRole Role,
    string? Specialization);

/// <summary>
/// Confirms the email address of a newly registered account with the token from the email link.
/// </summary>
public sealed record VerifyEmailRequest(string Token);

/// <summary>
/// Requests another verification email. The response is the same whether or not the account
/// exists, so it cannot be used to enumerate accounts (AGENTS.md §5).
/// </summary>
public sealed record ResendVerificationRequest(string Email);

/// <summary>
/// Starts password recovery for an email address (FR-04, F6-C1-T7).
/// </summary>
/// <remarks>
/// The response is the same whether or not the address has an account, so it cannot be used to
/// discover registered emails (AGENTS.md §5).
/// </remarks>
public sealed record ForgotPasswordRequest(string Email);

/// <summary>
/// Completes password recovery with the single-use token from the emailed link (F6-C1-T7).
/// </summary>
/// <remarks>
/// The new password goes through the same policy as registration. The token is consumed in the same
/// transaction as the password change, so it cannot be replayed.
/// </remarks>
public sealed record ResetPasswordRequest(string Token, string NewPassword);

/// <summary>
/// Signs in with a Firebase ID token obtained by the Google button (F6-C1-T8, Q-12).
/// </summary>
/// <remarks>
/// The request carries no role and no account data: the token is validated on the server and the
/// identity, role and doctor id all come from the existing account.
/// </remarks>
public sealed record GoogleSignInRequest(string IdToken);

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
