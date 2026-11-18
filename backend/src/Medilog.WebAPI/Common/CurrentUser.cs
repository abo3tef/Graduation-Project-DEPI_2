using System.Security.Claims;
using Medilog.Application.Common;
using Medilog.Application.Common.Abstractions;

namespace Medilog.WebAPI.Common;

/// <summary>
/// Resolves the acting user from the authenticated principal (decisions D-11/D-16).
/// </summary>
/// <remarks>
/// Identity is taken only from the validated JWT bearer principal. Client-supplied headers are
/// never trusted. Both claim spellings are accepted: the JWT options keep the wire names
/// (<c>sub</c>/<c>role</c>) while the default inbound mapping uses
/// <see cref="ClaimTypes.NameIdentifier"/> and <see cref="ClaimTypes.Role"/>.
/// </remarks>
public sealed class CurrentUser : ICurrentUser
{
    private const string SubjectClaim = "sub";
    private const string RoleClaim = "role";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var value = FindFirstValue(ClaimTypes.NameIdentifier, SubjectClaim);

            // Hyphenated ("D") form only; the empty GUID is never a valid user.
            return Guid.TryParseExact(value, "D", out var userId) && userId != Guid.Empty ? userId : null;
        }
    }

    public UserRole? Role
    {
        get
        {
            var value = FindFirstValue(ClaimTypes.Role, RoleClaim);

            // Exact, case-sensitive names only: numeric values ("1") or other casing are rejected.
            return value switch
            {
                nameof(UserRole.Doctor) => UserRole.Doctor,
                nameof(UserRole.Secretary) => UserRole.Secretary,
                // Admin is a real authenticated role; controller authorization is the first gate,
                // and AdminService re-checks the account in the database.
                nameof(UserRole.Admin) => UserRole.Admin,
                _ => null
            };
        }
    }

    private string? FindFirstValue(params string[] claimTypes)
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        if (principal is null)
        {
            return null;
        }

        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirstValue(claimType);

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
