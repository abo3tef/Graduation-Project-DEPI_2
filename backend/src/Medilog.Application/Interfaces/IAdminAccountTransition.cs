using Medilog.Application.Common;
using Medilog.Domain.Enums;

namespace Medilog.Application.Interfaces;

/// <summary>
/// Account data needed to move one account between the role-specific identity tables without
/// changing its id or credentials. The application service decides whether the move is allowed;
/// this port only performs the technical, atomic row replacement.
/// </summary>
public sealed record AdminAccountSnapshot(
    Guid Id,
    UserRole CurrentRole,
    UserRole NewRole,
    string FullName,
    string Email,
    string PasswordHash,
    string? Phone,
    string? Specialization,
    AccountStatus Status,
    DateTimeOffset? EmailVerifiedAtUtc,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Atomically replaces an account row in one identity table with the same id in another table.
/// Implemented in Infrastructure because it needs a database transaction; no business rules
/// belong here (they stay in <c>AdminService</c>).
/// </summary>
public interface IAdminAccountTransition
{
    Task ChangeRoleAsync(AdminAccountSnapshot account, CancellationToken cancellationToken);
}
