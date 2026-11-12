using System.Net;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Enums;

namespace Medilog.WebAPI.Tests;

public class HeaderIdentityTests
{
    private const string Base = "/api/appointments";

    private static async Task<(HttpMethod Method, string Url, object? Body)[]> AllRoutesAsync(ApiFactory api)
    {
        var s = api.Scenario;
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");
        await Task.CompletedTask;

        return
        [
            (HttpMethod.Post, Base, api.BookBody("11:00:00", "11:30:00")),
            (HttpMethod.Get, $"{Base}/{appointment.Id}", null),
            (HttpMethod.Get, Base, null),
            (HttpMethod.Put, $"{Base}/{appointment.Id}/schedule", new { appointmentDate = "2030-01-03", startTime = "14:00:00", endTime = "14:30:00" }),
            (HttpMethod.Post, $"{Base}/{appointment.Id}/cancel", new { cancellationReason = "reason" }),
            (HttpMethod.Put, $"{Base}/{appointment.Id}/status", new { status = "Confirmed" })
        ];
    }

    [Fact]
    public async Task Every_route_rejects_a_request_without_identity_headers_with_403()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();

        foreach (var (method, url, body) in await AllRoutesAsync(api))
        {
            var response = await client.CallAsync(method, url, caller: null, body);

            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{method} {url} returned {(int)response.StatusCode}");
            Assert.True(response.IsProblemJson());
        }

