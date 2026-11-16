using Medilog.Application.Common;

namespace Medilog.Application.Common.Abstractions;

/// <summary>
/// A signed access token and the instant it stops being valid.
/// </summary>
/// <param name="Token">The compact serialized JWT. Returned to the client once; never logged.</param>
/// <param name="ExpiresAtUtc">UTC expiry, also carried by the token's <c>exp</c> claim.</param>
public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAtUtc);

/// <summary>
/// Issues the access token for an authenticated account.
/// </summary>
/// <remarks>
/// The claims contract is decision D-35: <c>sub</c> = account id, <c>role</c> =
/// <c>Doctor</c>/<c>Secretary</c>/<c>Admin</c>, and <c>doctorId</c> only for doctors. The caller
/// passes the role resolved from the account record, so a client can never choose it.
/// </remarks>
public interface IAccessTokenIssuer
{
    /// <summary>Creates a short-lived access token; throws <c>NotConfiguredException</c> (503) when JWT is not configured.</summary>
    IssuedAccessToken IssueAccessToken(Guid userId, UserRole role, Guid? doctorId);
}
