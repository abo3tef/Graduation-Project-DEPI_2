using System.Net;
using System.Net.Http.Json;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.MedicalRecords.Contracts;
using Medilog.Application.Features.MedicalRecords.Responses;
using Medilog.Domain.Entities;
using Medilog.Infrastructure.Persistence.Context;
using Medilog.WebAPI.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DetailResponse = Medilog.Application.Features.MedicalRecords.Contracts.MedicalVisitResponse;

namespace Medilog.WebAPI.Tests.Api;

public sealed class VitalSignsApiTests
{
    private static readonly SaveVitalSignsRequest Valid = new("120/80", 72, 37m, 16, 70m, 175m, 98);
    private static readonly SaveVitalSignsRequest Empty = new(null, null, null, null, null, null, null);
    private static string Url(Guid visitId) => $"/api/medical-visits/{visitId}/vital-signs";

    private sealed class Scenario : IDisposable
    {
        public WebApiFactory Factory { get; } = new();
        public Guid DoctorId { get; } = Guid.NewGuid();
        public Guid PatientId { get; } = Guid.NewGuid();
        public Guid VisitId { get; } = Guid.NewGuid();
        public Guid OtherVisitId { get; } = Guid.NewGuid();
        public Guid LowId { get; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
        public Guid HighId { get; } = Guid.Parse("00000000-0000-0000-0000-000000000002");

        public async Task SeedAsync(bool measurements = true)
        {
            await Factory.SeedAsync(db =>
            {
                db.Add(ApiData.Doctor(DoctorId));
                db.Add(ApiData.Patient(PatientId, DoctorId));
                db.Add(ApiData.Visit(VisitId, PatientId, DoctorId));
                db.Add(ApiData.Visit(OtherVisitId, PatientId, DoctorId));
                if (measurements)
                {
                    db.Add(new VitalSigns { Id = HighId, VisitId = VisitId, HeartRate = 80 });
                    db.Add(new VitalSigns { Id = LowId, VisitId = VisitId, HeartRate = 72 });
                    db.Add(new VitalSigns { VisitId = OtherVisitId, HeartRate = 90 });
                }
            });
        }

        public void Dispose() => Factory.Dispose();
    }

