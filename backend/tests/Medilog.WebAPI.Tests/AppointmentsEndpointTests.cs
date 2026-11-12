using System.Net;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Enums;
using static Medilog.Application.Tests.Support.Scenario;

namespace Medilog.WebAPI.Tests;

public class AppointmentsEndpointTests
{
    private const string Base = "/api/appointments";

    private static readonly string[] AllowedProperties =
    [
        "appointmentDate", "cancellationReason", "createdAtUtc", "createdBy", "doctorId", "doctorName",
        "endTime", "id", "notes", "patientId", "patientName", "patientPhone", "reason", "startTime", "status"
    ];

    [Fact]
    public async Task Book_returns_201_with_location_and_a_json_contract_of_strings()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();

        var response = await client.CallAsync(HttpMethod.Post, Base, api.SecretaryCaller(), api.BookBody());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.ReadJsonAsync();
        var id = json.GetProperty("id").GetGuid();
        Assert.EndsWith($"{Base}/{id}", response.Headers.Location!.ToString());
        Assert.Equal("Scheduled", json.GetProperty("status").GetString());
        Assert.Equal("2030-01-02", json.GetProperty("appointmentDate").GetString());
        Assert.Equal("10:00:00", json.GetProperty("startTime").GetString());
        Assert.Equal("10:30:00", json.GetProperty("endTime").GetString());
        Assert.Equal("Doctor A", json.GetProperty("doctorName").GetString());
        Assert.Equal(api.Scenario.Secretary.Id, json.GetProperty("createdBy").GetGuid());

        var fetched = await client.CallAsync(HttpMethod.Get, response.Headers.Location.ToString(), api.SecretaryCaller());
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task Book_ignores_a_forged_creator_and_status_in_the_body()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        var forged = new
        {
            patientId = api.Scenario.PatientA.Id,
            doctorId = api.Scenario.DoctorA.Id,
            appointmentDate = "2030-01-02",
            startTime = "10:00:00",
            endTime = "10:30:00",
            createdBy = Guid.NewGuid(),
            status = "Completed",
            id = Guid.NewGuid()
        };

        var response = await client.CallAsync(HttpMethod.Post, Base, api.SecretaryCaller(), forged);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal(api.Scenario.Secretary.Id, json.GetProperty("createdBy").GetGuid());
        Assert.Equal("Scheduled", json.GetProperty("status").GetString());
        Assert.Equal(api.Scenario.Store.Appointments.Single().Id, json.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Book_overlap_returns_409_but_an_adjacent_slot_returns_201()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        api.Scenario.Seed(api.Scenario.DoctorA, api.Scenario.PatientA, "10:00", "10:30");

        var overlap = await client.CallAsync(HttpMethod.Post, Base, api.SecretaryCaller(), api.BookBody("10:15:00", "10:45:00"));
        var adjacent = await client.CallAsync(HttpMethod.Post, Base, api.SecretaryCaller(), api.BookBody("10:30:00", "11:00:00"));

        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
        Assert.True(overlap.IsProblemJson());
        Assert.Equal(HttpStatusCode.Created, adjacent.StatusCode);
    }

    [Fact]
    public async Task Book_with_a_cancelled_slot_is_allowed()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        api.Scenario.Seed(api.Scenario.DoctorA, api.Scenario.PatientA, "10:00", "10:30", AppointmentStatus.Cancelled);

        var response = await client.CallAsync(HttpMethod.Post, Base, api.SecretaryCaller(), api.BookBody());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Book_without_an_end_time_returns_400_with_camel_case_field_errors()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();

        var response = await client.CallAsync(HttpMethod.Post, Base, api.SecretaryCaller(), api.BookBody(end: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.IsProblemJson());
        var json = await response.ReadJsonAsync();
        Assert.True(json.GetProperty("errors").TryGetProperty("endTime", out _));
        Assert.Empty(api.Scenario.Store.Appointments);
    }

    [Fact]
    public async Task Book_in_the_past_returns_400()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();

