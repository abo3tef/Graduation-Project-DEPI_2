using System.Net;
using System.Text.Json;
using Medilog.Application.Common.Security;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Medilog.WebAPI.Tests.Support;
using Xunit;

namespace Medilog.WebAPI.Tests.Api;

/// <summary>
/// End-to-end tests for the F3-C2 medical read routes: <c>GET /api/patients/{id}/medical-profile</c>
/// and <c>GET /api/patients/{id}/medical-visits</c>. The real WebAPI pipeline runs on top of
/// <see cref="WebApiFactory"/>, so authentication, authorization, routing and the shared error
/// envelope are all exercised. These tests close the AGENTS §7 API-level release dependency:
/// a secretary cannot retrieve medical records by calling the API, and a doctor cannot retrieve
/// another doctor's patient data. Identifiers are synthetic (AGENTS.md §5).
/// </summary>
public sealed class MedicalRecordsApiTests
{
    private static string ProfileUrl(Guid patientId) => $"/api/patients/{patientId}/medical-profile";

    private static string HistoryUrl(Guid patientId) => $"/api/patients/{patientId}/medical-visits";

    private static Patient ProfilePatient(Guid id, Guid owningDoctorId)
    {
        var patient = ApiData.Patient(id, owningDoctorId);
        patient.BloodType = BloodType.APositive;
        patient.EmergencyContact = "Test emergency contact";
        return patient;
    }

    private static Allergy Allergy(Guid id, Guid patientId) => new()
    {
        Id = id,
        PatientId = patientId,
        AllergyName = "Penicillin",
        Severity = AllergySeverity.Mild,
        Notes = "test-only",
    };

    private static ChronicCondition Condition(Guid id, Guid patientId) => new()
    {
        Id = id,
        PatientId = patientId,
        ConditionName = "Diabetes mellitus type 2",
        Status = ChronicConditionStatus.Active,
    };

    private static MedicalVisit VisitAt(Guid id, Guid patientId, Guid doctorId, DateTimeOffset visitDate)
    {
        var visit = ApiData.Visit(id, patientId, doctorId);
        visit.VisitDate = visitDate;
        return visit;
    }

    private static VitalSigns VitalSigns(Guid id, Guid visitId) => new()
    {
        Id = id,
        VisitId = visitId,
        BloodPressure = "120/80",
        HeartRate = 72,
    };

    private static MedicalNote Note(Guid id, Guid patientId, Guid doctorId, Guid visitId) => new()
    {
        Id = id,
        PatientId = patientId,
        DoctorId = doctorId,
        VisitId = visitId,
        NoteContent = "test-only note",
    };

