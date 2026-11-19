using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Medilog.WebAPI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Medilog.WebAPI.Tests.Api;

/// <summary>
/// End-to-end tests for registration, email verification and resend (F6-C1-T4/T6) against the real
/// pipeline (F6-C1).
/// </summary>
/// <remarks>
/// The SMTP port is replaced with an in-memory recorder so the test can read the one-time link the
/// user would receive; everything else (routing, validation, token storage, hashing, JWT issuing)
/// runs for real. All data is synthetic (AGENTS.md §5).
/// </remarks>
public sealed class AuthRegistrationApiTests
{
    private const string RegisterUrl = "/api/auth/register";
    private const string VerifyEmailUrl = "/api/auth/verify-email";
    private const string ResendVerificationUrl = "/api/auth/resend-verification";
    private const string LoginUrl = "/api/auth/login";
    private const string Password = "Valid#Passw0rd";

    [Fact]
    public async Task Register_returns_201_without_a_location_header_and_emails_a_link()
    {
        // Arrange
        var email = new RecordingEmailService();
        using var factory = CreateFactory(email);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(RegisterUrl, new
        {
            fullName = "New Doctor",
            email = "new.doctor@medilog.test",
            password = Password,
            phone = "0100000000",
            role = "Doctor",
            specialization = "Cardiology",
        });

        // Assert: D-38 freezes the contract as 201 with no Location header.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var body = await response.ReadJsonAsync();
        Assert.Equal("new.doctor@medilog.test", body.GetProperty("email").GetString());
        Assert.Equal("Doctor", body.GetProperty("role").GetString());
        Assert.False(body.TryGetProperty("accessToken", out _));

        var message = Assert.Single(email.Messages);
        Assert.Equal("new.doctor@medilog.test", message.To);
        Assert.Contains("verify-email?token=", message.Body, StringComparison.Ordinal);
        Assert.Contains(
            "email=new.doctor%40medilog.test",
            message.Body,
            StringComparison.Ordinal);
        Assert.DoesNotContain(Password, message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_with_an_existing_email_returns_409()
    {
        // Arrange
        var email = new RecordingEmailService();
        using var factory = CreateFactory(email);
        await factory.SeedAsync(dbContext => dbContext.Doctors.Add(new Doctor
        {
            FullName = "Existing Doctor",
            Email = "taken@medilog.test",
            PasswordHash = "not-a-real-hash",
            Specialization = "General Medicine",
            AccountStatus = AccountStatus.Active,
            EmailVerifiedAtUtc = DateTimeOffset.UtcNow,
        }));

        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(RegisterUrl, new
        {
            fullName = "Another Person",
            email = "Taken@Medilog.Test",
            password = Password,
            role = "Secretary",
        });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.ReadJsonAsync();
        Assert.Equal(ErrorCodes.AuthEmailAlreadyRegistered, problem.GetProperty("code").GetString());
        Assert.Empty(email.Messages);
    }

    [Fact]
    public async Task Register_with_the_admin_role_returns_400()
    {
        // Arrange
        var email = new RecordingEmailService();
        using var factory = CreateFactory(email);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(RegisterUrl, new
        {
            fullName = "Sneaky Admin",
            email = "admin@medilog.test",
            password = Password,
            role = "Admin",
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.ReadJsonAsync();
        Assert.Equal(ErrorCodes.ValidationFailed, problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("role", out _));
        Assert.Empty(email.Messages);
    }

    [Fact]
    public async Task Register_with_a_weak_password_returns_400()
    {
        // Arrange
        var email = new RecordingEmailService();
        using var factory = CreateFactory(email);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(RegisterUrl, new
        {
            fullName = "New Doctor",
            email = "new.doctor@medilog.test",
            password = "weakpassword",
            role = "Doctor",
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.ReadJsonAsync();
        Assert.True(problem.GetProperty("errors").TryGetProperty("password", out _));
        Assert.Empty(email.Messages);
    }

    [Fact]
    public async Task Verify_email_with_the_emailed_token_activates_the_account_and_allows_sign_in()
    {
        // Arrange
        var email = new RecordingEmailService();
        using var factory = CreateFactory(email);
        using var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync(RegisterUrl, new
        {
            fullName = "New Doctor",
            email = "new.doctor@medilog.test",
            password = Password,
            role = "Doctor",
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        // Act
        var verify = await client.PostAsJsonAsync(
            VerifyEmailUrl,
            new { token = AuthEmailReader.ReadToken(email.Messages[^1]) });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);

        var login = await client.PostAsJsonAsync(
            LoginUrl,
            new { email = "new.doctor@medilog.test", password = Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var body = await login.ReadJsonAsync();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));
        Assert.Equal("Doctor", body.GetProperty("user").GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_before_verifying_the_email_returns_403_with_email_unverified()
    {
        // Arrange
        var email = new RecordingEmailService();
        using var factory = CreateFactory(email);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(RegisterUrl, new
        {
            fullName = "New Doctor",
            email = "new.doctor@medilog.test",
            password = Password,
            role = "Doctor",
        });

        // Act
        var response = await client.PostAsJsonAsync(
            LoginUrl,
            new { email = "new.doctor@medilog.test", password = Password });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var problem = await response.ReadJsonAsync();
        Assert.Equal(ErrorCodes.AuthEmailUnverified, problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Unknown_verification_token_returns_401_with_token_invalid()
    {
        // Arrange
        var email = new RecordingEmailService();
        using var factory = CreateFactory(email);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            VerifyEmailUrl,
            new { token = "not-a-real-token" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var problem = await response.ReadJsonAsync();
        Assert.Equal(ErrorCodes.AuthTokenInvalid, problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Resend_verification_returns_204_for_known_and_unknown_addresses()
    {
        // Arrange
        var email = new RecordingEmailService();
        using var factory = CreateFactory(email);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(RegisterUrl, new
        {
            fullName = "New Doctor",
            email = "new.doctor@medilog.test",
            password = Password,
            role = "Doctor",
        });

        // Act
        var known = await client.PostAsJsonAsync(
            ResendVerificationUrl,
            new { email = "new.doctor@medilog.test" });
        var unknown = await client.PostAsJsonAsync(
            ResendVerificationUrl,
            new { email = "nobody@medilog.test" });

        // Assert: the two responses are indistinguishable, so the endpoint cannot enumerate accounts.
        Assert.Equal(HttpStatusCode.NoContent, known.StatusCode);
        Assert.Equal(unknown.StatusCode, known.StatusCode);
        Assert.Equal(2, email.Messages.Count);
    }

    [Fact]
    public async Task Verification_token_can_be_used_only_once()
    {
        // Arrange
        var email = new RecordingEmailService();
        using var factory = CreateFactory(email);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(RegisterUrl, new
        {
            fullName = "New Doctor",
            email = "new.doctor@medilog.test",
            password = Password,
            role = "Doctor",
        });

        var token = AuthEmailReader.ReadToken(email.Messages[^1]);
        var first = await client.PostAsJsonAsync(VerifyEmailUrl, new { token });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        // Act
        var second = await client.PostAsJsonAsync(VerifyEmailUrl, new { token });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);

        var problem = await second.ReadJsonAsync();
        Assert.Equal(ErrorCodes.AuthTokenInvalid, problem.GetProperty("code").GetString());
    }

    private static WebApiFactory CreateFactory(RecordingEmailService email) =>
        new(
            configureTestServices: services =>
            {
                services.RemoveAll<IEmailService>();
                services.AddSingleton<IEmailService>(email);
            });
}
