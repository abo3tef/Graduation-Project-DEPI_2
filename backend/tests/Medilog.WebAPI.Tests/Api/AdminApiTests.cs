using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Security;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Medilog.Infrastructure.Persistence.Context;
using Medilog.Infrastructure.Security;
using Medilog.WebAPI.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Medilog.WebAPI.Tests.Api;

/// <summary>
/// Real-pipeline tests for F11-C1. The in-memory database is isolated per factory, the identity is
/// supplied by the test authentication handler (except where a real JWT is required), and all data
/// is synthetic (AGENTS.md §5).
/// </summary>
public sealed class AdminApiTests
{
    private const string Base = "/api/admin";
    private const string LoginUrl = "/api/auth/login";
    private const string PatientsUrl = "/api/patients";
    private const string AppointmentsUrl = "/api/appointments";
    private const string Password = "Valid#Passw0rd";

    private static readonly Lazy<string> PasswordHash = new(() => new PasswordHasher().Hash(Password));

    // ------------------------------------------------------------ authorization

    [Fact]
    public async Task Every_route_requires_authentication()
    {
        using var factory = new WebApiFactory();
        using var client = factory.CreateClientFor(null, null);
        var id = Guid.NewGuid();

        foreach (var (method, url, body) in AllRoutes(id))
        {
            using var request = new HttpRequestMessage(method, url);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            using var response = await client.SendAsync(request);
            Assert.True(
                HttpStatusCode.Unauthorized == response.StatusCode,
                $"{method} {url} -> {response.StatusCode}");
        }
    }

    [Theory]
    [InlineData(Roles.Doctor)]
    [InlineData(Roles.Secretary)]
    public async Task Non_admin_roles_are_forbidden_on_every_route(string role)
    {
        using var factory = new WebApiFactory();
        using var client = factory.CreateClientFor(Guid.NewGuid(), role);
        var id = Guid.NewGuid();

        foreach (var (method, url, body) in AllRoutes(id))
        {
            using var request = new HttpRequestMessage(method, url);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            using var response = await client.SendAsync(request);
            Assert.True(
                HttpStatusCode.Forbidden == response.StatusCode,
                $"{role}: {method} {url} -> {response.StatusCode}");
        }
    }

    // ------------------------------------------------------------ list & validation

