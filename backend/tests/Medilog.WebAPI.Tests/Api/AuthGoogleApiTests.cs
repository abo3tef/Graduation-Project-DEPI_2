using System.Net;
using System.Net.Http.Json;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Medilog.WebAPI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Medilog.WebAPI.Tests.Api;

/// <summary>
/// End-to-end tests for POST /api/auth/google (F6-C1-T8, decision Q-12).
/// </summary>
/// <remarks>
/// The Firebase validator is replaced with <see cref="FakeGoogleTokenValidator"/>; routing,
/// authorization rules, account lookup and real JWT issuing run unchanged. Data is synthetic.
/// </remarks>
public sealed class AuthGoogleApiTests
{
    private const string GoogleUrl = "/api/auth/google";

    [Fact]
    public async Task Valid_google_identity_for_a_verified_doctor_returns_a_token()
    {
        // Arrange
        var validator = new FakeGoogleTokenValidator
        {
            NextIdentity = new GoogleTokenIdentity("google-subject", "doctor@medilog.test", true),
        };

        using var factory = CreateFactory(validator);
        var doctor = await SeedDoctorAsync(factory);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(GoogleUrl, new { idToken = "firebase-token" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.ReadJsonAsync();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));

        var user = body.GetProperty("user");
        Assert.Equal(doctor.Id, user.GetProperty("id").GetGuid());
        Assert.Equal("Doctor", user.GetProperty("role").GetString());
        Assert.Equal(doctor.Id, user.GetProperty("doctorId").GetGuid());
    }

    [Fact]
    public async Task Unknown_google_account_returns_401_without_creating_anything()
    {
        // Arrange
        var validator = new FakeGoogleTokenValidator
        {
            NextIdentity = new GoogleTokenIdentity("google-subject", "nobody@medilog.test", true),
        };

        using var factory = CreateFactory(validator);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(GoogleUrl, new { idToken = "firebase-token" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var problem = await response.ReadJsonAsync();
        Assert.Equal(ErrorCodes.AuthInvalidGoogleToken, problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Invalid_google_token_returns_401_with_the_google_code()
    {
        // Arrange
        var validator = new FakeGoogleTokenValidator { NextIdentity = null };

        using var factory = CreateFactory(validator);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(GoogleUrl, new { idToken = "forged-token" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var problem = await response.ReadJsonAsync();
        Assert.Equal(ErrorCodes.AuthInvalidGoogleToken, problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Missing_id_token_returns_400_validation_failed()
    {
        // Arrange
        var validator = new FakeGoogleTokenValidator
        {
            NextIdentity = new GoogleTokenIdentity("google-subject", "doctor@medilog.test", true),
        };

        using var factory = CreateFactory(validator);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(GoogleUrl, new { idToken = "  " });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.ReadJsonAsync();
        Assert.Equal(ErrorCodes.ValidationFailed, problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("idToken", out _));
    }

    [Fact]
    public async Task Unverified_account_returns_403_with_email_unverified()
    {
        // Arrange
        var validator = new FakeGoogleTokenValidator
        {
            NextIdentity = new GoogleTokenIdentity("google-subject", "doctor@medilog.test", true),
        };

        using var factory = CreateFactory(validator);
        await SeedDoctorAsync(factory, verified: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(GoogleUrl, new { idToken = "firebase-token" });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var problem = await response.ReadJsonAsync();
        Assert.Equal(ErrorCodes.AuthEmailUnverified, problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Inactive_account_returns_403_with_account_inactive()
    {
        // Arrange
        var validator = new FakeGoogleTokenValidator
        {
            NextIdentity = new GoogleTokenIdentity("google-subject", "doctor@medilog.test", true),
        };

        using var factory = CreateFactory(validator);
        await SeedDoctorAsync(factory, status: AccountStatus.Inactive);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(GoogleUrl, new { idToken = "firebase-token" });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var problem = await response.ReadJsonAsync();
        Assert.Equal(ErrorCodes.AuthAccountInactive, problem.GetProperty("code").GetString());
    }

    private static WebApiFactory CreateFactory(FakeGoogleTokenValidator validator) =>
        new(
            configureTestServices: services =>
            {
                services.RemoveAll<IGoogleTokenValidator>();
                services.AddSingleton<IGoogleTokenValidator>(validator);
            });

    private static async Task<Doctor> SeedDoctorAsync(
        WebApiFactory factory,
        AccountStatus status = AccountStatus.Active,
        bool verified = true)
    {
        var doctor = new Doctor
        {
            FullName = "Doctor Under Test",
            Email = "doctor@medilog.test",
            PasswordHash = "not-a-real-hash",
            Specialization = "General Medicine",
            AccountStatus = status,
            EmailVerifiedAtUtc = verified ? DateTimeOffset.UtcNow : null,
        };

        await factory.SeedAsync(dbContext => dbContext.Doctors.Add(doctor));

        return doctor;
    }
}
