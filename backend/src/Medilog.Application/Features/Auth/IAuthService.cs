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

    /// <summary>
    /// Creates a doctor or secretary account and emails a verification link.
    /// </summary>
    /// <remarks>
    /// No token is returned: the account cannot sign in until its email address is confirmed
    /// (Q-11). The role comes from the closed allow-list, never from an existing account.
    /// </remarks>
    /// <exception cref="Common.Exceptions.ConflictException">
    /// The email address already has an account (409, <c>auth.email_already_registered</c>).
    /// </exception>
    Task<AuthUserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    /// <summary>Confirms an email address with a single-use token and activates the account.</summary>
    /// <exception cref="Common.Exceptions.AuthenticationFailedException">
    /// The token is unknown, already used or expired (401, <c>auth.token_invalid</c>).
    /// </exception>
    Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Sends another verification email when an unverified account exists.
    /// </summary>
    /// <remarks>
    /// Completes normally whether or not the address has an account, so the endpoint cannot be used
    /// to discover registered emails (AGENTS.md §5).
    /// </remarks>
    Task ResendVerificationAsync(ResendVerificationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Starts password recovery by emailing a single-use reset link when the address has an account.
    /// </summary>
    /// <remarks>
    /// Completes normally whether or not the address has an account, so the endpoint cannot be used
    /// to discover registered emails (AGENTS.md §5).
    /// </remarks>
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a new password with the single-use token from the emailed reset link.
    /// </summary>
    /// <exception cref="Common.Exceptions.AuthenticationFailedException">
    /// The token is unknown, already used, expired or was issued for another purpose
    /// (401, <c>auth.token_invalid</c>).
    /// </exception>
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);
}
