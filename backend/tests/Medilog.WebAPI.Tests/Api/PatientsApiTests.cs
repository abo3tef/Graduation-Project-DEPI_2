using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Medilog.Application.Common.Security;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Medilog.WebAPI.Tests.Support;
using Xunit;

namespace Medilog.WebAPI.Tests.Api;

/// <summary>
/// End-to-end tests for the F8-C1 patient routes on the real WebAPI pipeline (routing, role
/// authorization, JSON contract, global exception handler). Two doctors, one patient each, and a
/// secretary assigned to doctor A only model the approved scope (BRULE-04, Q-18).
/// </summary>
public sealed class PatientsApiTests
{
    private const string Base = "/api/patients";

    // ------------------------------------------------------------ authorization

    [Fact]
    public async Task Every_route_requires_authentication()
    {
        using var factory = new WebApiFactory();
        using var client = factory.CreateClientFor(null, null);
        var id = Guid.NewGuid();

        foreach (var (method, url) in AllRoutes(id))
        {
            using var request = new HttpRequestMessage(method, url);
            var response = await client.SendAsync(request);

            Assert.True(HttpStatusCode.Unauthorized == response.StatusCode, $"{method} {url} -> {response.StatusCode}");
        }
    }

    [Fact]
    public async Task An_admin_is_forbidden_on_every_route()
    {
        using var factory = new WebApiFactory();
        using var client = factory.CreateClientFor(Guid.NewGuid(), Roles.Admin);
        var id = Guid.NewGuid();

        foreach (var (method, url) in AllRoutes(id))
        {
            using var request = new HttpRequestMessage(method, url)
            {
                Content = JsonContent.Create(ValidBody()),
            };
            var response = await client.SendAsync(request);

            Assert.True(HttpStatusCode.Forbidden == response.StatusCode, $"{method} {url} -> {response.StatusCode}");
        }
    }

    // ------------------------------------------------------------------- create

    [Fact]
    public async Task Doctor_creates_a_patient_201_with_location_and_owner_from_the_token()
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        using var client = factory.CreateClientFor(scope.DoctorA);

