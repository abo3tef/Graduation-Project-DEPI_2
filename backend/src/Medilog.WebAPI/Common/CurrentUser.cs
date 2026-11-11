using System.Security.Claims;
using Medilog.Application.Common.Abstractions;

namespace Medilog.WebAPI.Common;

/// <summary>
/// Resolves the acting user from the authenticated principal (decision D-11/D-16).
/// </summary>
/// <remarks>
/// JWT Bearer maps <c>sub</c> to <see cref="ClaimTypes.NameIdentifier"/> and <c>role</c> to
/// <see cref="ClaimTypes.Role"/> by default. The raw claim names are also accepted so the
/// implementation keeps working if inbound claim mapping is turned off by configuration.
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

            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

    public string? Role => FindFirstValue(ClaimTypes.Role, RoleClaim);

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
