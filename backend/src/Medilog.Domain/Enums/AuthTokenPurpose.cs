namespace Medilog.Domain.Enums;

/// <summary>
/// Why a one-time authentication token was issued. A token is only valid for the purpose
/// it was created for (F6-C1, decision D-33).
/// </summary>
public enum AuthTokenPurpose
{
    /// <summary>Confirms ownership of an email address and activates a new account.</summary>
    EmailVerification,

    /// <summary>Allows setting a new password without being signed in.</summary>
    PasswordReset
}
