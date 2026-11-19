using Medilog.Application.Common;
using Medilog.Domain.Enums;

namespace Medilog.Application.Features.Admin;

/// <summary>
/// The composable projection returned by the three-table account union before it is mapped to
/// <see cref="AdminUserRecord"/>.
/// </summary>
/// <remarks>
/// <para>
/// EF Core can only compose <c>Where</c>/<c>OrderBy</c> against a projection when it can map a
/// member access back to the source expression. A member-initializer projection (this class) supports
/// that; the positional-record projection previously used here did not, which produced
/// <c>"new AdminUserRecord(...).Email could not be translated"</c> after the <c>UNION</c>.
/// </para>
/// <para>
/// This type never carries credentials or token material and never leaves the Application layer.
/// </para>
/// </remarks>
internal sealed class AdminUserRow
{
    public Guid Id { get; set; }

    public UserRole Role { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Specialization { get; set; }

    public AccountStatus Status { get; set; }

    public bool EmailVerified { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
