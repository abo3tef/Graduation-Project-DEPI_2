using Medilog.Domain.Common;
using Medilog.Domain.Enums;

namespace Medilog.Domain.Entities;

/// <summary>
/// A platform administrator who manages doctor and secretary accounts (F11).
/// </summary>
/// <remarks>
/// Admins have no medical profile and are never created through registration or Google
/// Sign-In (AGENTS.md §5, decision D-32); they are seeded or created by another Admin.
/// </remarks>
public sealed class Admin : CreatedEntity
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public AccountStatus AccountStatus { get; set; }

    public DateTimeOffset? EmailVerifiedAtUtc { get; set; }
}
