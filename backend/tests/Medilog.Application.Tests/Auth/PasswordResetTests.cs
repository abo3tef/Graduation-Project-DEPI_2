using Medilog.Application.Common;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Features.Auth;
using Medilog.Application.Features.Auth.Contracts;
using Medilog.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Medilog.Application.Tests.Auth;

/// <summary>
/// Forgot/reset password use cases (F6-C1-T7): no account enumeration, hashed single-use expiring
/// reset tokens, purpose binding and the password policy (AGENTS.md §5).
/// </summary>
public sealed class PasswordResetTests
{
    private const string EmailAddress = "doctor@medilog.test";
    private const string NewPassword = "NewValid#Passw0rd";

    [Fact]
    public async Task Forgot_password_emails_a_reset_link_and_stores_only_the_hash()
    {
        // Arrange
        using var fixture = new AuthFixture();
        var doctor = fixture.AddDoctor(EmailAddress);

        // Act
        await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest(EmailAddress),
            CancellationToken.None);

        // Assert
        var message = Assert.Single(fixture.Email.Messages);
        Assert.Equal(EmailAddress, message.To);
        Assert.Contains("reset-password?token=", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(AuthFixture.Password, message.Body, StringComparison.Ordinal);

        var stored = await fixture.DbContext.AuthTokens.SingleAsync();
        var rawToken = fixture.LastToken();

        Assert.Equal(AuthTokenPurpose.PasswordReset, stored.Purpose);
        Assert.Equal(doctor.Id, stored.UserId);
        Assert.NotEqual(rawToken, stored.TokenHash);
        Assert.Null(stored.ConsumedAtUtc);
        Assert.Equal(fixture.Clock.GetUtcNow().AddMinutes(60), stored.ExpiresAtUtc);
    }

    [Fact]
    public async Task Forgot_password_for_an_unknown_email_completes_silently()
    {
        // Arrange
        using var fixture = new AuthFixture();

        // Act
        await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest("nobody@medilog.test"),
            CancellationToken.None);

