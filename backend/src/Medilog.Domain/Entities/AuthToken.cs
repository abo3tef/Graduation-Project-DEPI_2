using Medilog.Domain.Common;
using Medilog.Domain.Enums;

namespace Medilog.Domain.Entities;

/// <summary>
/// A single-use, expiring token issued for email verification or password reset.
/// </summary>
/// <remarks>
/// <para>
/// Only a hash of the token is stored (AGENTS.md §5, decision D-33); the raw value is returned
/// to the caller once, inside the emailed link, and never persisted or logged.
/// </para>
/// <para>
/// The owner is stored as a <see cref="UserId"/> + <see cref="UserType"/> pair instead of a
/// foreign key because the three account kinds live in three different tables. Integrity is
/// enforced by the authentication service, and the table only holds disposable data.
/// </para>
/// </remarks>
public sealed class AuthToken : CreatedEntity
{
    public Guid UserId { get; set; }

    public UserType UserType { get; set; }

    public AuthTokenPurpose Purpose { get; set; }

    /// <summary>Hex-encoded SHA-256 hash of the raw token; never the raw token itself.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    /// <summary>Set when the token is used or replaced, so a token can never be used twice.</summary>
    public DateTimeOffset? ConsumedAtUtc { get; set; }
}
