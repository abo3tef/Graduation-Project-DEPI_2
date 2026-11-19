using Medilog.Infrastructure.Email;

namespace Medilog.Application.Tests.Email;

/// <summary>
/// The emailed verification link carries the address only as a resend-form convenience; the token
/// remains the single source of authority (AGENTS.md §5).
/// </summary>
public sealed class AuthLinkBuilderTests
{
    private readonly AuthLinkBuilder _builder = new(new AuthLinkOptions
    {
        AppBaseUrl = "https://app.medilog.test/"
    });

    [Fact]
    public void Verification_link_without_an_email_keeps_the_original_token_only_shape()
    {
        // Act
        var link = _builder.BuildEmailVerificationLink("token with+space");

        // Assert
        Assert.Equal(
            "https://app.medilog.test/verify-email?token=token%20with%2Bspace",
            link);
    }

    [Fact]
    public void Verification_link_with_an_email_url_encodes_the_address_for_the_resend_form()
    {
        // Act
        var link = _builder.BuildEmailVerificationLink("raw-token", "New.Doctor@Medilog.Test");

        // Assert
        Assert.Equal(
            "https://app.medilog.test/verify-email?token=raw-token&email=New.Doctor%40Medilog.Test",
            link);
    }

    [Fact]
    public void Password_reset_link_never_carries_an_email()
    {
        // Act
        var link = _builder.BuildPasswordResetLink("raw-token");

        // Assert
        Assert.Equal("https://app.medilog.test/reset-password?token=raw-token", link);
    }
}