    [Fact]
    public async Task Create_uses_route_visit_and_ignores_forged_body_ownership()
    {
        using var scenario = new Scenario();
        await scenario.SeedAsync(false);
        using var client = scenario.Factory.CreateClientFor(scenario.DoctorId);

        var response = await client.PostAsJsonAsync(Url(scenario.VisitId), new
        {
            bloodPressure = "120/80", heartRate = 72, temperature = 37m,
            respiratoryRate = 16, weight = 70m, height = 175m, spO2 = 98,
            visitId = scenario.OtherVisitId, patientId = Guid.NewGuid(), doctorId = Guid.NewGuid(),
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(Url(scenario.VisitId), response.Headers.Location!.AbsolutePath);
        var created = (await response.Content.ReadFromJsonAsync<VisitVitalSignsResponse>())!;
        Assert.Equal(new VisitVitalSignsResponse(created.Id, "120/80", 72, 37m, 16, 70m, 175m, 98), created);
        using var scope = scenario.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MedilogDbContext>();
        var stored = await db.VitalSigns.Include(value => value.Visit).SingleAsync();
        Assert.Equal(created.Id, stored.Id);
        Assert.Equal(scenario.VisitId, stored.VisitId);
        Assert.Equal(scenario.PatientId, stored.Visit.PatientId);
        Assert.Equal(scenario.DoctorId, stored.Visit.DoctorId);
    }

    [Theory]
    [InlineData("other", HttpStatusCode.NotFound)]
    [InlineData("secretary", HttpStatusCode.Forbidden)]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    public async Task Every_route_denies_unpermitted_identity(string identity, HttpStatusCode expected)
    {
        using var scenario = new Scenario();
        await scenario.SeedAsync();
        using var client = identity switch
        {
            "secretary" => scenario.Factory.CreateClientFor(scenario.DoctorId, "Secretary"),
            "other" => scenario.Factory.CreateClientFor(Guid.NewGuid()),
            _ => scenario.Factory.CreateClientFor(null, null),
        };

        var responses = new[]
        {
            await client.PostAsJsonAsync(Url(scenario.VisitId), Valid),
            await client.GetAsync(Url(scenario.VisitId)),
            await client.PutAsJsonAsync($"{Url(scenario.VisitId)}/{scenario.LowId}", Valid),
            await client.GetAsync($"/api/medical-visits/{scenario.VisitId}"),
        };

        foreach (var response in responses)
        {
            Assert.Equal(expected, response.StatusCode);
            Assert.DoesNotContain("bloodPressure", await response.Content.ReadAsStringAsync());
        }
        using var scope = scenario.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MedilogDbContext>();
        Assert.Equal(3, await db.VitalSigns.CountAsync());
        Assert.Equal(72, (await db.VitalSigns.FindAsync(scenario.LowId))!.HeartRate);
    }

    [Fact]
    public async Task List_is_paged_ordered_and_isolated_to_route_visit()
    {
        using var scenario = new Scenario();
        await scenario.SeedAsync();
        using var client = scenario.Factory.CreateClientFor(scenario.DoctorId);

        var first = (await client.GetFromJsonAsync<PagedResult<VisitVitalSignsResponse>>(
            $"{Url(scenario.VisitId)}?pageNumber=1&pageSize=1"))!;
        var second = (await client.GetFromJsonAsync<PagedResult<VisitVitalSignsResponse>>(
            $"{Url(scenario.VisitId)}?pageNumber=2&pageSize=1"))!;

        Assert.Equal(1, first.Page);
        Assert.Equal(1, first.PageSize);
        Assert.Equal(2, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(scenario.LowId, Assert.Single(first.Items).Id);
        Assert.Equal(2, second.Page);
        Assert.Equal(scenario.HighId, Assert.Single(second.Items).Id);
        var defaults = (await client.GetFromJsonAsync<PagedResult<VisitVitalSignsResponse>>(Url(scenario.VisitId)))!;
        Assert.Equal(10, defaults.PageSize);
        Assert.Equal(new[] { scenario.LowId, scenario.HighId }, defaults.Items.Select(value => value.Id));
        var beyond = (await client.GetFromJsonAsync<PagedResult<VisitVitalSignsResponse>>(
            $"{Url(scenario.VisitId)}?pageNumber=2147483647&pageSize=50"))!;
        Assert.Empty(beyond.Items);
        Assert.Equal(2, beyond.TotalCount);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public async Task Invalid_paging_is_400(int page, int size)
    {
        using var scenario = new Scenario();
        await scenario.SeedAsync();
        using var client = scenario.Factory.CreateClientFor(scenario.DoctorId);
        var response = await client.GetAsync($"{Url(scenario.VisitId)}?pageNumber={page}&pageSize={size}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_replaces_every_field_then_nulls_clear_previous_values_and_ids_are_preserved()
    {
        using var scenario = new Scenario();
        await scenario.SeedAsync();
        using var client = scenario.Factory.CreateClientFor(scenario.DoctorId);
        var url = $"{Url(scenario.VisitId)}/{scenario.LowId}";

        var response = await client.PutAsJsonAsync(url, Valid);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new VisitVitalSignsResponse(scenario.LowId, "120/80", 72, 37m, 16, 70m, 175m, 98),
            await response.Content.ReadFromJsonAsync<VisitVitalSignsResponse>());
        response = await client.PutAsJsonAsync(url, new
        {
            heartRate = 85, bloodPressure = (string?)null, temperature = (decimal?)null,
            respiratoryRate = (int?)null, weight = (decimal?)null, height = (decimal?)null, spO2 = (int?)null,
            id = Guid.NewGuid(), visitId = scenario.OtherVisitId, patientId = Guid.NewGuid(), doctorId = Guid.NewGuid(),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var expected = new VisitVitalSignsResponse(scenario.LowId, null, 85, null, null, null, null, null);
        Assert.Equal(expected, await response.Content.ReadFromJsonAsync<VisitVitalSignsResponse>());
        var page = (await client.GetFromJsonAsync<PagedResult<VisitVitalSignsResponse>>(Url(scenario.VisitId)))!;
        Assert.Equal(expected, page.Items.Single(value => value.Id == scenario.LowId));
        using var scope = scenario.Factory.Services.CreateScope();
        var stored = (await scope.ServiceProvider.GetRequiredService<MedilogDbContext>().VitalSigns.FindAsync(scenario.LowId))!;
        Assert.Equal(scenario.VisitId, stored.VisitId);
        Assert.Null(stored.Weight);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task All_null_and_out_of_range_writes_are_400_and_do_not_change_data(bool update)
    {
        using var scenario = new Scenario();
        await scenario.SeedAsync();
        using var client = scenario.Factory.CreateClientFor(scenario.DoctorId);
        foreach (var request in new[] { Empty, Valid with { HeartRate = 301 } })
        {
            var response = update
                ? await client.PutAsJsonAsync($"{Url(scenario.VisitId)}/{scenario.LowId}", request)
                : await client.PostAsJsonAsync(Url(scenario.VisitId), request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var scope = scenario.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MedilogDbContext>();
        Assert.Equal(3, await db.VitalSigns.CountAsync());
        Assert.Equal(72, (await db.VitalSigns.FindAsync(scenario.LowId))!.HeartRate);
    }

    [Fact]
    public async Task Missing_visit_missing_measurement_and_mismatched_visit_are_404()
    {
        using var scenario = new Scenario();
        await scenario.SeedAsync();
        using var client = scenario.Factory.CreateClientFor(scenario.DoctorId);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Url(Guid.NewGuid()), Valid)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Url(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(
            $"{Url(scenario.VisitId)}/{Guid.NewGuid()}", Valid)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(
            $"{Url(scenario.OtherVisitId)}/{scenario.LowId}", Valid)).StatusCode);
        using var scope = scenario.Factory.Services.CreateScope();
        Assert.Equal(72, (await scope.ServiceProvider.GetRequiredService<MedilogDbContext>()
            .VitalSigns.FindAsync(scenario.LowId))!.HeartRate);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Visit_detail_has_only_its_ordered_measurements_and_preserves_existing_properties(bool measurements)
    {
        using var scenario = new Scenario();
        await scenario.SeedAsync(measurements);
        using var client = scenario.Factory.CreateClientFor(scenario.DoctorId);

        var detail = (await client.GetFromJsonAsync<DetailResponse>($"/api/medical-visits/{scenario.VisitId}"))!;

        Assert.Equal(scenario.VisitId, detail.Id);
        Assert.Equal(scenario.PatientId, detail.PatientId);
        Assert.Equal(scenario.DoctorId, detail.DoctorId);
        Assert.Null(detail.AppointmentId);
        Assert.NotEqual(default, detail.VisitDate);
        Assert.Equal("Original complaint", detail.ChiefComplaint);
        Assert.Equal("Original symptoms", detail.Symptoms);
        Assert.Equal("Original notes", detail.ClinicalNotes);
        Assert.Empty(detail.Diagnoses);
        Assert.Empty(detail.Medications);
        Assert.Equal(measurements ? new[] { scenario.LowId, scenario.HighId } : Array.Empty<Guid>(),
            detail.VitalSigns.Select(value => value.Id));
        if (measurements)
        {
            Assert.Equal(72, detail.VitalSigns[0].HeartRate);
            Assert.Equal(80, detail.VitalSigns[1].HeartRate);
        }
        var updated = await client.PutAsJsonAsync($"/api/medical-visits/{scenario.VisitId}", ApiData.UpdateVisit());
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(detail.VitalSigns, (await updated.Content.ReadFromJsonAsync<DetailResponse>())!.VitalSigns);
    }
}