        var response = await client.CallAsync(HttpMethod.Post, Base, api.SecretaryCaller(), api.BookBody(date: "2029-12-31"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.IsProblemJson());
        Assert.Contains("past", (await response.ReadJsonAsync()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Book_for_an_unassigned_doctor_returns_404()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        var body = api.BookBody(doctor: api.Scenario.DoctorB, patient: api.Scenario.PatientB);

        var response = await client.CallAsync(HttpMethod.Post, Base, api.SecretaryCaller(), body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(api.Scenario.Store.Appointments);
    }

    [Fact]
    public async Task Get_returns_only_whitelisted_administrative_properties()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        var appointment = api.Scenario.Seed(api.Scenario.DoctorA, api.Scenario.PatientA, "10:00", "10:30");

        var response = await client.CallAsync(HttpMethod.Get, $"{Base}/{appointment.Id}", api.SecretaryCaller());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.ReadJsonAsync();
        var names = json.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(AllowedProperties.Order().ToArray(), names);

        var text = json.GetRawText().ToLowerInvariant();
        foreach (var medical in new[] { "diagnos", "prescri", "allerg", "medication", "bloodtype", "vital", "laborator", "clinical" })
        {
            Assert.DoesNotContain(medical, text);
        }
    }

    [Fact]
    public async Task Get_of_another_doctors_appointment_looks_exactly_like_a_missing_one()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        var other = api.Scenario.Seed(api.Scenario.DoctorB, api.Scenario.PatientB, "10:00", "10:30");

        var outOfScope = await client.CallAsync(HttpMethod.Get, $"{Base}/{other.Id}", api.SecretaryCaller());
        var missing = await client.CallAsync(HttpMethod.Get, $"{Base}/{Guid.NewGuid()}", api.SecretaryCaller());

        Assert.Equal(HttpStatusCode.NotFound, outOfScope.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(
            (await missing.ReadJsonAsync()).GetProperty("detail").GetString(),
            (await outOfScope.ReadJsonAsync()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task List_returns_a_paged_envelope_scoped_to_the_secretarys_doctors()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        var s = api.Scenario;
        for (var hour = 8; hour < 11; hour++)
        {
            s.Seed(s.DoctorA, s.PatientA, $"{hour:00}:00", $"{hour:00}:30");
        }

        s.Seed(s.DoctorB, s.PatientB, "09:00", "09:30");

        var response = await client.CallAsync(HttpMethod.Get, $"{Base}?pageNumber=2&pageSize=2", api.SecretaryCaller());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal(2, json.GetProperty("pageNumber").GetInt32());
        Assert.Equal(2, json.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, json.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, json.GetProperty("totalPages").GetInt32());
        var items = json.GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(items);
        Assert.Equal(s.DoctorA.Id, items[0].GetProperty("doctorId").GetGuid());
    }

    [Fact]
    public async Task List_filters_by_status_name_and_rejects_invalid_filters_and_paging()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        var s = api.Scenario;
        s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30", AppointmentStatus.Cancelled);
        s.Seed(s.DoctorA, s.PatientA, "11:00", "11:30");

        var cancelled = await client.CallAsync(HttpMethod.Get, $"{Base}?status=Cancelled", api.SecretaryCaller());
        Assert.Equal(1, (await cancelled.ReadJsonAsync()).GetProperty("totalCount").GetInt32());

        foreach (var bad in new[] { "status=Nope", "pageNumber=0", "pageSize=0", "pageSize=101", "dateFrom=not-a-date", "dateFrom=2030-02-01&dateTo=2030-01-01" })
        {
            var response = await client.CallAsync(HttpMethod.Get, $"{Base}?{bad}", api.SecretaryCaller());
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"'{bad}' returned {(int)response.StatusCode}");
        }
    }

    [Fact]
    public async Task Reschedule_returns_200_keeps_status_and_reports_conflicts_and_past_dates()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        var s = api.Scenario;
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30", AppointmentStatus.Confirmed);
        s.Seed(s.DoctorA, s.PatientA, "12:00", "12:30");
        var url = $"{Base}/{appointment.Id}/schedule";

        var ok = await client.CallAsync(HttpMethod.Put, url, api.SecretaryCaller(),
            new { appointmentDate = "2030-01-03", startTime = "14:00:00", endTime = "14:45:00" });
        var conflict = await client.CallAsync(HttpMethod.Put, url, api.SecretaryCaller(),
            new { appointmentDate = "2030-01-02", startTime = "12:15:00", endTime = "12:45:00" });
        var past = await client.CallAsync(HttpMethod.Put, url, api.SecretaryCaller(),
            new { appointmentDate = "2029-01-03", startTime = "14:00:00", endTime = "14:45:00" });

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var json = await ok.ReadJsonAsync();
        Assert.Equal("Confirmed", json.GetProperty("status").GetString());
        Assert.Equal("2030-01-03", json.GetProperty("appointmentDate").GetString());
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
    }

    [Fact]
    public async Task Cancel_returns_200_with_the_updated_appointment_and_requires_a_reason()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        var s = api.Scenario;
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");
        var url = $"{Base}/{appointment.Id}/cancel";

        var blank = await client.CallAsync(HttpMethod.Post, url, api.SecretaryCaller(), new { cancellationReason = "  " });
        var tooLong = await client.CallAsync(HttpMethod.Post, url, api.SecretaryCaller(), new { cancellationReason = new string('a', 501) });
        var ok = await client.CallAsync(HttpMethod.Post, url, api.SecretaryCaller(), new { cancellationReason = "patient asked" });
        var again = await client.CallAsync(HttpMethod.Post, url, api.SecretaryCaller(), new { cancellationReason = "twice" });

        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var json = await ok.ReadJsonAsync();
        Assert.Equal("Cancelled", json.GetProperty("status").GetString());
        Assert.Equal("patient asked", json.GetProperty("cancellationReason").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task Status_update_enforces_roles_and_string_enum_values()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        var s = api.Scenario;
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30", AppointmentStatus.Confirmed);
        var url = $"{Base}/{appointment.Id}/status";

        var secretaryCompletes = await client.CallAsync(HttpMethod.Put, url, api.SecretaryCaller(), new { status = "Completed" });
        var viaStatusCancel = await client.CallAsync(HttpMethod.Put, url, api.SecretaryCaller(), new { status = "Cancelled" });
        var numeric = await client.CallAsync(HttpMethod.Put, url, api.SecretaryCaller(), "{\"status\":2}");
        var unknown = await client.CallAsync(HttpMethod.Put, url, api.SecretaryCaller(), new { status = "Teleported" });
        var doctorCompletes = await client.CallAsync(HttpMethod.Put, url, api.DoctorCaller(s.DoctorA), new { status = "Completed" });

        Assert.Equal(HttpStatusCode.Forbidden, secretaryCompletes.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, viaStatusCancel.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, numeric.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.OK, doctorCompletes.StatusCode);
        Assert.Equal("Completed", (await doctorCompletes.ReadJsonAsync()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Secretary_can_mark_no_show_and_the_name_is_NoShow()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        var s = api.Scenario;
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");

        var response = await client.CallAsync(HttpMethod.Put, $"{Base}/{appointment.Id}/status", api.SecretaryCaller(), new { status = "NoShow" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("NoShow", (await response.ReadJsonAsync()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Invalid_transition_returns_400()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        var s = api.Scenario;
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30", AppointmentStatus.Completed);

        var response = await client.CallAsync(HttpMethod.Put, $"{Base}/{appointment.Id}/status", api.SecretaryCaller(), new { status = "Confirmed" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.IsProblemJson());
    }

    [Fact]
    public async Task Malformed_json_and_bad_ids_return_400_or_404_not_500()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();

        var malformed = await client.CallAsync(HttpMethod.Post, Base, api.SecretaryCaller(), "{bad");
        var notAGuid = await client.CallAsync(HttpMethod.Get, $"{Base}/not-a-guid", api.SecretaryCaller());

        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notAGuid.StatusCode); // route constraint {id:guid} does not match
    }

    [Fact]
    public async Task An_unexpected_failure_returns_a_generic_500_without_internal_details()
    {
        await using var api = new ApiFactory(clockOverride: new ThrowingClock());
        using var client = api.CreateClient();

        var response = await client.CallAsync(HttpMethod.Post, Base, api.SecretaryCaller(), api.BookBody());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(response.IsProblemJson());
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hunter2", text);
        Assert.DoesNotContain("SELECT", text);
        Assert.DoesNotContain("InvalidOperationException", text);
        Assert.DoesNotContain("   at ", text);
        Assert.Contains("traceId", text);
    }

    [Fact]
    public async Task Cors_preflight_allows_the_identity_headers_from_the_frontend_origin()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, Base);
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "x-user-id,x-user-role,content-type");

        var response = await client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode);
        var allowed = string.Join(',', response.Headers.GetValues("Access-Control-Allow-Headers")).ToLowerInvariant();
        Assert.Contains("x-user-id", allowed);
        Assert.Contains("x-user-role", allowed);
    }

    [Fact]
    public async Task The_openapi_document_lists_all_appointment_routes()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paths = (await response.ReadJsonAsync()).GetProperty("paths").EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Contains("/api/appointments", paths);
        Assert.Contains("/api/appointments/{id}", paths);
        Assert.Contains("/api/appointments/{id}/schedule", paths);
        Assert.Contains("/api/appointments/{id}/cancel", paths);
        Assert.Contains("/api/appointments/{id}/status", paths);
    }

    [Fact]
    public async Task Nothing_in_the_api_exposes_medical_routes()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        var s = api.Scenario;
        var caller = api.SecretaryCaller();

        foreach (var route in new[] { "/api/patients", "/api/visits", "/api/diagnoses", "/api/prescriptions", "/api/medical-records", $"/api/patients/{s.PatientA.Id}" })
        {
            var response = await client.CallAsync(HttpMethod.Get, route, caller);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
