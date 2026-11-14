namespace Medilog.Application.Common;

/// <summary>
/// The server-side roles that may act on Medilog data.
/// </summary>
/// <remarks>
/// The member names are the exact <c>role</c> claim values (decision D-35), so a typo cannot
/// silently disable an access check. <c>Admin</c> manages accounts only (F11) and never owns
/// medical records.
/// </remarks>
public enum UserRole
{
    Doctor,
    Secretary,
    Admin
}
