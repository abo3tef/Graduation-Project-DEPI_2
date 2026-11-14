using Medilog.Application.Features.Auth.Contracts;
using Medilog.Application.Features.Auth.Validation;

namespace Medilog.Application.Tests.Validation;

/// <summary>
/// Validates the shape rules of the sign-in request (F6-C1-T10, AGENTS.md §3.2).
/// </summary>
public sealed class AuthRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

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
}
