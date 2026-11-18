using Medilog.Application.Common;
using Medilog.Domain.Enums;

namespace Medilog.Application.Features.Admin;

/// <summary>
/// Rules shared by the admin account endpoints (F11-C1, Q-16/Q-22).
/// </summary>
/// <remarks>
/// Shape rules live in the validators; the service owns the business checks (self protection,
/// last active Admin, role-transition conflicts and secretary/doctor assignment scope).
/// </remarks>
internal static class AdminRules
{
    /// <summary>Maximum length of the free-text user search.</summary>
    public const int SearchMaxLength = 100;

    /// <summary>Upper bound for one secretary's doctor assignments in a single request.</summary>
    public const int MaxDoctorAssignments = 100;

    /// <summary>The roles whose accounts the Admin dashboard may list and edit.</summary>
    public static readonly UserRole[] ManageableRoles = [UserRole.Admin, UserRole.Doctor, UserRole.Secretary];

    /// <summary>Roles an Admin may assign. Admin is never changed through this endpoint (Q-22).</summary>
    public static readonly UserRole[] AssignableRoles = [UserRole.Doctor, UserRole.Secretary];

    /// <summary>Parses an optional role filter; an empty value means "no filter".</summary>
    public static bool TryParseRoleFilter(string? value, out UserRole role)
    {
        role = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out role) &&
               ManageableRoles.Contains(role);
    }

    /// <summary>Parses an optional account-status filter; an empty value means "no filter".</summary>
    public static bool TryParseStatusFilter(string? value, out AccountStatus status)
    {
        status = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out status) &&
               Enum.IsDefined(status);
    }

    /// <summary>Maps a user role to the one-time-token account kind.</summary>
    public static UserType ToUserType(UserRole role) => role switch
    {
        UserRole.Doctor => UserType.Doctor,
        UserRole.Secretary => UserType.Secretary,
        UserRole.Admin => UserType.Admin,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown account role.")
    };
}
