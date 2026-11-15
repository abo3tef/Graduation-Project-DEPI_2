using Medilog.Application.Common.Exceptions;
using Medilog.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Medilog.Application.Tests.Auth;

/// <summary>
/// The parts of the Firebase token validator that can be tested without a Firebase project or
/// network access (F6-C1-T8, decision Q-12).
/// </summary>
/// <remarks>
/// Signature, issuer, audience and expiry are handled by the framework JWT handler against Google's
/// published certificates, so those paths are exercised through the fake validator in
/// <see cref="GoogleSignInTests"/> instead of a real token.
/// </remarks>
public sealed class FirebaseGoogleTokenValidatorTests
{
    [Fact]
    public async Task Missing_project_id_fails_with_google_not_configured()
    {
        // Arrange: no Firebase project is configured for the demo/test environments.
        using var validator = CreateValidator(new FirebaseOptions { ProjectId = string.Empty });

        // Act
        var failure = await Assert.ThrowsAsync<NotConfiguredException>(
            () => validator.ValidateAsync("any-token", CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthGoogleNotConfigured, failure.Code);
    }

    [Theory]
    [InlineData("not-a-token")]
    [InlineData("a.b.c")]
    [InlineData("")]
    public async Task Malformed_token_is_rejected_as_invalid(string idToken)
    {
        // Arrange: the token is read before any key is fetched, so this needs no network.
        using var validator = CreateValidator(new FirebaseOptions { ProjectId = "medilog-demo" });

        // Act
        var failure = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => validator.ValidateAsync(idToken, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthInvalidGoogleToken, failure.Code);
    }

    [Fact]
    public async Task Token_without_a_key_id_is_rejected_as_invalid()
    {
        // Arrange: a structurally valid JWT that Google could not have signed (no kid header).
        using var validator = CreateValidator(new FirebaseOptions { ProjectId = "medilog-demo" });
        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "https://securetoken.google.com/medilog-demo",
            Audience = "medilog-demo",
            Claims = new Dictionary<string, object> { ["sub"] = "google-subject" },
        });

        // Act
        var failure = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => validator.ValidateAsync(token, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthInvalidGoogleToken, failure.Code);
    }

    private static FirebaseGoogleTokenValidator CreateValidator(FirebaseOptions options) =>
        new(options, new FixedClock(DateTimeOffset.UtcNow), NullLogger<FirebaseGoogleTokenValidator>.Instance);

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedClock(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
