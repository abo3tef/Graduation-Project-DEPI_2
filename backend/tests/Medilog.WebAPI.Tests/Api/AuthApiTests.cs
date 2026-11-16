using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Medilog.Application.Common.Exceptions;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Medilog.Infrastructure.Security;
using Medilog.WebAPI.Tests.Support;

namespace Medilog.WebAPI.Tests.Api;

/// <summary>
/// End-to-end tests for the sign-in endpoint (F6-C1-T3) against the real pipeline, including real
/// JWT issuing and bearer validation.
/// </summary>
/// <remarks>
/// The password hash is computed once because PBKDF2 with 600 000 iterations is deliberately slow
/// (decision D-34). All data is synthetic (AGENTS.md §5).
/// </remarks>
public sealed class AuthApiTests
{
    private const string LoginUrl = "/api/auth/login";

    private const string Password = "Valid#Passw0rd";

    private static readonly Lazy<string> PasswordHash = new(() => new PasswordHasher().Hash(Password));

    [Fact]
    public async Task Login_with_verified_doctor_returns_200_with_token_and_identity()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctor = await SeedDoctorAsync(factory);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(LoginUrl, new { email = doctor.Email, password = Password });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = json.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));

        var expiresAt = body.GetProperty("expiresAtUtc").GetDateTimeOffset();
        Assert.True(expiresAt > DateTimeOffset.UtcNow);

        var user = body.GetProperty("user");
        Assert.Equal(doctor.Id, user.GetProperty("id").GetGuid());
        Assert.Equal(doctor.Email, user.GetProperty("email").GetString());
        Assert.Equal("Doctor", user.GetProperty("role").GetString());
        Assert.Equal(doctor.Id, user.GetProperty("doctorId").GetGuid());
    }

    [Fact]
    public async Task Login_is_anonymous_and_does_not_require_an_identity_header()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctor = await SeedDoctorAsync(factory);
        using var client = factory.CreateClient();

        // Act: no X-Test-UserId header at all.
        var response = await client.PostAsJsonAsync(LoginUrl, new { email = doctor.Email, password = Password });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Issued_token_authenticates_a_role_protected_endpoint()
    {
        // Arrange: keep the real JWT bearer handler so the token is validated for real.
        using var factory = new WebApiFactory(useTestAuthentication: false);
        var doctor = await SeedDoctorAsync(factory);
        using var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync(LoginUrl, new { email = doctor.Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var accessToken = json.RootElement.GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Act
        var response = await client.GetAsync("/api/dashboard/doctor");

        // Assert: 200 means the token was validated, the role claim was read as "Doctor" and the
        // acting doctor resolved to the seeded account.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Request_without_a_token_is_rejected_by_a_protected_endpoint()
    {
        // Arrange
        using var factory = new WebApiFactory(useTestAuthentication: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/dashboard/doctor");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_returns_401_with_the_generic_code()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctor = await SeedDoctorAsync(factory);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(LoginUrl, new { email = doctor.Email, password = "Wrong#Passw0rd" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, json.RootElement.GetProperty("code").GetString());
        Assert.False(json.RootElement.TryGetProperty("accessToken", out _));
    }

    [Fact]
    public async Task Unknown_email_returns_the_identical_401_response()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctor = await SeedDoctorAsync(factory);
        using var client = factory.CreateClient();

        // Act
        var wrongPassword = await client.PostAsJsonAsync(
            LoginUrl,
            new { email = doctor.Email, password = "Wrong#Passw0rd" });
        var unknownEmail = await client.PostAsJsonAsync(
            LoginUrl,
            new { email = "nobody@medilog.test", password = Password });

        // Assert: same status, code and detail, so the response cannot be used to enumerate accounts.
        Assert.Equal(wrongPassword.StatusCode, unknownEmail.StatusCode);

        using var first = JsonDocument.Parse(await wrongPassword.Content.ReadAsStringAsync());
        using var second = JsonDocument.Parse(await unknownEmail.Content.ReadAsStringAsync());

        Assert.Equal(
            first.RootElement.GetProperty("code").GetString(),
            second.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            first.RootElement.GetProperty("detail").GetString(),
            second.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Unverified_account_returns_403_with_email_unverified()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctor = await SeedDoctorAsync(factory, verified: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(LoginUrl, new { email = doctor.Email, password = Password });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ErrorCodes.AuthEmailUnverified, json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Inactive_account_returns_403_with_account_inactive()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctor = await SeedDoctorAsync(factory, status: AccountStatus.Inactive);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(LoginUrl, new { email = doctor.Email, password = Password });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ErrorCodes.AuthAccountInactive, json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Empty_credentials_return_400_validation_failed_with_camel_case_errors()
    {
        // Arrange
        using var factory = new WebApiFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(LoginUrl, new { email = string.Empty, password = string.Empty });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var problem = json.RootElement;

        Assert.Equal(ErrorCodes.ValidationFailed, problem.GetProperty("code").GetString());

        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Malformed_json_body_returns_400_request_invalid()
    {
        // Arrange
        using var factory = new WebApiFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent("{\"email\":", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(LoginUrl, content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ErrorCodes.RequestInvalid, json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Login_response_never_contains_a_password_or_hash()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctor = await SeedDoctorAsync(factory);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(LoginUrl, new { email = doctor.Email, password = Password });
        var raw = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.DoesNotContain(Password, raw, StringComparison.Ordinal);
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("v1$600000$", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_is_case_insensitive_for_the_email_address()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctor = await SeedDoctorAsync(factory);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            LoginUrl,
            new { email = $"  {doctor.Email.ToUpperInvariant()}  ", password = Password });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<Doctor> SeedDoctorAsync(
        WebApiFactory factory,
        AccountStatus status = AccountStatus.Active,
        bool verified = true)
    {
        var doctor = new Doctor
        {
            FullName = "Doctor Under Test",
            Email = $"doctor-{Guid.NewGuid():N}@medilog.test",
            PasswordHash = PasswordHash.Value,
            Specialization = "General Medicine",
            AccountStatus = status,
            EmailVerifiedAtUtc = verified ? DateTimeOffset.UtcNow : null,
        };

        await factory.SeedAsync(dbContext => dbContext.Doctors.Add(doctor));

        return doctor;
    }
}
