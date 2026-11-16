using System.Net;
using System.Net.Http.Json;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Medilog.Infrastructure.Security;
using Medilog.WebAPI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Medilog.WebAPI.Tests.Api;

/// <summary>
/// End-to-end tests for forgot/reset password (F6-C1-T7) against the real pipeline: no account
/// enumeration, single-use tokens and the password policy (AGENTS.md §5).
/// </summary>
/// <remarks>
/// The password hash is computed once because PBKDF2 with 600 000 iterations is deliberately slow
/// (decision D-34). All data is synthetic (AGENTS.md §5).
/// </remarks>
public sealed class AuthPasswordResetApiTests
{
    private const string ForgotUrl = "/api/auth/forgot-password";
    private const string ResetUrl = "/api/auth/reset-password";
    private const string LoginUrl = "/api/auth/login";
    private const string EmailAddress = "doctor@medilog.test";
    private const string Password = "Valid#Passw0rd";
    private const string NewPassword = "NewValid#Passw0rd";

    private static readonly Lazy<string> PasswordHash = new(() => new PasswordHasher().Hash(Password));

    [Fact]
    public async Task Forgot_password_returns_204_for_known_and_unknown_addresses()
    {
        // Arrange
        var email = new RecordingEmailService();
        using var factory = CreateFactory(email);
        var doctor = await SeedDoctorAsync(factory);
        using var client = factory.CreateClient();

        // Act
        var known = await client.PostAsJsonAsync(ForgotUrl, new { email = doctor.Email });
        var unknown = await client.PostAsJsonAsync(ForgotUrl, new { email = "nobody@medilog.test" });

        // Assert: identical responses, one email only, so the endpoint cannot enumerate accounts.
        Assert.Equal(HttpStatusCode.NoContent, known.StatusCode);
        Assert.Equal(unknown.StatusCode, known.StatusCode);
        Assert.Single(email.Messages);
    }

    [Fact]
    public async Task Reset_password_changes_the_password_and_the_old_one_stops_working()
    {
        // Arrange
        var email = new RecordingEmailService();
        using var factory = CreateFactory(email);
        var doctor = await SeedDoctorAsync(factory);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(ForgotUrl, new { email = doctor.Email });
        var token = AuthEmailReader.ReadToken(email.Messages[^1]);

        // Act
        var reset = await client.PostAsJsonAsync(ResetUrl, new { token, newPassword = NewPassword });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var oldLogin = await client.PostAsJsonAsync(LoginUrl, new { email = doctor.Email, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await client.PostAsJsonAsync(LoginUrl, new { email = doctor.Email, password = NewPassword });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task Reset_token_can_be_used_only_once_over_http()
    {
        // Arrange
        var email = new RecordingEmailService();
        using var factory = CreateFactory(email);
        var doctor = await SeedDoctorAsync(factory);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(ForgotUrl, new { email = doctor.Email });
        var token = AuthEmailReader.ReadToken(email.Messages[^1]);

        var first = await client.PostAsJsonAsync(ResetUrl, new { token, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        // Act
        var second = await client.PostAsJsonAsync(
            ResetUrl,
            new { token, newPassword = "Another#Passw0rd" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);

        var problem = await second.ReadJsonAsync();
        Assert.Equal(ErrorCodes.AuthTokenInvalid, problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Unknown_reset_token_returns_401_with_token_invalid()
    {
        // Arrange
        var email = new RecordingEmailService();
        using var factory = CreateFactory(email);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            ResetUrl,
            new { token = "not-a-real-token", newPassword = NewPassword });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var problem = await response.ReadJsonAsync();
        Assert.Equal(ErrorCodes.AuthTokenInvalid, problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Reset_with_a_weak_password_returns_400_and_keeps_the_old_password()
    {
        // Arrange
        var email = new RecordingEmailService();
        using var factory = CreateFactory(email);
        var doctor = await SeedDoctorAsync(factory);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(ForgotUrl, new { email = doctor.Email });
        var token = AuthEmailReader.ReadToken(email.Messages[^1]);

        // Act
        var response = await client.PostAsJsonAsync(ResetUrl, new { token, newPassword = "weakpassword" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.ReadJsonAsync();
        Assert.True(problem.GetProperty("errors").TryGetProperty("newPassword", out _));

        var login = await client.PostAsJsonAsync(LoginUrl, new { email = doctor.Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Forgot_password_does_not_expose_a_token_in_the_response()
    {
        // Arrange
        var email = new RecordingEmailService();
        using var factory = CreateFactory(email);
        var doctor = await SeedDoctorAsync(factory);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(ForgotUrl, new { email = doctor.Email });

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(body);
    }

    private static WebApiFactory CreateFactory(RecordingEmailService email) =>
        new(
            configureTestServices: services =>
            {
                services.RemoveAll<IEmailService>();
                services.AddSingleton<IEmailService>(email);
            });

    private static async Task<Doctor> SeedDoctorAsync(WebApiFactory factory)
    {
        var doctor = new Doctor
        {
            FullName = "Doctor Under Test",
            Email = EmailAddress,
            PasswordHash = PasswordHash.Value,
            Specialization = "General Medicine",
            AccountStatus = AccountStatus.Active,
            EmailVerifiedAtUtc = DateTimeOffset.UtcNow,
        };

        await factory.SeedAsync(dbContext => dbContext.Doctors.Add(doctor));

        return doctor;
    }
}
