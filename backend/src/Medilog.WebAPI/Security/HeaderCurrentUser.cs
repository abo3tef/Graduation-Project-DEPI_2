using Medilog.Application.Common;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Interfaces;

namespace Medilog.WebAPI.Security;

/// <summary>
/// TEMPORARY identity source (Q-07): reads the acting user from the
/// <c>X-User-Id</c> and <c>X-User-Role</c> request headers until real
/// authentication is implemented by its owner. Remove this class, the
/// <c>Auth:AllowHeaderIdentity</c> setting and its DI registration when Q-07 lands
/// and replace it with a claims-based <see cref="ICurrentUser"/>.
/// </summary>
/// <remarks>
/// Headers can be forged by any caller, so they are honored only when
/// <c>Auth:AllowHeaderIdentity</c> is <c>true</c> (set in
/// <c>appsettings.Development.json</c>). When the flag is off, or a header is
/// missing/invalid, every use is rejected with <see cref="ForbiddenException"/>
/// (HTTP 403). The role values are case-sensitive: <c>Doctor</c> or <c>Secretary</c>.
/// Header values are never logged.
/// </remarks>
internal sealed class HeaderCurrentUser : ICurrentUser
{
    public const string UserIdHeader = "X-User-Id";

    public const string UserRoleHeader = "X-User-Role";

    public const string EnabledSettingName = "Auth:AllowHeaderIdentity";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly bool _enabled;
    private (Guid UserId, UserRole Role)? _resolved;

    public HeaderCurrentUser(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _enabled = configuration.GetValue<bool>(EnabledSettingName);
    }

    public Guid UserId => Resolve().UserId;

    public UserRole Role => Resolve().Role;

    private (Guid UserId, UserRole Role) Resolve()
    {
        _resolved ??= ReadHeaders();
        return _resolved.Value;
    }

    private (Guid UserId, UserRole Role) ReadHeaders()
    {
        var headers = _httpContextAccessor.HttpContext?.Request.Headers;

        if (!_enabled || headers is null)
        {
            throw new ForbiddenException("The current user could not be identified.");
        }

        var rawId = headers[UserIdHeader].ToString();
        var rawRole = headers[UserRoleHeader].ToString();

        if (!Guid.TryParseExact(rawId, "D", out var userId) || userId == Guid.Empty)
        {
            throw new ForbiddenException("The current user could not be identified.");
        }

        // Exact, case-sensitive names only: numeric values ("1") or other casing are rejected.
        var role = rawRole switch
        {
            nameof(UserRole.Doctor) => UserRole.Doctor,
            nameof(UserRole.Secretary) => UserRole.Secretary,
            _ => (UserRole?)null
        };

        return role is null
            ? throw new ForbiddenException("The current user could not be identified.")
            : (userId, role.Value);
    }
}
