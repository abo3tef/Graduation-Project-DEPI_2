using System.Text;
using Medilog.Application.Common;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Medilog.Infrastructure.Security;

/// <summary>
/// Issues HS256 access tokens with the claims contract in decision D-35.
/// </summary>
/// <remarks>
/// <para>
/// <c>sub</c> is the account id, <c>role</c> is the exact role claim value, and doctors also get
/// <c>doctorId</c> equal to <c>sub</c>. That equality is what lets the Phase 1 authorization and
/// data scoping code keep working unchanged once a real token replaces the development header.
/// </para>
/// <para>
/// When the signing key or issuer/audience is missing the issuer throws
/// <see cref="NotConfiguredException"/> (503 <c>auth.not_configured</c>) instead of producing a
/// token signed with a weak or empty key. The raw token is never logged.
/// </para>
/// </remarks>
public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    private const string SubjectClaim = "sub";
    private const string RoleClaim = "role";
    private const string DoctorIdClaim = "doctorId";
    private const string TokenIdClaim = "jti";

    private readonly JwtOptions _options;
    private readonly TimeProvider _clock;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtAccessTokenIssuer(JwtOptions options, TimeProvider clock)
    {
        _options = options;
        _clock = clock;
    }

    public IssuedAccessToken IssueAccessToken(Guid userId, UserRole role, Guid? doctorId)
    {
        if (!_options.IsConfigured || userId == Guid.Empty)
        {
            throw new NotConfiguredException(
                "Authentication is not configured. Set the JWT signing key, issuer and audience.",
                ErrorCodes.AuthNotConfigured);
        }

        var issuedAt = _clock.GetUtcNow();
        var expiresAt = issuedAt.AddMinutes(_options.AccessTokenMinutes);

        var claims = new Dictionary<string, object>
        {
            [SubjectClaim] = userId.ToString(),
            [RoleClaim] = role.ToString(),
            [TokenIdClaim] = Guid.NewGuid().ToString("N"),
        };

        if (role == UserRole.Doctor && doctorId is { } id && id != Guid.Empty)
        {
            claims[DoctorIdClaim] = id.ToString();
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = claims,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new IssuedAccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
