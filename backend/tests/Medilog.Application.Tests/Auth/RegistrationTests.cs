using Medilog.Application.Common;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Features.Auth.Contracts;
using Medilog.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Medilog.Application.Tests.Auth;

/// <summary>
/// Registration use cases (F6-C1-T4): role allow-list, profile creation, unique email, password
/// hashing, initial account state and the verification email (Q-11, AGENTS.md §5).
/// </summary>
public sealed class RegistrationTests
{
    [Fact]
    public async Task Register_doctor_creates_an_unverified_inactive_profile_and_emails_a_link()
    {
        // Arrange
        using var fixture = new AuthFixture();
        var request = new RegisterRequest(
            "  New Doctor  ",
            "  New.Doctor@Medilog.Test ",
            "Valid#Passw0rd",
            " 0100000000 ",
            UserRole.Doctor,
            "  Cardiology  ");

        // Act
        var response = await fixture.Service.RegisterAsync(request, CancellationToken.None);

        // Assert
        var doctor = await fixture.DbContext.Doctors.SingleAsync();

        Assert.Equal(doctor.Id, response.Id);
        Assert.Equal("New Doctor", doctor.FullName);
        Assert.Equal("new.doctor@medilog.test", doctor.Email);
        Assert.Equal("0100000000", doctor.Phone);
        Assert.Equal("Cardiology", doctor.Specialization);
        Assert.Equal(AccountStatus.Inactive, doctor.AccountStatus);
        Assert.Null(doctor.EmailVerifiedAtUtc);
        Assert.NotEqual("Valid#Passw0rd", doctor.PasswordHash);
        Assert.True(fixture.Hasher.Verify("Valid#Passw0rd", doctor.PasswordHash));

        Assert.Equal(UserRole.Doctor, response.Role);
        Assert.Equal(doctor.Id, response.DoctorId);
        Assert.Equal("new.doctor@medilog.test", response.Email);

        // No access token is issued before the email address is confirmed.
        Assert.Empty(fixture.TokenIssuer.Calls);

        var message = Assert.Single(fixture.Email.Messages);
        Assert.Equal("new.doctor@medilog.test", message.To);
        Assert.Contains("verify-email?token=", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Valid#Passw0rd", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_stores_only_a_hashed_verification_token_that_expires_in_24_hours()
    {
        // Arrange
        using var fixture = new AuthFixture();

        // Act
        await fixture.Service.RegisterAsync(
            new RegisterRequest("New Doctor", "new.doctor@medilog.test", "Valid#Passw0rd", null, UserRole.Doctor, "General Medicine"),
            CancellationToken.None);

        var rawToken = fixture.LastToken();

        // Assert
        var stored = await fixture.DbContext.AuthTokens.SingleAsync();

        Assert.Equal(AuthTokenPurpose.EmailVerification, stored.Purpose);
        Assert.Equal(UserType.Doctor, stored.UserType);
        Assert.NotEqual(rawToken, stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);
        Assert.DoesNotContain(rawToken, stored.TokenHash, StringComparison.Ordinal);
        Assert.Null(stored.ConsumedAtUtc);
        Assert.Equal(fixture.Clock.GetUtcNow().AddHours(24), stored.ExpiresAtUtc);
    }

    [Fact]
    public async Task Register_secretary_creates_a_secretary_profile_without_doctor_id()
    {
        // Arrange
        using var fixture = new AuthFixture();

        // Act
        var response = await fixture.Service.RegisterAsync(
            new RegisterRequest("New Secretary", "sec@medilog.test", "Valid#Passw0rd", null, UserRole.Secretary, null),
            CancellationToken.None);

        // Assert
        var secretary = await fixture.DbContext.Secretaries.SingleAsync();

        Assert.Equal(secretary.Id, response.Id);
        Assert.Equal(UserRole.Secretary, response.Role);
        Assert.Null(response.DoctorId);
        Assert.Equal(AccountStatus.Inactive, secretary.AccountStatus);
        Assert.Null(secretary.EmailVerifiedAtUtc);
        Assert.Empty(fixture.DbContext.Doctors);
    }

    [Fact]
    public async Task Email_that_already_has_a_doctor_account_is_rejected_with_409()
    {
        // Arrange
        using var fixture = new AuthFixture();
        fixture.AddDoctor("taken@medilog.test");
        var request = new RegisterRequest(
            "Another Person",
            "Taken@Medilog.Test",
            "Valid#Passw0rd",
            null,
            UserRole.Secretary,
            null);

        // Act
        var failure = await Assert.ThrowsAsync<ConflictException>(
            () => fixture.Service.RegisterAsync(request, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthEmailAlreadyRegistered, failure.Code);
        Assert.Empty(fixture.DbContext.Secretaries);
        Assert.Empty(fixture.Email.Messages);
    }

    [Fact]
    public async Task Email_lookup_is_case_insensitive_when_checking_for_a_duplicate()
    {
        // Arrange
        using var fixture = new AuthFixture();
        fixture.AddSecretary("Secretary@Medilog.Test");

        // Act
        var failure = await Assert.ThrowsAsync<ConflictException>(
            () => fixture.Service.RegisterAsync(
                new RegisterRequest("Person", "secretary@medilog.test", "Valid#Passw0rd", null, UserRole.Doctor, "General Medicine"),
                CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthEmailAlreadyRegistered, failure.Code);
        Assert.Empty(fixture.DbContext.Doctors);
    }

    [Fact]
    public async Task Admin_role_can_never_be_registered()
    {
        // Arrange
        using var fixture = new AuthFixture();

        // Act
        var failure = await Assert.ThrowsAsync<RequestValidationException>(
            () => fixture.Service.RegisterAsync(
                new RegisterRequest("Sneaky Admin", "admin@medilog.test", "Valid#Passw0rd", null, UserRole.Admin, null),
                CancellationToken.None));

        // Assert
        Assert.Contains("Role", failure.Errors.Keys);
        Assert.Empty(fixture.DbContext.Admins);
        Assert.Empty(fixture.Email.Messages);
    }

    [Theory]
    [InlineData("short1!")]
    [InlineData("alllowercase1!")]
    [InlineData("ALLUPPERCASE1!")]
    [InlineData("NoDigitsHere!!")]
    [InlineData("NoSymbols1234")]
    public async Task Weak_passwords_are_rejected_by_the_policy(string password)
    {
        // Arrange
        using var fixture = new AuthFixture();

        // Act
        var failure = await Assert.ThrowsAsync<RequestValidationException>(
            () => fixture.Service.RegisterAsync(
                new RegisterRequest("New Doctor", "new.doctor@medilog.test", password, null, UserRole.Doctor, "General Medicine"),
                CancellationToken.None));

        // Assert
        Assert.Contains("Password", failure.Errors.Keys);
        Assert.Empty(fixture.DbContext.Doctors);
        Assert.Empty(fixture.Email.Messages);
    }

    [Fact]
    public async Task Registered_account_cannot_sign_in_before_verifying_its_email()
    {
        // Arrange
        using var fixture = new AuthFixture();
        await fixture.Service.RegisterAsync(
            new RegisterRequest("New Doctor", "new.doctor@medilog.test", "Valid#Passw0rd", null, UserRole.Doctor, "General Medicine"),
            CancellationToken.None);

        // Act
        var failure = await Assert.ThrowsAsync<ForbiddenException>(
            () => fixture.Service.LoginAsync(
                new LoginRequest("new.doctor@medilog.test", "Valid#Passw0rd"),
                CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthEmailUnverified, failure.Code);
        Assert.Empty(fixture.TokenIssuer.Calls);
    }
}
