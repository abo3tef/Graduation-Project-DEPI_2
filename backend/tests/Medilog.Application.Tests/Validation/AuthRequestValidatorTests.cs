using Medilog.Application.Common;
using Medilog.Application.Features.Auth.Contracts;
using Medilog.Application.Features.Auth.Validation;

namespace Medilog.Application.Tests.Validation;

/// <summary>
/// Validates the shape rules of the authentication requests (F6-C1-T10, AGENTS.md §3.2).
/// </summary>
public sealed class AuthRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();
    private readonly RegisterRequestValidator _registerValidator = new();
    private readonly VerifyEmailRequestValidator _verifyEmailValidator = new();
    private readonly ResendVerificationRequestValidator _resendValidator = new();
    private readonly ForgotPasswordRequestValidator _forgotPasswordValidator = new();
    private readonly ResetPasswordRequestValidator _resetPasswordValidator = new();
    private readonly GoogleSignInRequestValidator _googleSignInValidator = new();

    [Fact]
    public void Valid_login_request_passes()
    {
        // Arrange
        var request = new LoginRequest("doctor@medilog.test", "Valid#Passw0rd");

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("not-an-email")]
    [InlineData("missing-at-sign.example.com")]
    public void Invalid_email_is_rejected(string email)
    {
        // Arrange
        var request = new LoginRequest(email, "Valid#Passw0rd");

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(LoginRequest.Email));
    }

    [Fact]
    public void Empty_password_is_rejected()
    {
        // Arrange
        var request = new LoginRequest("doctor@medilog.test", string.Empty);

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(LoginRequest.Password));
    }

    [Fact]
    public void Password_longer_than_128_characters_is_rejected()
    {
        // Arrange
        var request = new LoginRequest("doctor@medilog.test", new string('a', 129));

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Password_of_exactly_128_characters_is_accepted()
    {
        // Arrange
        var request = new LoginRequest("doctor@medilog.test", new string('a', 128));

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Email_longer_than_the_column_is_rejected()
    {
        // Arrange
        var email = new string('a', 320) + "@medilog.test";
        var request = new LoginRequest(email, "Valid#Passw0rd");

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_doctor_registration_passes()
    {
        // Arrange
        var request = new RegisterRequest(
            "New Doctor",
            "new.doctor@medilog.test",
            "Valid#Passw0rd",
            "0100000000",
            UserRole.Doctor,
            "Cardiology");

        // Act
        var result = _registerValidator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Valid_secretary_registration_passes_without_a_specialization()
    {
        // Arrange
        var request = new RegisterRequest(
            "New Secretary",
            "new.secretary@medilog.test",
            "Valid#Passw0rd",
            null,
            UserRole.Secretary,
            null);

        // Act
        var result = _registerValidator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    public void Registration_rejects_an_admin_role(UserRole role)
    {
        // Arrange
        var request = new RegisterRequest(
            "Someone",
            "someone@medilog.test",
            "Valid#Passw0rd",
            null,
            role,
            null);

        // Act
        var result = _registerValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(RegisterRequest.Role));
    }

    [Fact]
    public void Registration_rejects_a_secretary_with_a_specialization()
    {
        // Arrange
        var request = new RegisterRequest(
            "New Secretary",
            "new.secretary@medilog.test",
            "Valid#Passw0rd",
            null,
            UserRole.Secretary,
            "Cardiology");

        // Act
        var result = _registerValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(RegisterRequest.Specialization));
    }

    [Fact]
    public void Registration_rejects_a_missing_full_name()
    {
        // Arrange
        var request = new RegisterRequest(
            "  ",
            "new.doctor@medilog.test",
            "Valid#Passw0rd",
            null,
            UserRole.Doctor,
            null);

        // Act
        var result = _registerValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(RegisterRequest.FullName));
    }

    [Fact]
    public void Registration_rejects_an_overlong_phone_number()
    {
        // Arrange
        var request = new RegisterRequest(
            "New Doctor",
            "new.doctor@medilog.test",
            "Valid#Passw0rd",
            new string('0', 31),
            UserRole.Doctor,
            null);

        // Act
        var result = _registerValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(RegisterRequest.Phone));
    }

    [Fact]
    public void Registration_rejects_a_password_of_exactly_128_plus_one_characters()
    {
        // Arrange: the maximum is 128 characters.
        var request = new RegisterRequest(
            "New Doctor",
            "new.doctor@medilog.test",
            "Aa1!" + new string('a', 125),
            null,
            UserRole.Doctor,
            null);

        // Act
        var result = _registerValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Verification_request_requires_a_token()
    {
        // Arrange
        var request = new VerifyEmailRequest(string.Empty);

        // Act
        var result = _verifyEmailValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(VerifyEmailRequest.Token));
    }

    [Fact]
    public void Verification_request_accepts_a_token_without_an_email()
    {
        // Arrange: token-only clients must stay compatible with the original contract.
        var request = new VerifyEmailRequest("token-value");

        // Act
        var result = _verifyEmailValidator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Verification_request_accepts_a_valid_email_for_resend_prefill()
    {
        // Arrange
        var request = new VerifyEmailRequest("token-value", "new.doctor@medilog.test");

        // Act
        var result = _verifyEmailValidator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Verification_request_rejects_an_invalid_email()
    {
        // Arrange
        var request = new VerifyEmailRequest("token-value", "not-an-email");

        // Act
        var result = _verifyEmailValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(VerifyEmailRequest.Email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("not-an-email")]
    public void Resend_verification_rejects_an_invalid_email(string email)
    {
        // Arrange
        var request = new ResendVerificationRequest(email);

        // Act
        var result = _resendValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(ResendVerificationRequest.Email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("not-an-email")]
    public void Forgot_password_rejects_an_invalid_email(string email)
    {
        // Arrange
        var request = new ForgotPasswordRequest(email);

        // Act
        var result = _forgotPasswordValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(ForgotPasswordRequest.Email));
    }

    [Fact]
    public void Reset_password_accepts_a_valid_token_and_a_policy_compliant_password()
    {
        // Arrange
        var request = new ResetPasswordRequest("token-value", "Valid#Passw0rd");

        // Act
        var result = _resetPasswordValidator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Reset_password_requires_a_token()
    {
        // Arrange
        var request = new ResetPasswordRequest("  ", "Valid#Passw0rd");

        // Act
        var result = _resetPasswordValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(ResetPasswordRequest.Token));
    }

    [Theory]
    [InlineData("short1!")]
    [InlineData("NoSymbols1234")]
    public void Reset_password_enforces_the_password_policy(string password)
    {
        // Arrange
        var request = new ResetPasswordRequest("token-value", password);

        // Act
        var result = _resetPasswordValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(ResetPasswordRequest.NewPassword));
    }

    [Fact]
    public void Google_sign_in_requires_an_id_token()
    {
        // Arrange
        var request = new GoogleSignInRequest("  ");

        // Act
        var result = _googleSignInValidator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(GoogleSignInRequest.IdToken));
    }

    [Fact]
    public void Google_sign_in_accepts_a_token_value()
    {
        // Arrange: the validator only checks the shape; the token itself is validated separately.
        var request = new GoogleSignInRequest("a.firebase.id.token");

        // Act
        var result = _googleSignInValidator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }
}
