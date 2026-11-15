using Medilog.Application.Common;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Features.Auth.Contracts;
using Medilog.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Medilog.Application.Tests.Auth;

/// <summary>
/// Google Sign-In use cases (F6-C1-T8, decision Q-12): only an existing, email-verified account can
/// sign in, the role always comes from the account, and no account is ever created here.
/// </summary>
public sealed class GoogleSignInTests
{
    private const string GoogleToken = "firebase-id-token";

    [Fact]
    public async Task Verified_doctor_with_a_matching_email_receives_a_token_for_its_own_account()
    {
        // Arrange
        using var fixture = new AuthFixture();
        var doctor = fixture.AddDoctor("doctor@medilog.test");
        fixture.GoogleValidator.NextIdentity = new GoogleTokenIdentity(
            "google-subject",
            "Doctor@Medilog.Test",
            EmailVerified: true);

        // Act
        var response = await fixture.Service.SignInWithGoogleAsync(
            new GoogleSignInRequest(GoogleToken),
            CancellationToken.None);

        // Assert
        Assert.Equal(doctor.Id, response.User.Id);
        Assert.Equal(UserRole.Doctor, response.User.Role);
        Assert.Equal(doctor.Id, response.User.DoctorId);
        Assert.Equal(doctor.Email, response.User.Email);
        Assert.Equal(GoogleToken, Assert.Single(fixture.GoogleValidator.ReceivedTokens));

        var issued = Assert.Single(fixture.TokenIssuer.Calls);
        Assert.Equal(doctor.Id, issued.UserId);
        Assert.Equal(UserRole.Doctor, issued.Role);
        Assert.Equal(doctor.Id, issued.DoctorId);
    }

    [Fact]
    public async Task Google_user_never_gets_a_role_from_the_request()
    {
        // Arrange: the request only carries the token, and the account decides the role.
        using var fixture = new AuthFixture();
        var secretary = fixture.AddSecretary("secretary@medilog.test");
        fixture.GoogleValidator.NextIdentity = new GoogleTokenIdentity(
            "google-subject",
            "secretary@medilog.test",
            EmailVerified: true);

        // Act
        var response = await fixture.Service.SignInWithGoogleAsync(
            new GoogleSignInRequest(GoogleToken),
            CancellationToken.None);

        // Assert
        Assert.Equal(UserRole.Secretary, response.User.Role);
        Assert.Null(response.User.DoctorId);
        Assert.Equal(secretary.Id, response.User.Id);
    }

    [Fact]
    public async Task Unknown_email_is_rejected_and_no_account_is_created()
    {
        // Arrange
        using var fixture = new AuthFixture();
        fixture.GoogleValidator.NextIdentity = new GoogleTokenIdentity(
            "google-subject",
            "nobody@medilog.test",
            EmailVerified: true);

        // Act
        var failure = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => fixture.Service.SignInWithGoogleAsync(
                new GoogleSignInRequest(GoogleToken),
                CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthInvalidGoogleToken, failure.Code);
        Assert.Empty(fixture.DbContext.Doctors);
        Assert.Empty(fixture.DbContext.Secretaries);
        Assert.Empty(fixture.DbContext.Admins);
        Assert.Empty(fixture.TokenIssuer.Calls);
    }

    [Fact]
    public async Task Google_address_that_google_has_not_verified_is_rejected()
    {
        // Arrange
        using var fixture = new AuthFixture();
        fixture.AddDoctor("doctor@medilog.test");
        fixture.GoogleValidator.NextIdentity = new GoogleTokenIdentity(
            "google-subject",
            "doctor@medilog.test",
            EmailVerified: false);

        // Act
        var failure = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => fixture.Service.SignInWithGoogleAsync(
                new GoogleSignInRequest(GoogleToken),
                CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthInvalidGoogleToken, failure.Code);
        Assert.Empty(fixture.TokenIssuer.Calls);
    }

    [Fact]
    public async Task Unverified_medilog_account_cannot_sign_in_with_google()
    {
        // Arrange
        using var fixture = new AuthFixture();
        fixture.AddDoctor("doctor@medilog.test", verified: false);
        fixture.GoogleValidator.NextIdentity = new GoogleTokenIdentity(
            "google-subject",
            "doctor@medilog.test",
            EmailVerified: true);

        // Act
        var failure = await Assert.ThrowsAsync<ForbiddenException>(
            () => fixture.Service.SignInWithGoogleAsync(
                new GoogleSignInRequest(GoogleToken),
                CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthEmailUnverified, failure.Code);
        Assert.Empty(fixture.TokenIssuer.Calls);
    }

    [Fact]
    public async Task Inactive_account_cannot_sign_in_with_google()
    {
        // Arrange
        using var fixture = new AuthFixture();
        fixture.AddDoctor("doctor@medilog.test", status: AccountStatus.Inactive);
        fixture.GoogleValidator.NextIdentity = new GoogleTokenIdentity(
            "google-subject",
            "doctor@medilog.test",
            EmailVerified: true);

        // Act
        var failure = await Assert.ThrowsAsync<ForbiddenException>(
            () => fixture.Service.SignInWithGoogleAsync(
                new GoogleSignInRequest(GoogleToken),
                CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthAccountInactive, failure.Code);
        Assert.Empty(fixture.TokenIssuer.Calls);
    }

    [Fact]
    public async Task Invalid_google_token_is_rejected()
    {
        // Arrange
        using var fixture = new AuthFixture();
        fixture.AddDoctor("doctor@medilog.test");
        fixture.GoogleValidator.NextIdentity = null;

        // Act
        var failure = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => fixture.Service.SignInWithGoogleAsync(
                new GoogleSignInRequest(GoogleToken),
                CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthInvalidGoogleToken, failure.Code);
    }

    [Fact]
    public async Task Empty_token_is_rejected_before_validation()
    {
        // Arrange
        using var fixture = new AuthFixture();
        fixture.GoogleValidator.NextIdentity = new GoogleTokenIdentity(
            "google-subject",
            "doctor@medilog.test",
            EmailVerified: true);

        // Act
        var failure = await Assert.ThrowsAsync<RequestValidationException>(
            () => fixture.Service.SignInWithGoogleAsync(
                new GoogleSignInRequest("  "),
                CancellationToken.None));

        // Assert
        Assert.Contains("IdToken", failure.Errors.Keys);
        Assert.Empty(fixture.GoogleValidator.ReceivedTokens);
    }

    [Fact]
    public async Task Google_sign_in_never_touches_the_password_hash()
    {
        // Arrange
        using var fixture = new AuthFixture();
        var doctor = fixture.AddDoctor("doctor@medilog.test");
        var hashBefore = doctor.PasswordHash;
        fixture.GoogleValidator.NextIdentity = new GoogleTokenIdentity(
            "google-subject",
            "doctor@medilog.test",
            EmailVerified: true);

        // Act
        await fixture.Service.SignInWithGoogleAsync(
            new GoogleSignInRequest(GoogleToken),
            CancellationToken.None);

        // Assert
        var stored = await fixture.DbContext.Doctors.SingleAsync();
        Assert.Equal(hashBefore, stored.PasswordHash);
    }
}