    private static LaboratoryResult LabResult(Guid id, Guid patientId, Guid visitId) => new()
    {
        Id = id,
        PatientId = patientId,
        VisitId = visitId,
        TestName = "Hemoglobin",
        TestDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
        Value = 13.5m,
        Unit = "g/dL",
    };

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    [Fact]
    public async Task Profile_returns_200_with_the_full_class_spec_fields_for_the_owning_doctor()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ProfilePatient(patientId, doctorId));
            db.Add(Allergy(Guid.NewGuid(), patientId));
            db.Add(Condition(Guid.NewGuid(), patientId));
        });

        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.GetAsync(ProfileUrl(patientId));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var root = body.RootElement;
        Assert.Equal(patientId, root.GetProperty("patientId").GetGuid());
        Assert.Equal(BloodType.APositive.ToString(), root.GetProperty("bloodType").GetString());
        Assert.Equal("Test emergency contact", root.GetProperty("emergencyContact").GetString());
        Assert.Equal(Gender.Female.ToString(), root.GetProperty("gender").GetString());
        Assert.True(root.TryGetProperty("age", out var age) && age.ValueKind == JsonValueKind.Number);
        Assert.Equal(1, root.GetProperty("allergies").GetArrayLength());
        Assert.Equal(1, root.GetProperty("chronicConditions").GetArrayLength());
    }

    [Fact]
    public async Task History_returns_the_Q04_paged_envelope_newest_first_with_children()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var newestVisitId = Guid.NewGuid();
        var middleVisitId = Guid.NewGuid();
        var oldestVisitId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
            db.Add(VisitAt(newestVisitId, patientId, doctorId, DateTimeOffset.UtcNow.AddDays(-1)));
            db.Add(VisitAt(middleVisitId, patientId, doctorId, DateTimeOffset.UtcNow.AddDays(-5)));
            db.Add(VisitAt(oldestVisitId, patientId, doctorId, DateTimeOffset.UtcNow.AddDays(-10)));
            db.Add(ApiData.Diagnosis(Guid.NewGuid(), newestVisitId, patientId));
            db.Add(ApiData.Medication(Guid.NewGuid(), newestVisitId, patientId));
            db.Add(VitalSigns(Guid.NewGuid(), newestVisitId));
            db.Add(Note(Guid.NewGuid(), patientId, doctorId, newestVisitId));
            db.Add(LabResult(Guid.NewGuid(), patientId, newestVisitId));
        });

        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.GetAsync($"{HistoryUrl(patientId)}?pageNumber=1&pageSize=2");

        // Assert: the shared envelope and the newest-first ordering.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var page = body.RootElement;
        Assert.Equal(1, page.GetProperty("page").GetInt32());
        Assert.Equal(2, page.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page.GetProperty("totalPages").GetInt32());
        Assert.Equal(2, page.GetProperty("items").GetArrayLength());

        var newest = page.GetProperty("items")[0];
        Assert.Equal(newestVisitId, newest.GetProperty("visitId").GetGuid());
        Assert.Equal(1, newest.GetProperty("diagnoses").GetArrayLength());
        Assert.Equal(1, newest.GetProperty("medications").GetArrayLength());
        Assert.Equal(1, newest.GetProperty("vitalSigns").GetArrayLength());
        Assert.Equal(1, newest.GetProperty("notes").GetArrayLength());
        Assert.Equal(1, newest.GetProperty("labResults").GetArrayLength());
        Assert.Equal(middleVisitId, page.GetProperty("items")[1].GetProperty("visitId").GetGuid());

        // Act: the second page carries the oldest visit.
        var secondPage = await client.GetAsync($"{HistoryUrl(patientId)}?pageNumber=2&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, secondPage.StatusCode);

        using var secondBody = await ReadJsonAsync(secondPage);
        var secondRoot = secondBody.RootElement;
        Assert.Equal(2, secondRoot.GetProperty("page").GetInt32());
        Assert.Equal(1, secondRoot.GetProperty("items").GetArrayLength());
        Assert.Equal(oldestVisitId, secondRoot.GetProperty("items")[0].GetProperty("visitId").GetGuid());
    }

    [Fact]
    public async Task History_beyond_the_last_page_returns_200_with_empty_items()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
            db.Add(VisitAt(Guid.NewGuid(), patientId, doctorId, DateTimeOffset.UtcNow.AddDays(-1)));
        });

        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.GetAsync($"{HistoryUrl(patientId)}?pageNumber=2&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var root = body.RootElement;
        Assert.Equal(2, root.GetProperty("page").GetInt32());
        Assert.Equal(0, root.GetProperty("items").GetArrayLength());
        Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, root.GetProperty("totalPages").GetInt32());
    }

    /// <summary>
    /// AGENTS §7 API-level case: a secretary calling the API directly is rejected on both
    /// read routes and receives no medical data. The framework role check rejects the
    /// request before any service call; the deeper <c>medical.access_denied</c> boundary
    /// (with zero store calls) is proven by the F3-C1 unit tests against the service.
    /// </summary>
    [Fact]
    public async Task Secretary_request_is_rejected_with_403_and_no_medical_data()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ProfilePatient(patientId, doctorId));
        });

        using var client = factory.CreateClientFor(Guid.NewGuid(), Roles.Secretary);

        // Act & Assert
        foreach (var url in new[] { ProfileUrl(patientId), $"{HistoryUrl(patientId)}?pageNumber=1&pageSize=10" })
        {
            var response = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            var root = body.RootElement;
            Assert.True(root.TryGetProperty("code", out _), "The 403 must use the shared envelope.");
            Assert.False(
                root.TryGetProperty("fullName", out _) || root.TryGetProperty("bloodType", out _),
                "A secretary must never receive medical data.");
        }
    }

    /// <summary>AGENTS §7 API-level case: another doctor's patient is reported as 404.</summary>
    [Fact]
    public async Task Patient_of_another_doctor_is_not_found_on_both_routes()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var owningDoctorId = Guid.NewGuid();
        var otherDoctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(owningDoctorId));
            db.Add(ApiData.Doctor(otherDoctorId));
            db.Add(ProfilePatient(patientId, owningDoctorId));
        });

        using var client = factory.CreateClientFor(otherDoctorId);

        // Act & Assert
        foreach (var url in new[] { ProfileUrl(patientId), $"{HistoryUrl(patientId)}?pageNumber=1&pageSize=10" })
        {
            var response = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            var root = body.RootElement;
            Assert.Equal("patient.not_found", root.GetProperty("code").GetString());
            Assert.False(
                root.TryGetProperty("fullName", out _) || root.TryGetProperty("bloodType", out _),
                "Out-of-scope medical data must never be revealed.");
        }
    }

    [Fact]
    public async Task Unauthenticated_request_is_rejected_with_401_and_no_medical_data()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ProfilePatient(patientId, doctorId));
        });

        // No user header: the test handler yields no principal, so the real
        // authorization middleware produces a genuine 401 (decision D-19).
        using var client = factory.CreateClientFor(null);

        // Act & Assert
        foreach (var url in new[] { ProfileUrl(patientId), $"{HistoryUrl(patientId)}?pageNumber=1&pageSize=10" })
        {
            var response = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            Assert.False(
                body.RootElement.TryGetProperty("fullName", out _),
                "An unauthenticated caller must never receive medical data.");
        }
    }

    [Fact]
    public async Task History_uses_the_shared_paging_defaults_when_the_query_is_omitted()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
            db.Add(VisitAt(Guid.NewGuid(), patientId, doctorId, DateTimeOffset.UtcNow.AddDays(-1)));
        });

        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.GetAsync(HistoryUrl(patientId));

        // Assert: no query at all means the Q-04 defaults (page 1, size 10) from PagingDefaults.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var root = body.RootElement;
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(10, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, root.GetProperty("items").GetArrayLength());
    }

    [Theory]
    [InlineData("pageNumber=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=51")]
    public async Task Invalid_paging_returns_400_with_the_shared_validation_envelope(string query)
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
        var response = await client.GetAsync($"{HistoryUrl(patientId)}?{query}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var root = body.RootElement;
        Assert.Equal("validation.failed", root.GetProperty("code").GetString());
        Assert.True(root.TryGetProperty("errors", out var errors), "The 400 must list field errors.");

        var expectedField = query.StartsWith("pageNumber") ? "pageNumber" : "pageSize";
        Assert.True(
            errors.TryGetProperty(expectedField, out _),
            $"The '{expectedField}' error must be reported.");

        Assert.True(root.TryGetProperty("errorCodes", out var errorCodes));
        Assert.True(errorCodes.TryGetProperty(expectedField, out _));
    }

    [Fact]
    public async Task Unknown_patient_returns_the_same_404_as_an_out_of_scope_patient()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        await factory.SeedAsync(db => db.Add(ApiData.Doctor(doctorId)));

        using var client = factory.CreateClientFor(doctorId);
        var unknownPatientId = Guid.NewGuid();

        // Act
        var profileResponse = await client.GetAsync(ProfileUrl(unknownPatientId));
        var historyResponse = await client.GetAsync($"{HistoryUrl(unknownPatientId)}?pageNumber=1&pageSize=10");

        // Assert
        foreach (var response in new[] { profileResponse, historyResponse })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            Assert.Equal("patient.not_found", body.RootElement.GetProperty("code").GetString());
        }
    }
}
