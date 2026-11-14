using Medilog.Application.Common;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Features.Auth.Contracts;
using Medilog.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Medilog.Application.Tests.Auth;

/// <summary>
/// Email-verification and resend use cases (F6-C1-T6): single-use tokens, expiry, purpose binding,
/// previous-token invalidation and the no-enumeration response (AGENTS.md §5, Q-11).
/// </summary>
public sealed class EmailVerificationTests
{
    private const string EmailAddress = "new.doctor@medilog.test";

    [Fact]
    public async Task Valid_token_activates_the_account_and_allows_sign_in()
    {
        // Arrange
        using var fixture = new AuthFixture();
        await RegisterDoctorAsync(fixture);
        var token = fixture.LastToken();

        // Act
        await fixture.Service.VerifyEmailAsync(new VerifyEmailRequest(token), CancellationToken.None);

        // Assert
        var doctor = await fixture.DbContext.Doctors.SingleAsync();
        Assert.Equal(AccountStatus.Active, doctor.AccountStatus);
        Assert.Equal(fixture.Clock.GetUtcNow(), doctor.EmailVerifiedAtUtc);

        var stored = await fixture.DbContext.AuthTokens.SingleAsync();
        Assert.NotNull(stored.ConsumedAtUtc);

        var login = await fixture.Service.LoginAsync(
            new LoginRequest(EmailAddress, AuthFixture.Password),
            CancellationToken.None);

        Assert.Equal(UserRole.Doctor, login.User.Role);
        Assert.Equal(doctor.Id, login.User.DoctorId);
    }

    [Fact]
    public async Task Token_cannot_be_used_twice()
    {
        // Arrange
        using var fixture = new AuthFixture();
        await RegisterDoctorAsync(fixture);
        var token = fixture.LastToken();
        await fixture.Service.VerifyEmailAsync(new VerifyEmailRequest(token), CancellationToken.None);

        // Act
        var failure = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => fixture.Service.VerifyEmailAsync(new VerifyEmailRequest(token), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthTokenInvalid, failure.Code);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        // Arrange
        using var fixture = new AuthFixture();
        await RegisterDoctorAsync(fixture);
        var token = fixture.LastToken();

        // Act: the approved verification lifetime is 24 hours (D-36).
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddHours(24).AddMinutes(1);

        var failure = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => fixture.Service.VerifyEmailAsync(new VerifyEmailRequest(token), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthTokenInvalid, failure.Code);

        var doctor = await fixture.DbContext.Doctors.SingleAsync();
        Assert.Equal(AccountStatus.Inactive, doctor.AccountStatus);
        Assert.Null(doctor.EmailVerifiedAtUtc);
    }

    [Fact]
    public async Task Unknown_token_is_rejected_without_disclosing_why()
    {
        // Arrange
        using var fixture = new AuthFixture();
        await RegisterDoctorAsync(fixture);

        // Act
        var failure = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => fixture.Service.VerifyEmailAsync(
                new VerifyEmailRequest("not-a-real-token"),
                CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthTokenInvalid, failure.Code);
    }

    [Fact]
    public async Task Password_reset_token_cannot_be_used_to_verify_an_email()
    {
        // Arrange
        using var fixture = new AuthFixture();
        var doctor = fixture.AddDoctor(EmailAddress);

        await fixture.TokenService.IssuePasswordResetAsync(
            doctor.Id,
            UserType.Doctor,
            doctor.FullName,
            doctor.Email,
            CancellationToken.None);

        var resetToken = fixture.LastToken();

        // Act
        var failure = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => fixture.Service.VerifyEmailAsync(new VerifyEmailRequest(resetToken), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthTokenInvalid, failure.Code);
    }

    [Fact]
    public async Task Resending_invalidates_the_previous_link_and_issues_a_new_one()
    {
        // Arrange
        using var fixture = new AuthFixture();
        await RegisterDoctorAsync(fixture);
        var firstToken = fixture.LastToken();

        // Act
        await fixture.Service.ResendVerificationAsync(
            new ResendVerificationRequest(EmailAddress),
            CancellationToken.None);

        var secondToken = fixture.LastToken();

        // Assert
        Assert.Equal(2, fixture.Email.Messages.Count);
        Assert.NotEqual(firstToken, secondToken);

        var tokens = await fixture.DbContext.AuthTokens
            .OrderBy(token => token.ExpiresAtUtc)
            .ToListAsync();

        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens, token => token.ConsumedAtUtc is null);

        var reuseFailure = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => fixture.Service.VerifyEmailAsync(new VerifyEmailRequest(firstToken), CancellationToken.None));
        Assert.Equal(ErrorCodes.AuthTokenInvalid, reuseFailure.Code);

        await fixture.Service.VerifyEmailAsync(new VerifyEmailRequest(secondToken), CancellationToken.None);
    }

    [Fact]
    public async Task Resend_for_an_unknown_email_completes_silently()
    {
        // Arrange
        using var fixture = new AuthFixture();

        // Act
        await fixture.Service.ResendVerificationAsync(
            new ResendVerificationRequest("nobody@medilog.test"),
            CancellationToken.None);

        // Assert
        Assert.Empty(fixture.Email.Messages);
        Assert.Empty(fixture.DbContext.AuthTokens);
    }

    [Fact]
    public async Task Resend_for_an_already_verified_account_sends_nothing()
    {
        // Arrange
        using var fixture = new AuthFixture();
        fixture.AddDoctor("verified@medilog.test", verified: true);

        // Act
        await fixture.Service.ResendVerificationAsync(
            new ResendVerificationRequest("verified@medilog.test"),
            CancellationToken.None);

        // Assert
        Assert.Empty(fixture.Email.Messages);
        Assert.Empty(fixture.DbContext.AuthTokens);
    }

    [Fact]
    public async Task Resend_normalizes_the_email_case()
    {
        // Arrange
        using var fixture = new AuthFixture();
        await RegisterDoctorAsync(fixture);

        // Act
        await fixture.Service.ResendVerificationAsync(
            new ResendVerificationRequest("  NEW.DOCTOR@Medilog.Test "),
            CancellationToken.None);

        // Assert
        Assert.Equal(2, fixture.Email.Messages.Count);
        Assert.All(fixture.Email.Messages, message => Assert.Equal(EmailAddress, message.To));
    }

    [Fact]
    public async Task Empty_token_is_rejected_by_the_validator()
    {
        // Arrange
        using var fixture = new AuthFixture();

        // Act
        var failure = await Assert.ThrowsAsync<RequestValidationException>(
            () => fixture.Service.VerifyEmailAsync(new VerifyEmailRequest("  "), CancellationToken.None));

        // Assert
        Assert.Contains("Token", failure.Errors.Keys);
    }

    private static Task RegisterDoctorAsync(AuthFixture fixture) =>
        fixture.Service.RegisterAsync(
            new RegisterRequest("New Doctor", EmailAddress, AuthFixture.Password, null, UserRole.Doctor, null),
            CancellationToken.None);
}