        // Assert
        Assert.Empty(fixture.Email.Messages);
        Assert.Empty(fixture.DbContext.AuthTokens);
    }

    [Fact]
    public async Task Forgot_password_normalizes_the_email_case()
    {
        // Arrange
        using var fixture = new AuthFixture();
        fixture.AddDoctor(EmailAddress);

        // Act
        await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest("  DOCTOR@Medilog.Test "),
            CancellationToken.None);

        // Assert
        var message = Assert.Single(fixture.Email.Messages);
        Assert.Equal(EmailAddress, message.To);
    }

    [Fact]
    public async Task Reset_password_replaces_the_hash_and_the_new_password_signs_in()
    {
        // Arrange
        using var fixture = new AuthFixture();
        var doctor = fixture.AddDoctor(EmailAddress);

        await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest(EmailAddress),
            CancellationToken.None);

        var token = fixture.LastToken();

        // Act
        await fixture.Service.ResetPasswordAsync(
            new ResetPasswordRequest(token, NewPassword),
            CancellationToken.None);

        // Assert
        var updated = await fixture.DbContext.Doctors.SingleAsync();
        Assert.Equal(doctor.Id, updated.Id);
        Assert.True(fixture.Hasher.Verify(NewPassword, updated.PasswordHash));
        Assert.False(fixture.Hasher.Verify(AuthFixture.Password, updated.PasswordHash));

        var login = await fixture.Service.LoginAsync(
            new LoginRequest(EmailAddress, NewPassword),
            CancellationToken.None);
        Assert.Equal(doctor.Id, login.User.Id);

        var consumed = await fixture.DbContext.AuthTokens.SingleAsync();
        Assert.NotNull(consumed.ConsumedAtUtc);
    }

    [Fact]
    public async Task Reset_token_cannot_be_used_twice()
    {
        // Arrange
        using var fixture = new AuthFixture();
        fixture.AddDoctor(EmailAddress);

        await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest(EmailAddress),
            CancellationToken.None);

        var token = fixture.LastToken();
        await fixture.Service.ResetPasswordAsync(
            new ResetPasswordRequest(token, NewPassword),
            CancellationToken.None);

        // Act
        var failure = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => fixture.Service.ResetPasswordAsync(
                new ResetPasswordRequest(token, "Another#Passw0rd"),
                CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthTokenInvalid, failure.Code);

        var doctor = await fixture.DbContext.Doctors.SingleAsync();
        Assert.True(fixture.Hasher.Verify(NewPassword, doctor.PasswordHash));
    }

    [Fact]
    public async Task Expired_reset_token_is_rejected()
    {
        // Arrange
        using var fixture = new AuthFixture();
        fixture.AddDoctor(EmailAddress);

        await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest(EmailAddress),
            CancellationToken.None);

        var token = fixture.LastToken();

        // Act: the approved reset lifetime is 60 minutes (D-36).
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(61);

        var failure = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => fixture.Service.ResetPasswordAsync(
                new ResetPasswordRequest(token, NewPassword),
                CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthTokenInvalid, failure.Code);

        var doctor = await fixture.DbContext.Doctors.SingleAsync();
        Assert.True(fixture.Hasher.Verify(AuthFixture.Password, doctor.PasswordHash));
    }

    [Fact]
    public async Task A_verification_token_cannot_be_used_to_reset_a_password()
    {
        // Arrange
        using var fixture = new AuthFixture();
        fixture.AddDoctor(EmailAddress, verified: false);

        await fixture.Service.ResendVerificationAsync(
            new ResendVerificationRequest(EmailAddress),
            CancellationToken.None);

        var verificationToken = fixture.LastToken();

        // Act
        var failure = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => fixture.Service.ResetPasswordAsync(
                new ResetPasswordRequest(verificationToken, NewPassword),
                CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthTokenInvalid, failure.Code);
    }

    [Fact]
    public async Task Requesting_another_link_invalidates_the_previous_reset_token()
    {
        // Arrange
        using var fixture = new AuthFixture();
        fixture.AddDoctor(EmailAddress);

        await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest(EmailAddress),
            CancellationToken.None);
        var firstToken = fixture.LastToken();

        // Act
        await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest(EmailAddress),
            CancellationToken.None);
        var secondToken = fixture.LastToken();

        var reuseFailure = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => fixture.Service.ResetPasswordAsync(
                new ResetPasswordRequest(firstToken, NewPassword),
                CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthTokenInvalid, reuseFailure.Code);
        Assert.NotEqual(firstToken, secondToken);

        await fixture.Service.ResetPasswordAsync(
            new ResetPasswordRequest(secondToken, NewPassword),
            CancellationToken.None);
    }

    [Theory]
    [InlineData("short1!")]
    [InlineData("alllowercase1!")]
    [InlineData("ALLUPPERCASE1!")]
    [InlineData("NoDigitsHere!!")]
    [InlineData("NoSymbols1234")]
    public async Task Reset_rejects_a_password_that_breaks_the_policy(string password)
    {
        // Arrange
        using var fixture = new AuthFixture();
        fixture.AddDoctor(EmailAddress);

        await fixture.Service.ForgotPasswordAsync(
            new ForgotPasswordRequest(EmailAddress),
            CancellationToken.None);

        var token = fixture.LastToken();

        // Act
        var failure = await Assert.ThrowsAsync<RequestValidationException>(
            () => fixture.Service.ResetPasswordAsync(
                new ResetPasswordRequest(token, password),
                CancellationToken.None));

        // Assert
        Assert.Contains("NewPassword", failure.Errors.Keys);

        // The token is still usable: a rejected request must not consume it.
        var stored = await fixture.DbContext.AuthTokens.SingleAsync();
        Assert.Null(stored.ConsumedAtUtc);
    }

    [Fact]
    public async Task Reset_does_not_reveal_anything_for_an_unknown_token()
    {
        // Arrange
        using var fixture = new AuthFixture();
        var doctor = fixture.AddDoctor(EmailAddress);

        // Act
        var failure = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => fixture.Service.ResetPasswordAsync(
                new ResetPasswordRequest("not-a-real-token", NewPassword),
                CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthTokenInvalid, failure.Code);
        Assert.Equal("The link is invalid or has expired.", failure.Message);

        var unchanged = await fixture.DbContext.Doctors.SingleAsync();
        Assert.Equal(doctor.PasswordHash, unchanged.PasswordHash);
    }
}
