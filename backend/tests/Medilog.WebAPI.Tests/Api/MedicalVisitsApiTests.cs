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
/// End-to-end tests for the five approved medical-visit routes (decision D-19).
/// </summary>
/// <remarks>
/// The real WebAPI pipeline runs on top of <see cref="WebApiFactory"/>, so authentication,
/// authorization, routing, model binding and the global exception handler are all exercised.
/// Identifiers are synthetic and every test owns an isolated in-memory database.
/// </remarks>
public sealed class MedicalVisitsApiTests
{
    private const string VisitsUrl = "/api/medical-visits";

    [Fact]
    public async Task Create_visit_returns_201_with_location_and_can_be_read_back()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
        });

        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.PostAsJsonAsync(VisitsUrl, ApiData.CreateVisit(patientId));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<MedicalVisitResponse>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created!.Id);
        Assert.Equal(patientId, created.PatientId);
        Assert.Equal(doctorId, created.DoctorId);
        Assert.Empty(created.Diagnoses);

        Assert.NotNull(response.Headers.Location);
        Assert.Equal($"{VisitsUrl}/{created.Id}", response.Headers.Location!.AbsolutePath);

        var fetched = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task Get_visit_returns_200_with_its_diagnoses_for_the_owning_doctor()
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
            db.Add(ApiData.Diagnosis(Guid.NewGuid(), visitId, patientId));
        });

        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.GetAsync($"{VisitsUrl}/{visitId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var visit = await response.Content.ReadFromJsonAsync<MedicalVisitResponse>();
        Assert.NotNull(visit);
        Assert.Equal(visitId, visit!.Id);
        Assert.Equal(patientId, visit.PatientId);
        Assert.Single(visit.Diagnoses);
    }

    [Fact]
    public async Task Update_visit_returns_200_and_keeps_the_ownership_fields()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
        });

        using var client = factory.CreateClientFor(doctorId);
        var createResponse = await client.PostAsJsonAsync(VisitsUrl, ApiData.CreateVisit(patientId));
        var created = await createResponse.Content.ReadFromJsonAsync<MedicalVisitResponse>();

        // Act
        var response = await client.PutAsJsonAsync(
            $"{VisitsUrl}/{created!.Id}",
            ApiData.UpdateVisit());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<MedicalVisitResponse>();
        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated!.Id);
        Assert.Equal(created.PatientId, updated.PatientId);
        Assert.Equal(created.DoctorId, updated.DoctorId);
        Assert.Equal(created.AppointmentId, updated.AppointmentId);
        Assert.Equal("Updated complaint", updated.ChiefComplaint);
        Assert.Equal("Updated symptoms", updated.Symptoms);
        Assert.Equal("Updated notes", updated.ClinicalNotes);
    }

    [Fact]
    public async Task Add_diagnosis_returns_201_with_the_parent_visit_as_location()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
        });

        using var client = factory.CreateClientFor(doctorId);
        var createResponse = await client.PostAsJsonAsync(VisitsUrl, ApiData.CreateVisit(patientId));
        var visit = await createResponse.Content.ReadFromJsonAsync<MedicalVisitResponse>();

        // Act
        var response = await client.PostAsJsonAsync(
            $"{VisitsUrl}/{visit!.Id}/diagnoses",
            ApiData.CreateDiagnosis());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var diagnosis = await response.Content.ReadFromJsonAsync<DiagnosisResponse>();
        Assert.NotNull(diagnosis);
        Assert.NotEqual(Guid.Empty, diagnosis!.Id);
        Assert.Equal(visit.Id, diagnosis.VisitId);
        Assert.Equal(patientId, diagnosis.PatientId);

        Assert.NotNull(response.Headers.Location);
        Assert.Equal($"{VisitsUrl}/{visit.Id}", response.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task Update_diagnosis_returns_200_for_the_owning_doctor()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
        });

        using var client = factory.CreateClientFor(doctorId);
        var createResponse = await client.PostAsJsonAsync(VisitsUrl, ApiData.CreateVisit(patientId));
        var visit = await createResponse.Content.ReadFromJsonAsync<MedicalVisitResponse>();
        var diagnosisResponse = await client.PostAsJsonAsync(
            $"{VisitsUrl}/{visit!.Id}/diagnoses",
            ApiData.CreateDiagnosis());
        var created = await diagnosisResponse.Content.ReadFromJsonAsync<DiagnosisResponse>();

        // Act
        var response = await client.PutAsJsonAsync(
            $"{VisitsUrl}/{visit.Id}/diagnoses/{created!.Id}",
            ApiData.UpdateDiagnosis());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<DiagnosisResponse>();
        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated!.Id);
        Assert.Equal("Tension headache", updated.DiagnosisName);
        Assert.Equal("Pressure-like pain", updated.Description);
    }

    [Fact]
    public async Task Anonymous_request_is_rejected_with_401_on_every_route()
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
    public async Task Secretary_request_is_rejected_with_403_on_every_route()
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
    public async Task Visit_of_another_doctor_is_not_found()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var owningDoctorId = Guid.NewGuid();
        var otherDoctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var visitId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(owningDoctorId));
            db.Add(ApiData.Doctor(otherDoctorId));
            db.Add(ApiData.Patient(patientId, owningDoctorId));
            db.Add(ApiData.Visit(visitId, patientId, owningDoctorId));
        });

        using var client = factory.CreateClientFor(otherDoctorId);

        // Act
        var getResponse = await client.GetAsync($"{VisitsUrl}/{visitId}");
        var putResponse = await client.PutAsJsonAsync(
            $"{VisitsUrl}/{visitId}",
            ApiData.UpdateVisit());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, putResponse.StatusCode);
    }

    [Fact]
    public async Task Create_for_a_patient_of_another_doctor_returns_403()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var owningDoctorId = Guid.NewGuid();
        var actingDoctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(owningDoctorId));
            db.Add(ApiData.Doctor(actingDoctorId));
            db.Add(ApiData.Patient(patientId, owningDoctorId));
        });

        using var client = factory.CreateClientFor(actingDoctorId);

        // Act
        var response = await client.PostAsJsonAsync(VisitsUrl, ApiData.CreateVisit(patientId));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_for_an_unowned_patient_returns_403()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, owningDoctorId: null));
        });

        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.PostAsJsonAsync(VisitsUrl, ApiData.CreateVisit(patientId));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_for_an_unknown_patient_returns_404()
    {
        // Arrange
        using var factory = new WebApiFactory();
        using var client = factory.CreateClientFor(Guid.NewGuid());

        // Act
        var response = await client.PostAsJsonAsync(
            VisitsUrl,
            ApiData.CreateVisit(Guid.NewGuid()));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Appointment_of_another_patient_returns_400_with_a_field_error()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var otherPatientId = Guid.NewGuid();
        var appointmentOfOtherPatientId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
            db.Add(ApiData.Patient(otherPatientId, doctorId));
            db.Add(ApiData.Appointment(appointmentOfOtherPatientId, otherPatientId, doctorId));
        });

        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.PostAsJsonAsync(
            VisitsUrl,
            ApiData.CreateVisit(patientId, appointmentOfOtherPatientId));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var problem = await ReadProblemAsync(response);
        Assert.True(
            problem.RootElement.TryGetProperty("errors", out var errors),
            "The validation response must expose an 'errors' member.");
        Assert.True(
            errors.TryGetProperty("appointmentId", out _),
            "The appointment field error must be reported.");
        Assert.Equal(
            ErrorCodes.ValidationFailed,
            problem.RootElement.GetProperty("code").GetString());
        Assert.True(problem.RootElement.TryGetProperty("errorCodes", out var errorCodes));
        Assert.True(errorCodes.TryGetProperty("appointmentId", out var appointmentCodes));
        Assert.Contains(
            ErrorCodes.AppointmentMismatch,
            appointmentCodes.EnumerateArray().Select(entry => entry.GetString()));
    }

    [Fact]
    public async Task Linking_the_same_appointment_twice_returns_409()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
            db.Add(ApiData.Appointment(appointmentId, patientId, doctorId));
        });

        using var client = factory.CreateClientFor(doctorId);
        var first = await client.PostAsJsonAsync(
            VisitsUrl,
            ApiData.CreateVisit(patientId, appointmentId));

        // Act
        var second = await client.PostAsJsonAsync(
            VisitsUrl,
            ApiData.CreateVisit(patientId, appointmentId));

        // Assert
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Validation_error_returns_400_problem_details_without_internal_details()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.PostAsJsonAsync(VisitsUrl, ApiData.CreateVisit(Guid.Empty));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var problem = await ReadProblemAsync(response);
        Assert.True(problem.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("patientId", out _));
        Assert.Equal(
            ErrorCodes.ValidationFailed,
            problem.RootElement.GetProperty("code").GetString());
        Assert.True(problem.RootElement.TryGetProperty("errorCodes", out var errorCodes));
        Assert.True(errorCodes.TryGetProperty("patientId", out _));

        var raw = problem.RootElement.GetRawText();
        Assert.DoesNotContain("stackTrace", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_visit_returns_404_problem_details()
    {
        // Arrange
        using var factory = new WebApiFactory();
        using var client = factory.CreateClientFor(Guid.NewGuid());

        // Act
        var response = await client.GetAsync($"{VisitsUrl}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var problem = await ReadProblemAsync(response);
        Assert.Equal(404, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Resource not found.", problem.RootElement.GetProperty("title").GetString());

        var raw = problem.RootElement.GetRawText();
        Assert.DoesNotContain("stackTrace", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Diagnosis_of_another_doctor_is_not_found()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var owningDoctorId = Guid.NewGuid();
        var otherDoctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var visitId = Guid.NewGuid();
        var diagnosisId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(owningDoctorId));
            db.Add(ApiData.Doctor(otherDoctorId));
            db.Add(ApiData.Patient(patientId, owningDoctorId));
            db.Add(ApiData.Visit(visitId, patientId, owningDoctorId));
            db.Add(ApiData.Diagnosis(diagnosisId, visitId, patientId));
        });

        using var client = factory.CreateClientFor(otherDoctorId);

        // Act
        var response = await client.PutAsJsonAsync(
            $"{VisitsUrl}/{visitId}/diagnoses/{diagnosisId}",
            ApiData.UpdateDiagnosis());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_route_is_not_exposed_in_this_cycle()
    {
        // Arrange
        using var factory = new WebApiFactory();
        using var client = factory.CreateClientFor(Guid.NewGuid());

        // Act
        var response = await client.GetAsync(VisitsUrl);

        // Assert
        // Q-04 (shared list/pagination contract) is still open, so the endpoint must not exist.
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Delete_route_is_not_exposed_in_this_cycle()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var visitId = Guid.NewGuid();
        using var client = factory.CreateClientFor(Guid.NewGuid());

        // Act
        var response = await client.DeleteAsync($"{VisitsUrl}/{visitId}");

        // Assert
        // D-12: no delete use case was approved for C2.
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    private static async Task<JsonDocument> ReadProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var stream = await response.Content.ReadAsStreamAsync();

        return await JsonDocument.ParseAsync(stream);
    }

    /// <summary>Builds one request per approved route so authorization can be checked everywhere.</summary>
    private static IReadOnlyList<RouteRequest> BuildRouteRequests()
    {
        var visitId = Guid.NewGuid();
        var diagnosisId = Guid.NewGuid();

        return
        [
            new(HttpMethod.Post, VisitsUrl, HasBody: true),
            new(HttpMethod.Get, $"{VisitsUrl}/{visitId}"),
            new(HttpMethod.Put, $"{VisitsUrl}/{visitId}", HasBody: true),
            new(HttpMethod.Post, $"{VisitsUrl}/{visitId}/diagnoses", HasBody: true),
            new(HttpMethod.Put, $"{VisitsUrl}/{visitId}/diagnoses/{diagnosisId}", HasBody: true),
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
