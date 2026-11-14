using Medilog.Domain.Enums;

namespace Medilog.Application.Features.Auth;

/// <summary>
/// How long a one-time authentication token stays valid (decision D-36).
/// </summary>
/// <remarks>
/// The values are approved demo values and are read from configuration by the host, so they can be
/// shortened without a code change. They are ordinary configuration, not secrets.
/// </remarks>
public sealed class AuthTokenLifetimes
{
    public TimeSpan EmailVerification { get; set; } = TimeSpan.FromMinutes(1440);

    public TimeSpan PasswordReset { get; set; } = TimeSpan.FromMinutes(60);

    /// <summary>Lifetime for the requested purpose.</summary>
    public TimeSpan For(AuthTokenPurpose purpose) => purpose switch
    {
        AuthTokenPurpose.EmailVerification => EmailVerification,
        AuthTokenPurpose.PasswordReset => PasswordReset,
        _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "Unknown token purpose.")
    };
}
