using System.Security.Claims;
using Medilog.Application.Common;
using Medilog.Application.Common.Abstractions;

namespace Medilog.WebAPI.Common;

/// <summary>
/// Resolves the acting user from the authenticated principal (decisions D-11/D-16).
/// </summary>
/// <remarks>
/// <para>
/// JWT Bearer maps <c>sub</c> to <see cref="ClaimTypes.NameIdentifier"/> and <c>role</c> to
/// <see cref="ClaimTypes.Role"/> by default. The raw claim names are also accepted so the
/// implementation keeps working if inbound claim mapping is turned off by configuration.
/// </para>
/// <para>
/// TEMPORARY fallback (Q-07): when <c>Auth:AllowHeaderIdentity</c> is <c>true</c> (Development
/// only) and no authenticated principal is present, the <c>X-User-Id</c> / <c>X-User-Role</c>
/// headers are honored. Headers can be forged by any caller, so this is rejected in production.
/// Header values are never logged.
/// </para>
/// </remarks>
public sealed class CurrentUser : ICurrentUser
{
    private const string SubjectClaim = "sub";
    private const string RoleClaim = "role";

    public const string UserIdHeader = "X-User-Id";

    public const string UserRoleHeader = "X-User-Role";

    public const string EnabledSettingName = "Auth:AllowHeaderIdentity";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly bool _allowHeaderIdentity;

    public CurrentUser(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _allowHeaderIdentity = configuration.GetValue<bool>(EnabledSettingName);
    }

    public Guid? UserId
    {
        get
        {
            var value = FindFirstValue(ClaimTypes.NameIdentifier, SubjectClaim) ?? ReadHeader(UserIdHeader);

            // Hyphenated ("D") form only, matching the header contract the development
            // identity documents; the empty GUID is never a valid user.
            return Guid.TryParseExact(value, "D", out var userId) && userId != Guid.Empty ? userId : null;
        }
    }

    public UserRole? Role
    {
        get
        {
            var value = FindFirstValue(ClaimTypes.Role, RoleClaim) ?? ReadHeader(UserRoleHeader);

            // Exact, case-sensitive names only: numeric values ("1") or other casing are rejected.
            return value switch
            {
                nameof(UserRole.Doctor) => UserRole.Doctor,
                nameof(UserRole.Secretary) => UserRole.Secretary,
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

    private string? ReadHeader(string name)
    {
        if (!_allowHeaderIdentity)
        {
            return null;
        }

        var value = _httpContextAccessor.HttpContext?.Request.Headers[name].ToString();

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
