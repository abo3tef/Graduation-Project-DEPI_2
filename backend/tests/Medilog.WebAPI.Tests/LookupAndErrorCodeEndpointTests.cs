using System.Net;
using System.Text.Json;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;

namespace Medilog.WebAPI.Tests;

public class LookupAndErrorCodeEndpointTests
{
    private const string Base = "/api/appointments";

    private static string[] Names(JsonElement element) =>
        element.EnumerateObject().Select(p => p.Name).Order().ToArray();

    [Fact]
    public async Task Doctor_lookups_return_only_the_assigned_active_doctors_with_minimal_fields()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        api.Scenario.DoctorA.Specialization = "Cardiology";

        var response = await client.CallAsync(HttpMethod.Get, $"{Base}/lookups/doctors", api.SecretaryCaller());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.ReadJsonAsync()).EnumerateArray().ToArray();
        Assert.Single(items); // doctor B is not assigned
        Assert.Equal(["fullName", "id", "specialization"], Names(items[0]));
        Assert.Equal(api.Scenario.DoctorA.Id, items[0].GetProperty("id").GetGuid());
        Assert.Equal("Cardiology", items[0].GetProperty("specialization").GetString());
    }

    [Fact]
    public async Task Patient_lookups_return_only_id_name_and_phone()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        var s = api.Scenario;
        s.Store.Patients.Add(new Patient { FullName = "Mona Said", Phone = "0101", DoctorId = s.DoctorA.Id, Address = "secret street", EmergencyContact = "x" });

        var response = await client.CallAsync(HttpMethod.Get, $"{Base}/lookups/patients?doctorId={s.DoctorA.Id}&search=mona", api.SecretaryCaller());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        var items = JsonDocument.Parse(text).RootElement.EnumerateArray().ToArray();
        Assert.Single(items);
        Assert.Equal(["fullName", "id", "phone"], Names(items[0]));
        Assert.DoesNotContain("secret street", text);
        foreach (var medical in new[] { "bloodtype", "birth", "address", "emergency", "diagnos", "allerg", "medication" })
        {
            Assert.DoesNotContain(medical, text.ToLowerInvariant());
        }
    }

    [Fact]
    public async Task Patient_lookups_for_an_unassigned_doctor_return_404_with_the_doctor_code()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();

        var response = await client.CallAsync(HttpMethod.Get, $"{Base}/lookups/patients?doctorId={api.Scenario.DoctorB.Id}", api.SecretaryCaller());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("doctor.not_found", (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("")]                                              // doctorId missing
    [InlineData("?doctorId=00000000-0000-0000-0000-000000000000")]
    [InlineData("?doctorId=VALID&limit=0")]
    [InlineData("?doctorId=VALID&limit=21")]
    [InlineData("?doctorId=not-a-guid")]
    public async Task Patient_lookups_reject_invalid_input_with_400(string query)
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        query = query.Replace("VALID", api.Scenario.DoctorA.Id.ToString());

        var response = await client.CallAsync(HttpMethod.Get, $"{Base}/lookups/patients{query}", api.SecretaryCaller());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_doctor_id_reports_a_field_error_and_a_validator_code()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();

        var response = await client.CallAsync(
            HttpMethod.Get,
            $"{Base}/lookups/patients?doctorId=00000000-0000-0000-0000-000000000000",
            api.SecretaryCaller());

        var json = await response.ReadJsonAsync();
        Assert.Equal("validation.failed", json.GetProperty("code").GetString());
        Assert.True(json.GetProperty("errors").TryGetProperty("doctorId", out _));
        Assert.Contains("NotEmptyValidator",
            json.GetProperty("errorCodes").GetProperty("doctorId").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Lookups_require_an_identity()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();

        var doctors = await client.CallAsync(HttpMethod.Get, $"{Base}/lookups/doctors", caller: null);
        var patients = await client.CallAsync(HttpMethod.Get, $"{Base}/lookups/patients?doctorId={api.Scenario.DoctorA.Id}", caller: null);

        Assert.Equal(HttpStatusCode.Forbidden, doctors.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, patients.StatusCode);
        Assert.Equal("identity.invalid", (await doctors.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_doctor_gets_only_themself_and_cannot_search_another_doctors_patients()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        var s = api.Scenario;
        var doctorA = api.DoctorCaller(s.DoctorA);

        var doctors = (await (await client.CallAsync(HttpMethod.Get, $"{Base}/lookups/doctors", doctorA)).ReadJsonAsync()).EnumerateArray().ToArray();
        var other = await client.CallAsync(HttpMethod.Get, $"{Base}/lookups/patients?doctorId={s.DoctorB.Id}", doctorA);

        Assert.Single(doctors);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    [Fact]
    public async Task Problem_responses_carry_stable_codes_for_the_localized_messages()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        var s = api.Scenario;
        s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");
        var confirmed = s.Seed(s.DoctorA, s.PatientA, "12:00", "12:30", AppointmentStatus.Confirmed);

        async Task<string?> CodeOf(HttpResponseMessage response) =>
            (await response.ReadJsonAsync()).GetProperty("code").GetString();

        var overlap = await client.CallAsync(HttpMethod.Post, Base, api.SecretaryCaller(), api.BookBody("10:15:00", "10:45:00"));
        var past = await client.CallAsync(HttpMethod.Post, Base, api.SecretaryCaller(), api.BookBody(date: "2029-12-31"));
        var invalid = await client.CallAsync(HttpMethod.Post, Base, api.SecretaryCaller(), api.BookBody(end: null));
        var completes = await client.CallAsync(HttpMethod.Put, $"{Base}/{confirmed.Id}/status", api.SecretaryCaller(), new { status = "Completed" });
        var missing = await client.CallAsync(HttpMethod.Get, $"{Base}/{Guid.NewGuid()}", api.SecretaryCaller());
        var transition = await client.CallAsync(HttpMethod.Put, $"{Base}/{confirmed.Id}/status", api.DoctorCaller(s.DoctorA), new { status = "Confirmed" });

        Assert.Equal("appointment.overlap", await CodeOf(overlap));
        Assert.Equal("appointment.in_the_past", await CodeOf(past));
        Assert.Equal("validation.failed", await CodeOf(invalid));
        Assert.Equal("appointment.complete_requires_doctor", await CodeOf(completes));
        Assert.Equal("appointment.not_found", await CodeOf(missing));
        Assert.Equal("appointment.invalid_status_transition", await CodeOf(transition));

        var json = await invalid.ReadJsonAsync();
        Assert.Contains("NotNullValidator",
            json.GetProperty("errorCodes").GetProperty("endTime").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task An_unexpected_failure_has_the_generic_server_code()
    {
        await using var api = new ApiFactory(clockOverride: new ThrowingClock());
        using var client = api.CreateClient();

        var response = await client.CallAsync(HttpMethod.Post, Base, api.SecretaryCaller(), api.BookBody());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("server.error", (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_openapi_document_lists_the_lookup_routes()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();

        var paths = (await (await client.GetAsync("/openapi/v1.json")).ReadJsonAsync())
            .GetProperty("paths").EnumerateObject().Select(p => p.Name).ToArray();

        Assert.Contains("/api/appointments/lookups/doctors", paths);
        Assert.Contains("/api/appointments/lookups/patients", paths);
    }
}
