using Medilog.Application.Common;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Features.Auth;
using Medilog.Application.Features.Auth.Contracts;
using Medilog.Domain.Enums;

namespace Medilog.Application.Tests.Auth;

/// <summary>
/// Sign-in use cases: credentials, account state and the identity handed to the token issuer
/// (F6-C1-T3, Q-11, Q-14).
/// </summary>
public sealed class LoginTests
{
    [Fact]
    public async Task Login_with_verified_doctor_returns_doctor_identity_and_doctor_id()
    {
        // Arrange
        using var fixture = new AuthFixture();
        var doctor = fixture.AddDoctor();
        var request = new LoginRequest(doctor.Email, AuthFixture.Password);

        // Act
        var response = await fixture.Service.LoginAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(doctor.Id, response.User.Id);
        Assert.Equal(UserRole.Doctor, response.User.Role);
        Assert.Equal(doctor.Id, response.User.DoctorId);
        Assert.Equal(doctor.Email, response.User.Email);
        Assert.Equal(fixture.Clock.GetUtcNow().AddMinutes(1440), response.ExpiresAtUtc);
        Assert.Single(fixture.TokenIssuer.Calls);
        Assert.Equal((doctor.Id, UserRole.Doctor, (Guid?)doctor.Id), fixture.TokenIssuer.Calls[0]);
    }

    [Fact]
    public async Task Login_with_secretary_returns_secretary_role_without_doctor_id()
    {
        // Arrange
        using var fixture = new AuthFixture();
        var secretary = fixture.AddSecretary();
        var request = new LoginRequest(secretary.Email, AuthFixture.Password);

        // Act
        var response = await fixture.Service.LoginAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(UserRole.Secretary, response.User.Role);
        Assert.Null(response.User.DoctorId);
        Assert.Equal((secretary.Id, UserRole.Secretary, (Guid?)null), fixture.TokenIssuer.Calls[0]);
    }

    [Fact]
    public async Task Login_with_admin_returns_admin_role_without_doctor_id()
    {
        // Arrange
        using var fixture = new AuthFixture();
        var admin = fixture.AddAdmin();
        var request = new LoginRequest(admin.Email, AuthFixture.Password);

        // Act
        var response = await fixture.Service.LoginAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(UserRole.Admin, response.User.Role);
        Assert.Null(response.User.DoctorId);
    }

    [Fact]
    public async Task Login_normalizes_the_email_case_and_surrounding_whitespace()
    {
        // Arrange
        using var fixture = new AuthFixture();
        var doctor = fixture.AddDoctor("doctor@medilog.test");
        var request = new LoginRequest("  Doctor@Medilog.Test  ", AuthFixture.Password);

        // Act
        var response = await fixture.Service.LoginAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(doctor.Id, response.User.Id);
        Assert.Equal("doctor@medilog.test", response.User.Email);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_produce_the_identical_failure()
    {
        // Arrange
        using var fixture = new AuthFixture();
        var doctor = fixture.AddDoctor();
        var wrongPassword = new LoginRequest(doctor.Email, "Wrong#Passw0rd");
        var unknownEmail = new LoginRequest("nobody@medilog.test", AuthFixture.Password);

        // Act
        var passwordFailure = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => fixture.Service.LoginAsync(wrongPassword, CancellationToken.None));
        var emailFailure = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => fixture.Service.LoginAsync(unknownEmail, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, passwordFailure.Code);
        Assert.Equal(passwordFailure.Code, emailFailure.Code);
        Assert.Equal(passwordFailure.Message, emailFailure.Message);
        Assert.Empty(fixture.TokenIssuer.Calls);
    }

    [Fact]
    public async Task Inactive_account_cannot_sign_in_even_with_the_right_password()
    {
        // Arrange
        using var fixture = new AuthFixture();
        var doctor = fixture.AddDoctor(status: AccountStatus.Inactive);
        var request = new LoginRequest(doctor.Email, AuthFixture.Password);

        // Act
        var failure = await Assert.ThrowsAsync<ForbiddenException>(
            () => fixture.Service.LoginAsync(request, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthAccountInactive, failure.Code);
        Assert.Empty(fixture.TokenIssuer.Calls);
    }

    [Fact]
    public async Task Unverified_account_cannot_sign_in_even_with_the_right_password()
    {
        // Arrange
        using var fixture = new AuthFixture();
        var doctor = fixture.AddDoctor(verified: false);
        var request = new LoginRequest(doctor.Email, AuthFixture.Password);

        // Act
        var failure = await Assert.ThrowsAsync<ForbiddenException>(
            () => fixture.Service.LoginAsync(request, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthEmailUnverified, failure.Code);
        Assert.Empty(fixture.TokenIssuer.Calls);
    }

    [Fact]
    public async Task Account_state_is_not_disclosed_when_the_password_is_wrong()
    {
        // Arrange
        using var fixture = new AuthFixture();
        var doctor = fixture.AddDoctor(status: AccountStatus.Inactive);
        var request = new LoginRequest(doctor.Email, "Wrong#Passw0rd");

        // Act
        var failure = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => fixture.Service.LoginAsync(request, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, failure.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public async Task Invalid_email_shape_is_rejected_before_any_lookup(string email)
    {
        // Arrange
        using var fixture = new AuthFixture();
        var request = new LoginRequest(email, AuthFixture.Password);

        // Act
        var failure = await Assert.ThrowsAsync<RequestValidationException>(
            () => fixture.Service.LoginAsync(request, CancellationToken.None));

        // Assert
        Assert.Contains("Email", failure.Errors.Keys);
        Assert.Empty(fixture.TokenIssuer.Calls);
    }

    [Fact]
    public async Task Empty_password_is_rejected_before_any_lookup()
    {
        // Arrange
        using var fixture = new AuthFixture();
        var doctor = fixture.AddDoctor();
        var request = new LoginRequest(doctor.Email, string.Empty);

        // Act
        var failure = await Assert.ThrowsAsync<RequestValidationException>(
            () => fixture.Service.LoginAsync(request, CancellationToken.None));

        // Assert
        Assert.Contains("Password", failure.Errors.Keys);
        Assert.Empty(fixture.TokenIssuer.Calls);
    }

    [Fact]
    public void Auth_response_contract_never_exposes_a_password_or_hash()
    {
        // Act
        var propertyNames = typeof(AuthResponse)
            .GetProperties()
            .Select(property => property.Name)
            .Concat(typeof(AuthUserResponse).GetProperties().Select(property => property.Name))
            .ToArray();

        // Assert
        Assert.DoesNotContain(propertyNames, name => name.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
    }
}
