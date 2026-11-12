using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Medilog.Application.Interfaces;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Medilog.WebAPI.Tests;

/// <summary>
/// Hosts the real WebAPI pipeline (routing, JSON, exception handler, header
/// identity) against the in-memory store, so no database is needed. Every test
/// creates its own instance and therefore its own data.
/// </summary>
internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly bool _headerIdentityEnabled;
    private readonly IClinicClock? _clockOverride;

    public ApiFactory(bool headerIdentityEnabled = true, IClinicClock? clockOverride = null)
    {
        _headerIdentityEnabled = headerIdentityEnabled;
        _clockOverride = clockOverride;
    }

    public Scenario Scenario { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:AllowHeaderIdentity"] = _headerIdentityEnabled ? "true" : "false",
                ["ConnectionStrings:DefaultConnection"] = "Server=unused;Database=unused;User=unused;Password=unused"
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAppointmentStore>();
            services.AddSingleton<IAppointmentStore>(Scenario.Store);
            services.RemoveAll<IClinicClock>();
            services.AddSingleton<IClinicClock>(_clockOverride ?? Scenario.Clock);
        });
    }
}

/// <summary>Host that keeps the real clinic clock, to test configuration failures.</summary>
internal sealed class RealClockFactory : WebApplicationFactory<Program>
{
    private readonly string _timeZone;

    public RealClockFactory(string timeZone)
    {
        _timeZone = timeZone;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Clinic:TimeZone"] = _timeZone,
                ["ConnectionStrings:DefaultConnection"] = "Server=unused;Database=unused;User=unused;Password=unused"
            }));
    }
}

internal sealed class ThrowingClock : IClinicClock
{
    public DateTime LocalNow => throw new InvalidOperationException("SELECT secret FROM vault; password=hunter2");

    public bool IsNonExistentLocalTime(DateTime localTime) => false;
}

internal sealed record Caller(Guid Id, string Role);

internal static class ApiTestExtensions
{
    public static Caller SecretaryCaller(this ApiFactory api) => new(api.Scenario.Secretary.Id, "Secretary");

    public static Caller DoctorCaller(this ApiFactory api, Doctor doctor) => new(doctor.Id, "Doctor");

    public static async Task<HttpResponseMessage> CallAsync(
        this HttpClient client,
        HttpMethod method,
        string url,
        Caller? caller,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);

        if (caller is not null)
        {
            request.Headers.Add("X-User-Id", caller.Id.ToString());
            request.Headers.Add("X-User-Role", caller.Role);
        }

        if (body is not null)
        {
            request.Content = body is string raw
                ? new StringContent(raw, Encoding.UTF8, "application/json")
                : JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    public static bool IsProblemJson(this HttpResponseMessage response) =>
        response.Content.Headers.ContentType?.MediaType == "application/problem+json";

    public static object BookBody(
        this ApiFactory api,
        string start = "10:00:00",
        string? end = "10:30:00",
        string date = "2030-01-02",
        Doctor? doctor = null,
        Patient? patient = null) =>
        new
        {
            patientId = (patient ?? api.Scenario.PatientA).Id,
            doctorId = (doctor ?? api.Scenario.DoctorA).Id,
            appointmentDate = date,
            startTime = start,
            endTime = end,
            reason = "checkup"
        };
}
