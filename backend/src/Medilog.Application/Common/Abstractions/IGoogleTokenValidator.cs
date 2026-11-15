namespace Medilog.Application.Common.Abstractions;

/// <summary>
/// The identity proven by a validated Google ID token (FR-01, F6-C1-T8).
/// </summary>
/// <param name="Subject">Google's stable subject identifier for the account.</param>
/// <param name="Email">The email address Google asserts for the account.</param>
/// <param name="EmailVerified">Whether Google has verified that address.</param>
/// <remarks>
/// No role is present: a Google user never chooses a Medilog role, and the role always comes from
/// the existing account (decision Q-12).
/// </remarks>
public sealed record GoogleTokenIdentity(string Subject, string Email, bool EmailVerified);

/// <summary>
/// Validates a Firebase ID token on the server (decision Q-12).
/// </summary>
/// <remarks>
/// Implemented in Infrastructure with the framework JWT handler against
/// <c>securetoken.google.com/{projectId}</c>; signature, issuer, audience and expiry must all pass.
/// A token that cannot be validated is never trusted, and the raw token is never logged.
/// </remarks>
public interface IGoogleTokenValidator
{
    /// <summary>Returns the identity carried by a valid Google ID token.</summary>
    /// <exception cref="Common.Exceptions.AuthenticationFailedException">
    /// The token is malformed, forged, expired, issued for another project or lacks a subject
    /// (401, <c>auth.invalid_google_token</c>).
    /// </exception>
    /// <exception cref="Common.Exceptions.NotConfiguredException">
    /// Google sign-in is not configured or its public keys cannot be retrieved
    /// (503, <c>auth.google_not_configured</c>).
    /// </exception>
    Task<GoogleTokenIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken);
}