        var response = await client.PostAsJsonAsync(
            Base,
            ValidBody(doctorId: scope.DoctorB, bloodType: "OPositive"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadJsonAsync();
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal(scope.DoctorA, created.GetProperty("doctorId").GetGuid());
        Assert.Equal("Sara Ali", created.GetProperty("fullName").GetString());
        Assert.Equal("1990-04-12", created.GetProperty("dateOfBirth").GetString());
        Assert.Equal("Female", created.GetProperty("gender").GetString());
        Assert.Equal("OPositive", created.GetProperty("bloodType").GetString());
        Assert.Equal($"{Base}/{id}", response.Headers.Location!.AbsolutePath);

        var fetched = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task Create_with_invalid_input_is_400_with_field_errors()
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        using var client = factory.CreateClientFor(scope.DoctorA);

        var response = await client.PostAsJsonAsync(
            Base,
            new { fullName = "", dateOfBirth = "2999-01-01", gender = "Female", phone = "abc" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.IsProblemJson());
        var problem = await response.ReadJsonAsync();
        Assert.Equal("validation.failed", problem.GetProperty("code").GetString());
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("fullName", out _));
        Assert.True(errors.TryGetProperty("dateOfBirth", out _));
        Assert.True(errors.TryGetProperty("phone", out _));
    }

    [Fact]
    public async Task Create_with_a_malformed_body_is_request_invalid()
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        using var client = factory.CreateClientFor(scope.DoctorA);

        var response = await client.PostAsync(Base, new StringContent("{ not json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request.invalid", (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Secretary_creates_for_an_assigned_doctor_and_the_response_has_no_blood_type_property()
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        using var client = factory.CreateClientFor(scope.Secretary, Roles.Secretary);

        var response = await client.PostAsJsonAsync(Base, ValidBody(doctorId: scope.DoctorA));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadJsonAsync();
        Assert.Equal(scope.DoctorA, created.GetProperty("doctorId").GetGuid());
        Assert.False(created.TryGetProperty("bloodType", out _));
    }

    [Fact]
    public async Task Secretary_creating_for_an_unassigned_doctor_is_404()
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        using var client = factory.CreateClientFor(scope.Secretary, Roles.Secretary);

        var response = await client.PostAsJsonAsync(Base, ValidBody(doctorId: scope.DoctorB));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("doctor.not_found", (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Secretary_sending_a_blood_type_is_403()
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        using var client = factory.CreateClientFor(scope.Secretary, Roles.Secretary);

        var response = await client.PostAsJsonAsync(Base, ValidBody(doctorId: scope.DoctorA, bloodType: "APositive"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            "patient.medical_field_forbidden",
            (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    // -------------------------------------------------------------- read/scope

    [Fact]
    public async Task Doctor_reads_own_patient_but_another_doctors_is_404()
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        using var client = factory.CreateClientFor(scope.DoctorA);

        var own = await client.GetAsync($"{Base}/{scope.PatientA}");
        var other = await client.GetAsync($"{Base}/{scope.PatientB}");

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal("APositive", (await own.ReadJsonAsync()).GetProperty("bloodType").GetString());
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal("patient.not_found", (await other.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Secretary_never_receives_the_blood_type_in_get_or_list_or_lookup()
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        using var client = factory.CreateClientFor(scope.Secretary, Roles.Secretary);

        var single = await client.GetAsync($"{Base}/{scope.PatientA}");
        var list = await client.GetAsync(Base);
        var lookup = await client.GetAsync($"{Base}/lookup");

        Assert.Equal(HttpStatusCode.OK, single.StatusCode);
        Assert.False((await single.ReadJsonAsync()).TryGetProperty("bloodType", out _));
        var item = Assert.Single((await list.ReadJsonAsync()).GetProperty("items").EnumerateArray());
        Assert.False(item.TryGetProperty("bloodType", out _));
        Assert.DoesNotContain("blood", await lookup.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var unassigned = await client.GetAsync($"{Base}/{scope.PatientB}");
        Assert.Equal(HttpStatusCode.NotFound, unassigned.StatusCode);
    }

    // --------------------------------------------------------------------- list

    [Fact]
    public async Task List_is_scoped_searchable_and_paged_with_the_shared_envelope()
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        await factory.SeedAsync(db =>
        {
            for (var i = 1; i <= 4; i++)
            {
                db.Add(NewPatient(Guid.NewGuid(), scope.DoctorA, $"Extra {i}", phone: $"0100000000{i}"));
            }
        });
        using var client = factory.CreateClientFor(scope.DoctorA);

        var all = await (await client.GetAsync($"{Base}?pageNumber=2&pageSize=2")).ReadJsonAsync();
        var search = await (await client.GetAsync($"{Base}?search=Extra%203")).ReadJsonAsync();
        var phone = await (await client.GetAsync($"{Base}?search=01000000004")).ReadJsonAsync();

        Assert.Equal(5, all.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, all.GetProperty("page").GetInt32());
        Assert.Equal(2, all.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, all.GetProperty("totalPages").GetInt32());
        Assert.Equal(2, all.GetProperty("items").GetArrayLength());
        Assert.Equal("Extra 3", Assert.Single(search.GetProperty("items").EnumerateArray()).GetProperty("fullName").GetString());
        Assert.Equal("Extra 4", Assert.Single(phone.GetProperty("items").EnumerateArray()).GetProperty("fullName").GetString());
    }

    [Theory]
    [InlineData("pageNumber=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=51")]
    [InlineData("pageNumber=abc")]
    [InlineData("gender=Alien")]
    public async Task List_with_invalid_paging_or_filters_is_400(string query)
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        using var client = factory.CreateClientFor(scope.DoctorA);

        var response = await client.GetAsync($"{Base}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.IsProblemJson());
    }

    [Fact]
    public async Task Lookup_is_a_bounded_minimal_list_and_does_not_collide_with_the_id_or_medical_routes()
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        using var client = factory.CreateClientFor(scope.DoctorA);

        var lookup = await client.GetAsync($"{Base}/lookup?search=Alpha&limit=5");
        var tooMany = await client.GetAsync($"{Base}/lookup?limit=21");
        var medical = await client.GetAsync($"{Base}/{scope.PatientA}/medical-profile");

        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        var item = Assert.Single((await lookup.ReadJsonAsync()).EnumerateArray());
        Assert.Equal(scope.PatientA, item.GetProperty("id").GetGuid());
        Assert.Equal(["fullName", "id", "phone"], item.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal(HttpStatusCode.OK, medical.StatusCode);
    }

    // ------------------------------------------------------------------- update

    [Fact]
    public async Task Doctor_updates_own_patient_and_cannot_move_it_to_another_owner()
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        using var client = factory.CreateClientFor(scope.DoctorA);

        var response = await client.PutAsJsonAsync(
            $"{Base}/{scope.PatientA}",
            ValidBody(doctorId: scope.DoctorB, fullName: "Renamed", bloodType: "ONegative"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadJsonAsync();
        Assert.Equal("Renamed", updated.GetProperty("fullName").GetString());
        Assert.Equal("ONegative", updated.GetProperty("bloodType").GetString());
        Assert.Equal(scope.DoctorA, updated.GetProperty("doctorId").GetGuid());
    }

    [Fact]
    public async Task Doctor_updating_another_doctors_patient_is_404()
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        using var client = factory.CreateClientFor(scope.DoctorA);

        var response = await client.PutAsJsonAsync($"{Base}/{scope.PatientB}", ValidBody());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Secretary_updates_administrative_fields_and_the_blood_type_survives()
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        using var secretary = factory.CreateClientFor(scope.Secretary, Roles.Secretary);
        using var doctor = factory.CreateClientFor(scope.DoctorA);

        var response = await secretary.PutAsJsonAsync($"{Base}/{scope.PatientA}", ValidBody(fullName: "Renamed By Secretary"));
        var rejected = await secretary.PutAsJsonAsync($"{Base}/{scope.PatientA}", ValidBody(bloodType: "BNegative"));
        var stored = await (await doctor.GetAsync($"{Base}/{scope.PatientA}")).ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await response.ReadJsonAsync()).TryGetProperty("bloodType", out _));
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.Equal("Renamed By Secretary", stored.GetProperty("fullName").GetString());
        Assert.Equal("APositive", stored.GetProperty("bloodType").GetString());
    }

    [Fact]
    public async Task Secretary_updating_an_unassigned_doctors_patient_is_404()
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        using var client = factory.CreateClientFor(scope.Secretary, Roles.Secretary);

        var response = await client.PutAsJsonAsync($"{Base}/{scope.PatientB}", ValidBody());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------- delete

    [Fact]
    public async Task Doctor_deletes_an_empty_patient_204_and_it_is_gone()
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        using var client = factory.CreateClientFor(scope.DoctorA);

        var response = await client.DeleteAsync($"{Base}/{scope.PatientA}");
        var after = await client.GetAsync($"{Base}/{scope.PatientA}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
    }

    [Fact]
    public async Task Delete_of_a_patient_with_an_appointment_is_409_and_the_patient_remains()
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        await factory.SeedAsync(db => db.Add(ApiData.Appointment(Guid.NewGuid(), scope.PatientA, scope.DoctorA)));
        using var client = factory.CreateClientFor(scope.DoctorA);

        var response = await client.DeleteAsync($"{Base}/{scope.PatientA}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(response.IsProblemJson());
        Assert.Equal("patient.has_related_records", (await response.ReadJsonAsync()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Base}/{scope.PatientA}")).StatusCode);
    }

    [Fact]
    public async Task Delete_of_a_patient_with_a_visit_is_409()
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        await factory.SeedAsync(db => db.Add(ApiData.Visit(Guid.NewGuid(), scope.PatientA, scope.DoctorA)));
        using var client = factory.CreateClientFor(scope.DoctorA);

        var response = await client.DeleteAsync($"{Base}/{scope.PatientA}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_of_a_patient_with_a_payment_is_409()
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        var appointmentId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Appointment(appointmentId, scope.PatientA, scope.DoctorA));
            db.Add(Payment.Create(appointmentId, scope.PatientA, scope.DoctorA, scope.DoctorA, 50m, PaymentMethod.Cash, null));
        });
        using var client = factory.CreateClientFor(scope.DoctorA);

        var response = await client.DeleteAsync($"{Base}/{scope.PatientA}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_by_a_secretary_is_403_and_of_another_doctors_patient_is_404()
    {
        using var factory = new WebApiFactory();
        var scope = await SeedAsync(factory);
        using var secretary = factory.CreateClientFor(scope.Secretary, Roles.Secretary);
        using var doctor = factory.CreateClientFor(scope.DoctorA);

        var bySecretary = await secretary.DeleteAsync($"{Base}/{scope.PatientA}");
        var foreign = await doctor.DeleteAsync($"{Base}/{scope.PatientB}");

        Assert.Equal(HttpStatusCode.Forbidden, bySecretary.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    // ------------------------------------------------------------------ helpers

    private static IEnumerable<(HttpMethod Method, string Url)> AllRoutes(Guid id)
    {
        yield return (HttpMethod.Post, Base);
        yield return (HttpMethod.Get, Base);
        yield return (HttpMethod.Get, $"{Base}/lookup");
        yield return (HttpMethod.Get, $"{Base}/{id}");
        yield return (HttpMethod.Put, $"{Base}/{id}");
        yield return (HttpMethod.Delete, $"{Base}/{id}");
    }

    private static object ValidBody(
        Guid? doctorId = null,
        string fullName = "Sara Ali",
        string? bloodType = null) =>
        new
        {
            doctorId,
            fullName,
            dateOfBirth = "1990-04-12",
            gender = "Female",
            phone = "01001234567",
            address = "12 Nile Street",
            emergencyContact = "Mother 01111111111",
            bloodType,
        };

    private static Patient NewPatient(Guid id, Guid doctorId, string name, string? phone = null, BloodType? bloodType = null) => new()
    {
        Id = id,
        DoctorId = doctorId,
        FullName = name,
        DateOfBirth = new DateOnly(1990, 4, 12),
        Gender = Gender.Female,
        Phone = phone,
        BloodType = bloodType,
    };

    private static async Task<Scope> SeedAsync(WebApiFactory factory)
    {
        var scope = new Scope(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await factory.SeedAsync(db =>
        {
            var doctorA = ApiData.Doctor(scope.DoctorA);
            var doctorB = ApiData.Doctor(scope.DoctorB);

            db.AddRange(
                doctorA,
                doctorB,
                NewPatient(scope.PatientA, scope.DoctorA, "Alpha Patient", bloodType: BloodType.APositive),
                NewPatient(scope.PatientB, scope.DoctorB, "Beta Patient", bloodType: BloodType.BPositive),
                new Secretary
                {
                    Id = scope.Secretary,
                    FullName = "Secretary Under Test",
                    Email = $"{Guid.NewGuid():N}@medilog.test",
                    PasswordHash = "test-only-placeholder",
                    AccountStatus = AccountStatus.Active,
                    Doctors = [doctorA],
                });
        });

        return scope;
    }

    private sealed record Scope(Guid DoctorA, Guid DoctorB, Guid PatientA, Guid PatientB, Guid Secretary);
}
