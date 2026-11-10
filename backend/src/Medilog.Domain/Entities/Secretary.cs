using Medilog.Domain.Common;
using Medilog.Domain.Enums;

namespace Medilog.Domain.Entities;

/// <summary>
/// An authenticated user who manages appointments for assigned doctors.
/// Secretaries are not medical users and must never access medical records.
/// </summary>
public sealed class Secretary : CreatedEntity
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public AccountStatus AccountStatus { get; set; }

    public ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();
}
