using Medilog.Application.Common;
using Medilog.Domain.Enums;

namespace Medilog.Application.Features.Admin;

/// <summary>
/// A role-neutral account projection built in the data source from the three identity tables.
/// It never carries credentials or token material.
/// </summary>
internal sealed record AdminUserRecord(
    Guid Id,
    UserRole Role,
    string FullName,
    string Email,
    string? Phone,
    string? Specialization,
    AccountStatus Status,
    bool EmailVerified,
    DateTimeOffset CreatedAtUtc);
