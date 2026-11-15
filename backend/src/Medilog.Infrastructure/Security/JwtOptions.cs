using System.Text;
using Microsoft.Extensions.Configuration;

namespace Medilog.Infrastructure.Security;

/// <summary>
/// Configuration for issuing and validating Medilog access tokens (decisions D-35/D-36).
/// </summary>
/// <remarks>
/// <para>
/// Only the signing key is secret. It is read from user-secrets or an environment variable and is
/// never committed (AGENTS.md §5). Everything else is ordinary configuration.
/// </para>
/// <para>
/// The type is bound manually from the <c>Jwt</c> section so that no additional configuration
/// package is required (decision D-41), and the same helper is used by both the token issuer and
/// the bearer validation setup so the two can never disagree.
/// </para>
/// </remarks>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Minimum key length accepted by HMAC-SHA256 (256 bits).</summary>
    public const int MinimumSigningKeyBytes = 32;

    private const int DefaultAccessTokenMinutes = 1440;

    public string Issuer { get; set; } = "medilog";

    public string Audience { get; set; } = "medilog-client";

    /// <summary>Secret HMAC key. Must be at least 32 bytes; never commit a real value.</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Access-token lifetime in minutes; 1440 (24 h) is the approved demo value.</summary>
    public int AccessTokenMinutes { get; set; } = DefaultAccessTokenMinutes;

    /// <summary>True when the values are sufficient to issue and validate a token.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Issuer) &&
        !string.IsNullOrWhiteSpace(Audience) &&
        AccessTokenMinutes > 0 &&
        SigningKeyBytes >= MinimumSigningKeyBytes;

    /// <summary>Length of the configured key in bytes, using the encoding the issuer signs with.</summary>
    public int SigningKeyBytes => Encoding.UTF8.GetByteCount(SigningKey ?? string.Empty);

    /// <summary>Reads the <c>Jwt</c> section, applying the approved defaults.</summary>
    public static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);

        return new JwtOptions
        {
            Issuer = section["Issuer"] ?? "medilog",
            Audience = section["Audience"] ?? "medilog-client",
            SigningKey = section["SigningKey"] ?? string.Empty,
            AccessTokenMinutes = int.TryParse(section["AccessTokenMinutes"], out var minutes)
                ? minutes
                : DefaultAccessTokenMinutes,
        };
    }
}
