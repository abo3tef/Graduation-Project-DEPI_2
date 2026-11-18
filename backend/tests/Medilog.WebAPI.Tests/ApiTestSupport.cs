using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Medilog.Application.Interfaces;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Medilog.WebAPI.Tests;

/// <summary>
/// Hosts the real WebAPI pipeline (routing, JSON, exception handler, header
/// identity) against the in-memory store, so no database is needed. Every test
/// creates its own instance and therefore its own data.
/// </summary>
internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly IClinicClock? _clockOverride;

    public ApiFactory(IClinicClock? clockOverride = null)
    {
        _clockOverride = clockOverride;
    }

    public Scenario Scenario { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Enabled"] = "false",
                ["ConnectionStrings:DefaultConnection"] = "Server=unused;Database=unused;User=unused;Password=unused"
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAppointmentStore>();
            services.AddSingleton<IAppointmentStore>(Scenario.Store);
            services.RemoveAll<IClinicClock>();
            services.AddSingleton<IClinicClock>(_clockOverride ?? Scenario.Clock);
        });

        // Test-only authentication: these headers are meaningful only inside this host. Production
        // CurrentUser reads claims from the validated bearer principal and never reads headers.
        builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = HeaderTestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = HeaderTestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = HeaderTestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, HeaderTestAuthHandler>(
                    HeaderTestAuthHandler.SchemeName,
                    _ => { });
        });
    }
}

/// <summary>
/// Test-only identity handler used by the legacy appointment API tests. It accepts the historic
/// <c>X-User-*</c> headers so test scenarios stay readable; no production code accepts them.
/// </summary>
internal sealed class HeaderTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "HeaderTest";

    public HeaderTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-User-Id", out var idValues) ||
            !Guid.TryParseExact(idValues.ToString(), "D", out var userId) ||
            userId == Guid.Empty)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(SchemeName);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString("D")));
        identity.AddClaim(new Claim("sub", userId.ToString("D")));

        if (Request.Headers.TryGetValue("X-User-Role", out var roleValues))
        {
            var role = roleValues.ToString();
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
            identity.AddClaim(new Claim("role", role));
        }

        var principal = new ClaimsPrincipal(identity);

        return Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
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
                ["Seed:Enabled"] = "false",
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