    [Fact]
    public async Task List_filters_orders_pages_and_never_returns_credentials()
    {
        using var factory = new WebApiFactory();
        var admin = await SeedAdminAsync(factory);
        var activeDoctor = await SeedDoctorAsync(factory);
        var inactiveDoctor = await SeedDoctorAsync(factory, status: AccountStatus.Inactive);
        var secretary = await SeedSecretaryAsync(factory);
        using var client = factory.CreateClientFor(admin.Id, Roles.Admin);

        var response = await client.GetAsync($"{Base}/users?pageNumber=1&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var raw = await response.Content.ReadAsStringAsync();
        AssertNoSecrets(raw);

        using var document = JsonDocument.Parse(raw);
        var body = document.RootElement;
        Assert.Equal(4, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, body.GetProperty("totalPages").GetInt32());
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(2, body.GetProperty("pageSize").GetInt32());

        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(
            ["createdAtUtc", "email", "emailVerified", "fullName", "id", "phone", "role", "specialization", "status"],
            items[0].EnumerateObject().Select(property => property.Name).Order().ToArray());

        var doctors = await client.GetAsync($"{Base}/users?role=Doctor&status=Inactive&pageSize=50");
        var inactive = (await doctors.ReadJsonAsync()).GetProperty("items").EnumerateArray().ToList();
        Assert.Single(inactive);
        Assert.Equal(inactiveDoctor.Id, inactive[0].GetProperty("id").GetGuid());
        Assert.Equal("Doctor", inactive[0].GetProperty("role").GetString());

        var searched = await client.GetAsync(
            $"{Base}/users?search={Uri.EscapeDataString(secretary.Email)}");
        var matches = (await searched.ReadJsonAsync()).GetProperty("items").EnumerateArray().ToList();
        Assert.Single(matches);
        Assert.Equal(secretary.Id, matches[0].GetProperty("id").GetGuid());

        var all = await client.GetAsync($"{Base}/users?pageSize=50");
        var emails = (await all.ReadJsonAsync()).GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("email").GetString())
            .ToArray();
        Assert.Equal(emails.OrderBy(email => email, StringComparer.Ordinal), emails);
        Assert.Contains(admin.Email, emails);
        Assert.Contains(activeDoctor.Email, emails);

        var invalid = await client.GetAsync($"{Base}/users?pageSize=51");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed,
            (await invalid.ReadJsonAsync()).GetProperty("code").GetString());
    }

    // ------------------------------------------------------------ account status & login

    [Fact]
    public async Task Deactivation_blocks_login_and_reactivation_restores_it()
    {
        using var factory = new WebApiFactory(useTestAuthentication: false);
        var admin = await SeedAdminAsync(factory);
        var doctor = await SeedDoctorAsync(factory);
        using var adminClient = await SignInAsync(factory, admin);
        using var loginClient = factory.CreateClient();

        var deactivate = await adminClient.PatchAsJsonAsync(
            $"{Base}/users/{doctor.Id}/status",
            new { status = "Inactive" });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        var deactivatedRaw = await deactivate.Content.ReadAsStringAsync();
        AssertNoSecrets(deactivatedRaw);
        Assert.Equal("Inactive", JsonDocument.Parse(deactivatedRaw).RootElement
            .GetProperty("status").GetString());

        var blocked = await loginClient.PostAsJsonAsync(
            LoginUrl,
            new { email = doctor.Email, password = Password });
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal(ErrorCodes.AuthAccountInactive,
            (await blocked.ReadJsonAsync()).GetProperty("code").GetString());

        var reactivate = await adminClient.PatchAsJsonAsync(
            $"{Base}/users/{doctor.Id}/status",
            new { status = "Active" });
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);

        var allowed = await loginClient.PostAsJsonAsync(
            LoginUrl,
            new { email = doctor.Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Self_deactivation_is_blocked_and_a_deactivated_admin_token_stops_administering()
    {
        using var factory = new WebApiFactory(useTestAuthentication: false);
        var adminA = await SeedAdminAsync(factory);
        var adminB = await SeedAdminAsync(factory);
        using var clientA = await SignInAsync(factory, adminA);
        using var clientB = await SignInAsync(factory, adminB);
        using var loginClient = factory.CreateClient();

        var self = await clientA.PatchAsJsonAsync(
            $"{Base}/users/{adminA.Id}/status",
            new { status = "Inactive" });
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        Assert.Equal(ErrorCodes.AdminSelfDeactivationForbidden,
            (await self.ReadJsonAsync()).GetProperty("code").GetString());

        // The last active Admin also remains active through the self-protection rule.
        var stillActive = await clientA.GetAsync($"{Base}/users");
        Assert.Equal(HttpStatusCode.OK, stillActive.StatusCode);

        var deactivateA = await clientB.PatchAsJsonAsync(
            $"{Base}/users/{adminA.Id}/status",
            new { status = "Inactive" });
        Assert.Equal(HttpStatusCode.OK, deactivateA.StatusCode);

        // The access token is still cryptographically valid; the service must reject the now
        // inactive account instead of trusting the token alone.
        var oldToken = await clientA.GetAsync($"{Base}/users");
        Assert.Equal(HttpStatusCode.Forbidden, oldToken.StatusCode);
        Assert.Equal(ErrorCodes.IdentityInvalid,
            (await oldToken.ReadJsonAsync()).GetProperty("code").GetString());

        var login = await loginClient.PostAsJsonAsync(
            LoginUrl,
            new { email = adminA.Email, password = Password });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Equal(ErrorCodes.AuthAccountInactive,
            (await login.ReadJsonAsync()).GetProperty("code").GetString());
    }

    // ------------------------------------------------------------ role transitions

    [Fact]
    public async Task Role_change_preserves_identity_and_blocks_invalid_or_conflicting_changes()
    {
        using var factory = new WebApiFactory();
        var admin = await SeedAdminAsync(factory);
        var doctor = await SeedDoctorAsync(factory);
        var pendingToken = new AuthToken
        {
            UserId = doctor.Id,
            UserType = UserType.Doctor,
            Purpose = AuthTokenPurpose.EmailVerification,
            TokenHash = "hash-only",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
        };
        await factory.SeedAsync(db => db.AuthTokens.Add(pendingToken));

        using var client = factory.CreateClientFor(admin.Id, Roles.Admin);
        var originalCreatedAt = doctor.CreatedAtUtc;

        var response = await client.PutAsJsonAsync(
            $"{Base}/users/{doctor.Id}/role",
            new { role = "Secretary" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        AssertNoSecrets(raw);

        using var document = JsonDocument.Parse(raw);
        var body = document.RootElement;
        Assert.Equal(doctor.Id, body.GetProperty("id").GetGuid());
        Assert.Equal("Secretary", body.GetProperty("role").GetString());
        Assert.Equal(doctor.Email, body.GetProperty("email").GetString());
        Assert.Equal(originalCreatedAt, body.GetProperty("createdAtUtc").GetDateTimeOffset());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MedilogDbContext>();
            var secretary = await db.Secretaries.AsNoTracking()
                .FirstOrDefaultAsync(entity => entity.Id == doctor.Id);
            Assert.NotNull(secretary);
            Assert.Equal(doctor.PasswordHash, secretary!.PasswordHash);
            Assert.Equal(doctor.AccountStatus, secretary.AccountStatus);
            Assert.Equal(doctor.EmailVerifiedAtUtc, secretary.EmailVerifiedAtUtc);
            Assert.Equal(originalCreatedAt, secretary.CreatedAtUtc);
            Assert.Null(await db.Doctors.FindAsync(doctor.Id));
            Assert.Equal(UserType.Secretary,
                (await db.AuthTokens.FindAsync(pendingToken.Id))!.UserType);
        }

        var busyDoctor = await SeedDoctorAsync(factory);
        var patientId = Guid.NewGuid();
        await factory.SeedAsync(db =>
            db.Patients.Add(ApiData.Patient(patientId, busyDoctor.Id)));

        var blocked = await client.PutAsJsonAsync(
            $"{Base}/users/{busyDoctor.Id}/role",
            new { role = "Secretary" });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(ErrorCodes.AdminRoleChangeBlocked,
            (await blocked.ReadJsonAsync()).GetProperty("code").GetString());

        var otherAdmin = await SeedAdminAsync(factory);
        var adminTarget = await client.PutAsJsonAsync(
            $"{Base}/users/{otherAdmin.Id}/role",
            new { role = "Doctor" });
        Assert.Equal(HttpStatusCode.Forbidden, adminTarget.StatusCode);
        Assert.Equal(ErrorCodes.AdminRoleChangeForbidden,
            (await adminTarget.ReadJsonAsync()).GetProperty("code").GetString());

        var self = await client.PutAsJsonAsync(
            $"{Base}/users/{admin.Id}/role",
            new { role = "Secretary" });
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        Assert.Equal(ErrorCodes.AdminSelfRoleChangeForbidden,
            (await self.ReadJsonAsync()).GetProperty("code").GetString());

        var invalid = await client.PutAsJsonAsync(
            $"{Base}/users/{busyDoctor.Id}/role",
            new { role = "Admin" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed,
            (await invalid.ReadJsonAsync()).GetProperty("code").GetString());
    }

    // ------------------------------------------------------------ Q-16 assignments

    [Fact]
    public async Task Assignment_replacement_is_unique_and_changes_secretary_scope()
    {
        using var factory = new WebApiFactory();
        var admin = await SeedAdminAsync(factory);
        var secretary = await SeedSecretaryAsync(factory);
        var doctorA = await SeedDoctorAsync(factory);
        var doctorB = await SeedDoctorAsync(factory);
        var patientA = Guid.NewGuid();
        var patientB = Guid.NewGuid();
        var appointmentA = Guid.NewGuid();
        var appointmentB = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Patients.AddRange(
                ApiData.Patient(patientA, doctorA.Id),
                ApiData.Patient(patientB, doctorB.Id));
            db.Appointments.AddRange(
                ApiData.Appointment(appointmentA, patientA, doctorA.Id),
                ApiData.Appointment(appointmentB, patientB, doctorB.Id));
        });

        using var adminClient = factory.CreateClientFor(admin.Id, Roles.Admin);
        using var secretaryClient = factory.CreateClientFor(secretary.Id, Roles.Secretary);

        var before = await secretaryClient.GetAsync($"{PatientsUrl}?pageNumber=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Empty((await before.ReadJsonAsync()).GetProperty("items").EnumerateArray());

        var beforeAppointments = await secretaryClient.GetAsync(
            $"{AppointmentsUrl}?pageNumber=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, beforeAppointments.StatusCode);
        Assert.Empty((await beforeAppointments.ReadJsonAsync()).GetProperty("items").EnumerateArray());

        var duplicate = await adminClient.PutAsJsonAsync(
            $"{Base}/secretaries/{secretary.Id}/doctors",
            new { doctorIds = new[] { doctorA.Id, doctorA.Id } });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed,
            (await duplicate.ReadJsonAsync()).GetProperty("code").GetString());

        var assigned = await adminClient.PutAsJsonAsync(
            $"{Base}/secretaries/{secretary.Id}/doctors",
            new { doctorIds = new[] { doctorA.Id } });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        AssertNoSecrets(await assigned.Content.ReadAsStringAsync());
        var doctors = (await assigned.ReadJsonAsync())
            .GetProperty("doctors").EnumerateArray().ToList();
        Assert.Single(doctors);
        Assert.Equal(doctorA.Id, doctors[0].GetProperty("id").GetGuid());

        var readBack = await adminClient.GetAsync($"{Base}/secretaries/{secretary.Id}/doctors");
        Assert.Equal(HttpStatusCode.OK, readBack.StatusCode);
        Assert.Equal(doctorA.Id, (await readBack.ReadJsonAsync())
            .GetProperty("doctors")[0].GetProperty("id").GetGuid());

        var scoped = await secretaryClient.GetAsync($"{PatientsUrl}?pageNumber=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, scoped.StatusCode);
        var visibleIds = (await scoped.ReadJsonAsync()).GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();
        Assert.Equal([patientA], visibleIds);
        Assert.DoesNotContain(patientB, visibleIds);

        var scopedAppointments = await secretaryClient.GetAsync(
            $"{AppointmentsUrl}?pageNumber=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, scopedAppointments.StatusCode);
        var visibleAppointmentIds = (await scopedAppointments.ReadJsonAsync())
            .GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();
        Assert.Equal([appointmentA], visibleAppointmentIds);
        Assert.DoesNotContain(appointmentB, visibleAppointmentIds);

        var cleared = await adminClient.PutAsJsonAsync(
            $"{Base}/secretaries/{secretary.Id}/doctors",
            new { doctorIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Empty((await cleared.ReadJsonAsync()).GetProperty("doctors").EnumerateArray());

        var scopedAfter = await secretaryClient.GetAsync($"{PatientsUrl}?pageNumber=1&pageSize=50");
        Assert.Empty((await scopedAfter.ReadJsonAsync()).GetProperty("items").EnumerateArray());

        var appointmentsAfter = await secretaryClient.GetAsync(
            $"{AppointmentsUrl}?pageNumber=1&pageSize=50");
        Assert.Empty((await appointmentsAfter.ReadJsonAsync()).GetProperty("items").EnumerateArray());

        var missingDoctor = await adminClient.PutAsJsonAsync(
            $"{Base}/secretaries/{secretary.Id}/doctors",
            new { doctorIds = new[] { Guid.NewGuid() } });
        Assert.Equal(HttpStatusCode.NotFound, missingDoctor.StatusCode);
        Assert.Equal(ErrorCodes.DoctorNotFound,
            (await missingDoctor.ReadJsonAsync()).GetProperty("code").GetString());
    }

    // ------------------------------------------------------------ helpers

    private static IEnumerable<(HttpMethod Method, string Url, object? Body)> AllRoutes(Guid id)
    {
        yield return (HttpMethod.Get, $"{Base}/users", null);
        yield return (HttpMethod.Patch, $"{Base}/users/{id}/status", new { status = "Inactive" });
        yield return (HttpMethod.Put, $"{Base}/users/{id}/role", new { role = "Secretary" });
        yield return (HttpMethod.Get, $"{Base}/secretaries/{id}/doctors", null);
        yield return (HttpMethod.Put, $"{Base}/secretaries/{id}/doctors", new { doctorIds = Array.Empty<Guid>() });
    }

    private static Task<Admin> SeedAdminAsync(WebApiFactory factory) =>
        SeedAdminAsync(factory, $"admin-{Guid.NewGuid():N}@medilog.test");

    private static async Task<Admin> SeedAdminAsync(WebApiFactory factory, string email)
    {
        var admin = new Admin
        {
            FullName = "Administrator Under Test",
            Email = email,
            PasswordHash = PasswordHash.Value,
            AccountStatus = AccountStatus.Active,
            EmailVerifiedAtUtc = DateTimeOffset.UtcNow,
        };

        await factory.SeedAsync(db => db.Admins.Add(admin));
        return admin;
    }

    private static async Task<Doctor> SeedDoctorAsync(
        WebApiFactory factory,
        AccountStatus status = AccountStatus.Active)
    {
        var doctor = new Doctor
        {
            FullName = "Doctor Under Test",
            Email = $"doctor-{Guid.NewGuid():N}@medilog.test",
            PasswordHash = PasswordHash.Value,
            Specialization = "General Medicine",
            AccountStatus = status,
            EmailVerifiedAtUtc = DateTimeOffset.UtcNow,
        };

        await factory.SeedAsync(db => db.Doctors.Add(doctor));
        return doctor;
    }

    private static async Task<Secretary> SeedSecretaryAsync(WebApiFactory factory)
    {
        var secretary = new Secretary
        {
            FullName = "Secretary Under Test",
            Email = $"secretary-{Guid.NewGuid():N}@medilog.test",
            PasswordHash = "test-only-hash",
            AccountStatus = AccountStatus.Active,
            EmailVerifiedAtUtc = DateTimeOffset.UtcNow,
        };

        await factory.SeedAsync(db => db.Secretaries.Add(secretary));
        return secretary;
    }

    private static async Task<HttpClient> SignInAsync(WebApiFactory factory, Admin admin)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            LoginUrl,
            new { email = admin.Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var token = (await response.ReadJsonAsync()).GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static void AssertNoSecrets(string raw)
    {
        Assert.DoesNotContain(Password, raw, StringComparison.Ordinal);
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("v1$600000$", raw, StringComparison.Ordinal);
    }
}
