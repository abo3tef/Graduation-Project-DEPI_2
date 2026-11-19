namespace Medilog.Application.Common.Abstractions;

/// <summary>
/// Builds the absolute links that are emailed to users.
/// </summary>
/// <remarks>
/// The base URL is configuration, not a per-request value, so link building is isolated behind
/// this port. The raw one-time token appears only inside the link and is never logged.
/// </remarks>
public interface IAuthLinkBuilder
{
    /// <summary>
    /// Link that confirms a new account's email address. The optional address is echoed in the link
    /// only so the verification screen can prefill its resend form; it is never proof of identity.
    /// </summary>
    string BuildEmailVerificationLink(string token, string? email = null);

    /// <summary>Link that lets a user choose a new password.</summary>
    string BuildPasswordResetLink(string token);
}
