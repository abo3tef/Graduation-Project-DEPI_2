using System.Text;
using Medilog.Application.Common;
using Medilog.Application.Common.Exceptions;
using Medilog.Infrastructure.Security;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Medilog.Application.Tests.Auth;

/// <summary>
/// Verifies the access-token claims contract (decision D-35) and HS256 signing/validation
/// (decisions D-36/D-41) with the real issuer.
/// </summary>
public sealed class ClaimsContractTests
{
    private const string SigningKey = "unit-test-signing-key-0123456789abcdef";

    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Doctor_token_carries_sub_role_and_a_matching_doctor_id()
    {
        // Arrange
        var clock = new FakeTimeProvider(Now);
        var issuer = new JwtAccessTokenIssuer(CreateOptions(), clock);
        var doctorId = Guid.NewGuid();

        // Act
        var issued = issuer.IssueAccessToken(doctorId, UserRole.Doctor, doctorId);
        var token = new JsonWebToken(issued.Token);

        // Assert
        Assert.Equal(doctorId.ToString(), ClaimValue(token, "sub"));
        Assert.Equal("Doctor", ClaimValue(token, "role"));
        Assert.Equal(doctorId.ToString(), ClaimValue(token, "doctorId"));
        Assert.False(string.IsNullOrWhiteSpace(ClaimValue(token, "jti")));
        Assert.Equal("medilog-tests", token.Issuer);
        Assert.Contains("medilog-client", token.Audiences);
        Assert.Equal(Now.AddMinutes(1440), issued.ExpiresAtUtc);
        Assert.Equal(TimeSpan.FromMinutes(1440), token.ValidTo - token.IssuedAt);
    }

    [Theory]
    [InlineData(UserRole.Secretary)]
    [InlineData(UserRole.Admin)]
    public void Non_doctor_tokens_have_no_doctor_id_claim(UserRole role)
    {
        // Arrange
        var clock = new FakeTimeProvider(Now);
        var issuer = new JwtAccessTokenIssuer(CreateOptions(), clock);
        var userId = Guid.NewGuid();

        // Act
        var issued = issuer.IssueAccessToken(userId, role, doctorId: null);
        var token = new JsonWebToken(issued.Token);

        // Assert
        Assert.Equal(role.ToString(), ClaimValue(token, "role"));
        Assert.False(token.TryGetClaim("doctorId", out _));
    }

    [Fact]
    public async Task Token_validates_with_the_signing_key_and_fails_with_another_key()
    {
        // Arrange
        var clock = new FakeTimeProvider(Now);
        var issuer = new JwtAccessTokenIssuer(CreateOptions(), clock);
        var issued = issuer.IssueAccessToken(Guid.NewGuid(), UserRole.Doctor, Guid.NewGuid());
        var handler = new JsonWebTokenHandler();

        // Act: this test is about the signature, so lifetime is not validated. Otherwise the fixed
        // issue time makes the result depend on the calendar (expiry has its own test below).
        var withCorrectKey = await handler.ValidateTokenAsync(
            issued.Token,
            ValidationParameters(SigningKey, validateLifetime: false));
        var withWrongKey = await handler.ValidateTokenAsync(
            issued.Token,
            ValidationParameters("another-signing-key-0123456789abcdef"));

        // Assert
        Assert.True(withCorrectKey.IsValid);
        Assert.False(withWrongKey.IsValid);
    }

    [Fact]
    public async Task Expired_token_no_longer_validates()
    {
        // Arrange: a clock 48 hours in the past means a 24-hour token is already expired.
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow.AddHours(-48));
        var issuer = new JwtAccessTokenIssuer(CreateOptions(), clock);
        var issued = issuer.IssueAccessToken(Guid.NewGuid(), UserRole.Doctor, Guid.NewGuid());
        var handler = new JsonWebTokenHandler();

        // Act
        var result = await handler.ValidateTokenAsync(issued.Token, ValidationParameters(SigningKey));

        // Assert
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    public void Issuing_fails_with_not_configured_when_the_key_is_missing_or_weak(string signingKey)
    {
        // Arrange
        var options = new JwtOptions
        {
            Issuer = "medilog-tests",
            Audience = "medilog-client",
            SigningKey = signingKey,
            AccessTokenMinutes = 1440,
        };

        var issuer = new JwtAccessTokenIssuer(options, new FakeTimeProvider(Now));

        // Act
        var failure = Assert.Throws<NotConfiguredException>(
            () => issuer.IssueAccessToken(Guid.NewGuid(), UserRole.Doctor, Guid.NewGuid()));

        // Assert
        Assert.Equal(ErrorCodes.AuthNotConfigured, failure.Code);
    }

    [Fact]
    public void Issuing_rejects_an_empty_account_id()
    {
        // Arrange
        var issuer = new JwtAccessTokenIssuer(CreateOptions(), new FakeTimeProvider(Now));

        // Act / Assert
        Assert.Throws<NotConfiguredException>(
            () => issuer.IssueAccessToken(Guid.Empty, UserRole.Doctor, null));
    }

    private static JwtOptions CreateOptions() => new()
    {
        Issuer = "medilog-tests",
        Audience = "medilog-client",
        SigningKey = SigningKey,
        AccessTokenMinutes = 1440,
    };

    private static TokenValidationParameters ValidationParameters(
        string signingKey,
        bool validateLifetime = true) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = "medilog-tests",
        ValidateAudience = true,
        ValidAudience = "medilog-client",
        ValidateLifetime = validateLifetime,
        ClockSkew = TimeSpan.Zero,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
    };

    private static string ClaimValue(JsonWebToken token, string claimType) =>
        token.TryGetClaim(claimType, out var claim) ? claim.Value : string.Empty;
}
