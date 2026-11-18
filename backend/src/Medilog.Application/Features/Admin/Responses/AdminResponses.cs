using Medilog.Application.Common;
using Medilog.Domain.Enums;

namespace Medilog.Application.Features.Admin.Responses;

/// <summary>
/// An account as the Admin dashboard may see it. Credentials, password hashes, tokens and one-time
/// token hashes are never part of this contract.
/// </summary>
public sealed record AdminUserResponse(
    Guid Id,
    UserRole Role,
    string FullName,
    string Email,
    string? Phone,
    string? Specialization,
    AccountStatus Status,
    bool EmailVerified,
    DateTimeOffset CreatedAtUtc);

/// <summary>A doctor that may be assigned to a secretary, without medical data.</summary>
public sealed record AdminDoctorResponse(Guid Id, string FullName, string Email);

/// <summary>The complete assignment set for one secretary (Q-16).</summary>
public sealed record SecretaryDoctorsResponse(
    Guid SecretaryId,
    IReadOnlyList<AdminDoctorResponse> Doctors);
