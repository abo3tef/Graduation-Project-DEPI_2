using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Security;
using Medilog.Application.Features.MedicalRecords.Contracts;
using Medilog.WebAPI.Tests.Support;
using Xunit;

namespace Medilog.WebAPI.Tests.Api;

/// <summary>
/// End-to-end tests for the three medication-line routes (F4-C3, decision D-19).
/// </summary>
/// <remarks>
/// The real WebAPI pipeline runs on top of <see cref="WebApiFactory"/>, so authentication,
/// authorization, routing, model binding and the global exception handler are all exercised.
/// Identifiers are synthetic and every test owns an isolated in-memory database.
/// </remarks>
public sealed class MedicationsApiTests
{
    private const string VisitsUrl = "/api/medical-visits";

    [Fact]
    public async Task Create_medication_returns_201_with_parent_visit_location_and_can_be_read_back()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var visitId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
            db.Add(ApiData.Visit(visitId, patientId, doctorId));
        });

        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.PostAsJsonAsync(
            $"{VisitsUrl}/{visitId}/medications",
            ApiData.CreateMedication());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<MedicationResponse>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created!.Id);
        Assert.Equal(visitId, created.VisitId);
        Assert.Equal(patientId, created.PatientId);
        Assert.Equal("Amoxicillin", created.MedicationName);

        Assert.NotNull(response.Headers.Location);
        Assert.Equal($"{VisitsUrl}/{visitId}", response.Headers.Location!.AbsolutePath);

        var visit = await client.GetFromJsonAsync<MedicalVisitResponse>($"{VisitsUrl}/{visitId}");
        Assert.NotNull(visit);
        Assert.Single(visit!.Medications);
        Assert.Equal(created.Id, visit.Medications[0].Id);
    }

    [Fact]
    public async Task Update_medication_returns_200_and_keeps_the_ownership_fields()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var visitId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
            db.Add(ApiData.Visit(visitId, patientId, doctorId));
        });

        using var client = factory.CreateClientFor(doctorId);
        var createResponse = await client.PostAsJsonAsync(
            $"{VisitsUrl}/{visitId}/medications",
            ApiData.CreateMedication());
        var created = await createResponse.Content.ReadFromJsonAsync<MedicationResponse>();

        // Act
        var response = await client.PutAsJsonAsync(
            $"{VisitsUrl}/{visitId}/medications/{created!.Id}",
            ApiData.UpdateMedication());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<MedicationResponse>();
        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated!.Id);
        Assert.Equal(visitId, updated.VisitId);
        Assert.Equal(patientId, updated.PatientId);
        Assert.Equal("Azithromycin", updated.MedicationName);
        Assert.Equal("250 mg", updated.Dosage);
        Assert.Equal("Once daily", updated.Frequency);
        Assert.Equal("5 days", updated.Duration);
        Assert.Equal("Take before food", updated.Instructions);
    }

    [Fact]
    public async Task Delete_medication_returns_204_and_removes_it_from_the_visit()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var visitId = Guid.NewGuid();
        var medicationId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
            db.Add(ApiData.Visit(visitId, patientId, doctorId));
            db.Add(ApiData.Medication(medicationId, visitId, patientId));
        });

        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.DeleteAsync(
            $"{VisitsUrl}/{visitId}/medications/{medicationId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var visit = await client.GetFromJsonAsync<MedicalVisitResponse>($"{VisitsUrl}/{visitId}");
        Assert.NotNull(visit);
        Assert.Empty(visit!.Medications);

        var repeated = await client.DeleteAsync(
            $"{VisitsUrl}/{visitId}/medications/{medicationId}");
        Assert.Equal(HttpStatusCode.NotFound, repeated.StatusCode);
    }

    [Fact]
    public async Task Anonymous_request_is_rejected_with_401_on_every_medication_route()
    {
        // Arrange
        using var factory = new WebApiFactory();
        using var client = factory.CreateClient();

        // Act & Assert
        foreach (var request in BuildRouteRequests())
        {
            using var message = request.ToHttpRequestMessage();
            var response = await client.SendAsync(message);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Secretary_request_is_rejected_with_403_on_every_medication_route()
    {
        // Arrange
        using var factory = new WebApiFactory();
        using var client = factory.CreateClientFor(Guid.NewGuid(), Roles.Secretary);

        // Act & Assert
        foreach (var request in BuildRouteRequests())
        {
            using var message = request.ToHttpRequestMessage();
            var response = await client.SendAsync(message);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task Medication_of_another_doctor_is_not_found()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var owningDoctorId = Guid.NewGuid();
        var otherDoctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var visitId = Guid.NewGuid();
        var medicationId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(owningDoctorId));
            db.Add(ApiData.Doctor(otherDoctorId));
            db.Add(ApiData.Patient(patientId, owningDoctorId));
            db.Add(ApiData.Visit(visitId, patientId, owningDoctorId));
            db.Add(ApiData.Medication(medicationId, visitId, patientId));
        });

        using var client = factory.CreateClientFor(otherDoctorId);

        // Act
        var createResponse = await client.PostAsJsonAsync(
            $"{VisitsUrl}/{visitId}/medications",
            ApiData.CreateMedication());
        var updateResponse = await client.PutAsJsonAsync(
            $"{VisitsUrl}/{visitId}/medications/{medicationId}",
            ApiData.UpdateMedication());
        var deleteResponse = await client.DeleteAsync(
            $"{VisitsUrl}/{visitId}/medications/{medicationId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Medication_from_another_visit_is_not_found()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var firstVisitId = Guid.NewGuid();
        var secondVisitId = Guid.NewGuid();
        var medicationOfSecondVisitId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
            db.Add(ApiData.Visit(firstVisitId, patientId, doctorId));
            db.Add(ApiData.Visit(secondVisitId, patientId, doctorId));
            db.Add(ApiData.Medication(medicationOfSecondVisitId, secondVisitId, patientId));
        });

        using var client = factory.CreateClientFor(doctorId);

        // Act
        var updateResponse = await client.PutAsJsonAsync(
            $"{VisitsUrl}/{firstVisitId}/medications/{medicationOfSecondVisitId}",
            ApiData.UpdateMedication());
        var deleteResponse = await client.DeleteAsync(
            $"{VisitsUrl}/{firstVisitId}/medications/{medicationOfSecondVisitId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Validation_error_returns_400_problem_details_without_internal_details()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var visitId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
            db.Add(ApiData.Visit(visitId, patientId, doctorId));
        });

        using var client = factory.CreateClientFor(doctorId);
        var invalid = ApiData.CreateMedication() with { MedicationName = "  " };

        // Act
        var response = await client.PostAsJsonAsync(
            $"{VisitsUrl}/{visitId}/medications",
            invalid);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var problem = await ReadProblemAsync(response);
        Assert.True(problem.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("medicationName", out _));
        Assert.Equal(
            ErrorCodes.ValidationFailed,
            problem.RootElement.GetProperty("code").GetString());

        var raw = problem.RootElement.GetRawText();
        Assert.DoesNotContain("stackTrace", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", raw, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<JsonDocument> ReadProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var stream = await response.Content.ReadAsStreamAsync();

        return await JsonDocument.ParseAsync(stream);
    }

    /// <summary>Builds one request per medication route so authorization can be checked everywhere.</summary>
    private static IReadOnlyList<RouteRequest> BuildRouteRequests()
    {
        var visitId = Guid.NewGuid();
        var medicationId = Guid.NewGuid();

        return
        [
            new(HttpMethod.Post, $"{VisitsUrl}/{visitId}/medications", HasBody: true),
            new(HttpMethod.Put, $"{VisitsUrl}/{visitId}/medications/{medicationId}", HasBody: true),
            new(HttpMethod.Delete, $"{VisitsUrl}/{visitId}/medications/{medicationId}"),
        ];
    }

    private sealed record RouteRequest(HttpMethod Method, string Url, bool HasBody = false)
    {
        public HttpRequestMessage ToHttpRequestMessage()
        {
            var message = new HttpRequestMessage(Method, Url);

            if (HasBody)
            {
                message.Content = JsonContent.Create(new { });
            }

            return message;
        }
    }
}