        Assert.Single(api.Scenario.Store.Appointments); // only the seeded one: nothing was changed
    }

    [Theory]
    [InlineData("not-a-guid", "Doctor")]
    [InlineData("00000000-0000-0000-0000-000000000000", "Doctor")]
    [InlineData("", "Doctor")]
    [InlineData("VALID", "doctor")]     // role is case-sensitive
    [InlineData("VALID", "SECRETARY")]
    [InlineData("VALID", "0")]          // numeric roles are not accepted
    [InlineData("VALID", "Admin")]
    [InlineData("VALID", "")]
    [InlineData("VALID", " Doctor")]
    public async Task Invalid_identity_headers_are_rejected_with_403(string id, string role)
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Base);
        request.Headers.TryAddWithoutValidation("X-User-Id", id == "VALID" ? api.Scenario.DoctorA.Id.ToString() : id);
        request.Headers.TryAddWithoutValidation("X-User-Role", role);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_guid_without_hyphens_or_with_only_one_header_is_rejected()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();

        using var noHyphens = new HttpRequestMessage(HttpMethod.Get, Base);
        noHyphens.Headers.Add("X-User-Id", api.Scenario.DoctorA.Id.ToString("N"));
        noHyphens.Headers.Add("X-User-Role", "Doctor");

        using var idOnly = new HttpRequestMessage(HttpMethod.Get, Base);
        idOnly.Headers.Add("X-User-Id", api.Scenario.DoctorA.Id.ToString());

        using var roleOnly = new HttpRequestMessage(HttpMethod.Get, Base);
        roleOnly.Headers.Add("X-User-Role", "Doctor");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(noHyphens)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(idOnly)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(roleOnly)).StatusCode);
    }

    [Fact]
    public async Task An_unknown_or_inactive_user_is_rejected_with_403()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();

        var stranger = await client.CallAsync(HttpMethod.Get, Base, new Caller(Guid.NewGuid(), "Doctor"));
        api.Scenario.Secretary.AccountStatus = AccountStatus.Inactive;
        var inactive = await client.CallAsync(HttpMethod.Get, Base, api.SecretaryCaller());

        Assert.Equal(HttpStatusCode.Forbidden, stranger.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, inactive.StatusCode);
    }

    [Fact]
    public async Task A_valid_id_with_the_wrong_role_does_not_borrow_the_other_roles_rights()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();

        // The doctor's id presented as a Secretary is not a known secretary.
        var response = await client.CallAsync(HttpMethod.Get, Base, new Caller(api.Scenario.DoctorA.Id, "Secretary"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task When_header_identity_is_disabled_even_valid_headers_get_403()
    {
        await using var api = new ApiFactory(headerIdentityEnabled: false);
        using var client = api.CreateClient();

        foreach (var (method, url, body) in await AllRoutesAsync(api))
        {
            var response = await client.CallAsync(method, url, api.SecretaryCaller(), body);

            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{method} {url} returned {(int)response.StatusCode}");
        }
    }

    [Fact]
    public async Task Doctors_only_see_and_touch_their_own_appointments()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        var s = api.Scenario;
        var own = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");
        var other = s.Seed(s.DoctorB, s.PatientB, "10:00", "10:30");
        var doctorA = api.DoctorCaller(s.DoctorA);

        var list = await (await client.CallAsync(HttpMethod.Get, Base, doctorA)).ReadJsonAsync();
        var getOther = await client.CallAsync(HttpMethod.Get, $"{Base}/{other.Id}", doctorA);
        var cancelOther = await client.CallAsync(HttpMethod.Post, $"{Base}/{other.Id}/cancel", doctorA, new { cancellationReason = "x" });
        var forgedFilter = await (await client.CallAsync(HttpMethod.Get, $"{Base}?doctorId={s.DoctorB.Id}", doctorA)).ReadJsonAsync();

        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        Assert.Equal(own.Id, list.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, getOther.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cancelOther.StatusCode);
        Assert.Equal(0, forgedFilter.GetProperty("totalCount").GetInt32());
        Assert.Equal(AppointmentStatus.Scheduled, other.Status);
    }

    [Fact]
    public async Task A_secretary_assigned_to_two_doctors_sees_both_but_never_a_third()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        var s = api.Scenario;
        s.AssignSecretaryTo(s.DoctorB);
        s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");
        s.Seed(s.DoctorB, s.PatientB, "10:00", "10:30");
        var doctorC = new Medilog.Domain.Entities.Doctor { FullName = "Doctor C", AccountStatus = AccountStatus.Active };
        var patientC = new Medilog.Domain.Entities.Patient { FullName = "Patient C", DoctorId = doctorC.Id };
        s.Store.Doctors.Add(doctorC);
        s.Store.Patients.Add(patientC);
        s.Seed(doctorC, patientC, "10:00", "10:30");

        var list = await (await client.CallAsync(HttpMethod.Get, Base, api.SecretaryCaller())).ReadJsonAsync();

        Assert.Equal(2, list.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("GET", "/api/appointments", false)]                       // 403: no identity
    [InlineData("GET", "/api/appointments?pageNumber=0", true)]           // 400: validation
    [InlineData("GET", "/api/appointments/00000000-0000-0000-0000-000000000001", true)] // 404
    public async Task Error_responses_still_carry_the_cors_header_so_the_browser_can_read_them(
        string method, string url, bool withIdentity)
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        request.Headers.Add("Origin", "http://localhost:3000");

        if (withIdentity)
        {
            request.Headers.Add("X-User-Id", api.Scenario.Secretary.Id.ToString());
            request.Headers.Add("X-User-Role", "Secretary");
        }

        var response = await client.SendAsync(request);

        Assert.False(response.IsSuccessStatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal("http://localhost:3000", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Identity_headers_are_never_echoed_in_responses()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();

        var response = await client.CallAsync(HttpMethod.Get, Base, new Caller(Guid.NewGuid(), "Doctor"));
        var text = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("X-User", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_unknown_clinic_time_zone_stops_the_application_at_startup()
    {
        using var api = new RealClockFactory("Nowhere/Land");

        var exception = Record.Exception(() => api.CreateClient());

        Assert.NotNull(exception);
        var chain = new List<string>();
        for (var e = exception; e is not null; e = e.InnerException)
        {
            chain.Add(e.Message);
        }

        Assert.Contains(chain, message => message.Contains("Nowhere/Land"));
    }

    [Fact]
    public void A_valid_clinic_time_zone_lets_the_application_start()
    {
        using var api = new RealClockFactory("Africa/Cairo");

        using var client = api.CreateClient();

        Assert.NotNull(client);
    }
}
