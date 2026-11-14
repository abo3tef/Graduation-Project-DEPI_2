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

    private static string NotesUrl(Guid patientId) => $"/api/patients/{patientId}/notes";

    private static string LabResultsUrl(Guid patientId) => $"/api/patients/{patientId}/lab-results";

    private static string TimelineUrl(Guid patientId) => $"/api/patients/{patientId}/timeline";

    private static string DocumentsUrl(Guid patientId) => $"/api/patients/{patientId}/documents";

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

    // Note: CreatedAtUtc is deliberately not seedable — the audit interceptor
    // assigns it on insert (see the ordering test below).
    private static MedicalNote Note(
        Guid id,
        Guid patientId,
        Guid doctorId,
        Guid? visitId = null) => new()
    {
        Id = id,
        PatientId = patientId,
        DoctorId = doctorId,
        VisitId = visitId,
        NoteContent = "test-only note",
    };

    private static LaboratoryResult LabResult(
        Guid id,
        Guid patientId,
        Guid? visitId = null,
        DateOnly? testDate = null) => new()
    {
        Id = id,
        PatientId = patientId,
        VisitId = visitId,
        TestName = "Hemoglobin",
        TestDate = testDate ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
        Value = 13.5m,
        Unit = "g/dL",
    };

    /// <summary>
    /// Seeds a document with a sentinel storage location. Unlike the read models,
    /// the persisted entity requires <see cref="MedicalDocument.Location"/>; every
    /// F3-C3b test proves that this sentinel never reaches a response.
    /// </summary>
    private static MedicalDocument MedicalDocument(
        Guid id,
        Guid patientId,
        DateTimeOffset uploadDate,
        Guid? uploadedBy = null,
        string fileName = "report.pdf",
        MedicalDocumentType type = MedicalDocumentType.Pdf,
        string? description = null) => new()
    {
        Id = id,
        PatientId = patientId,
        UploadedBy = uploadedBy,
        FileName = fileName,
        Type = type,
        Location = "sentinel-storage-location-must-not-leak",
        UploadDate = uploadDate,
        Description = description,
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
        var medicalRoutes = new[]
        {
            ProfileUrl(patientId),
            $"{HistoryUrl(patientId)}?pageNumber=1&pageSize=10",
            $"{NotesUrl(patientId)}?pageNumber=1&pageSize=10",
            $"{LabResultsUrl(patientId)}?pageNumber=1&pageSize=10",
            $"{TimelineUrl(patientId)}?pageNumber=1&pageSize=10",
            $"{DocumentsUrl(patientId)}?pageNumber=1&pageSize=10"
        };

        foreach (var url in medicalRoutes)
        {
            var response = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            var root = body.RootElement;
            Assert.True(root.TryGetProperty("code", out _), "The 403 must use the shared envelope.");
            Assert.False(
                root.TryGetProperty("fullName", out _) || root.TryGetProperty("bloodType", out _) ||
                root.TryGetProperty("noteContent", out _) || root.TryGetProperty("testName", out _) ||
                root.TryGetProperty("fileName", out _),
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
        var medicalRoutes = new[]
        {
            ProfileUrl(patientId),
            $"{HistoryUrl(patientId)}?pageNumber=1&pageSize=10",
            $"{NotesUrl(patientId)}?pageNumber=1&pageSize=10",
            $"{LabResultsUrl(patientId)}?pageNumber=1&pageSize=10",
            $"{TimelineUrl(patientId)}?pageNumber=1&pageSize=10",
            $"{DocumentsUrl(patientId)}?pageNumber=1&pageSize=10"
        };

        foreach (var url in medicalRoutes)
        {
            var response = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            var root = body.RootElement;
            Assert.Equal("patient.not_found", root.GetProperty("code").GetString());
            Assert.False(
                root.TryGetProperty("fullName", out _) || root.TryGetProperty("bloodType", out _) ||
                root.TryGetProperty("noteContent", out _) || root.TryGetProperty("testName", out _) ||
                root.TryGetProperty("fileName", out _),
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
        var medicalRoutes = new[]
        {
            ProfileUrl(patientId),
            $"{HistoryUrl(patientId)}?pageNumber=1&pageSize=10",
            $"{NotesUrl(patientId)}?pageNumber=1&pageSize=10",
            $"{LabResultsUrl(patientId)}?pageNumber=1&pageSize=10",
            $"{TimelineUrl(patientId)}?pageNumber=1&pageSize=10",
            $"{DocumentsUrl(patientId)}?pageNumber=1&pageSize=10"
        };

        foreach (var url in medicalRoutes)
        {
            var response = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            Assert.False(
                body.RootElement.TryGetProperty("fullName", out _) ||
                body.RootElement.TryGetProperty("noteContent", out _),
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
    [InlineData("medical-visits", "pageNumber=0")]
    [InlineData("medical-visits", "pageSize=0")]
    [InlineData("medical-visits", "pageSize=51")]
    [InlineData("notes", "pageNumber=0")]
    [InlineData("notes", "pageSize=51")]
    [InlineData("lab-results", "pageNumber=0")]
    [InlineData("lab-results", "pageSize=51")]
    [InlineData("timeline", "pageNumber=0")]
    [InlineData("timeline", "pageSize=51")]
    [InlineData("documents", "pageNumber=0")]
    [InlineData("documents", "pageSize=51")]
    public async Task Invalid_paging_returns_400_with_the_shared_validation_envelope(
        string route,
        string query)
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
        var response = await client.GetAsync($"/api/patients/{patientId}/{route}?{query}");

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
        var routes = new[]
        {
            ProfileUrl(unknownPatientId),
            $"{HistoryUrl(unknownPatientId)}?pageNumber=1&pageSize=10",
            $"{NotesUrl(unknownPatientId)}?pageNumber=1&pageSize=10",
            $"{LabResultsUrl(unknownPatientId)}?pageNumber=1&pageSize=10",
            $"{TimelineUrl(unknownPatientId)}?pageNumber=1&pageSize=10",
            $"{DocumentsUrl(unknownPatientId)}?pageNumber=1&pageSize=10"
        };

        // Assert
        foreach (var url in routes)
        {
            var response = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            Assert.Equal("patient.not_found", body.RootElement.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task Notes_return_every_patient_record_in_deterministic_order_with_visit_links()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var linkedVisitId = Guid.NewGuid();

        // CreatedAtUtc is system-assigned on insert and deliberately protected
        // afterwards by the audit interceptor (tests must not backdate it), and
        // every row of one SaveChanges shares the same instant. This test
        // therefore exercises the P-C tie-breaker deterministically on the real
        // provider: with equal creation instants the descending-id order fully
        // decides. The ids are crafted to differ only in the final byte, so they
        // order identically as SQLite TEXT, MySQL CHAR(36)/BINARY(16), and under
        // .NET Guid.CompareTo, and they agree with the intended age order. The
        // ascending-timestamp path itself is proven against the store contract by
        // the Application-level tests (Notes_are_newest_first); date-descending
        // SQL ordering is proven below by the lab-results test, whose TestDate is
        // a plain domain field the interceptor does not touch.
        var newestId = new Guid("00000000-0000-0000-0000-000000000003");
        var middleId = new Guid("00000000-0000-0000-0000-000000000002");
        var oldestId = new Guid("00000000-0000-0000-0000-000000000001");

        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
            db.Add(ApiData.Visit(linkedVisitId, patientId, doctorId));
            db.Add(Note(oldestId, patientId, doctorId));
            db.Add(Note(middleId, patientId, doctorId));
            db.Add(Note(newestId, patientId, doctorId, linkedVisitId));
        });

        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.GetAsync($"{NotesUrl(patientId)}?pageNumber=1&pageSize=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var root = body.RootElement;
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(2, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, root.GetProperty("totalPages").GetInt32());

        var items = root.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal(newestId, items[0].GetProperty("id").GetGuid());
        Assert.Equal(linkedVisitId, items[0].GetProperty("visitId").GetGuid());
        Assert.Equal(doctorId, items[0].GetProperty("doctorId").GetGuid());
        Assert.Equal("Doctor Under Test", items[0].GetProperty("doctorName").GetString());
        Assert.Equal(middleId, items[1].GetProperty("id").GetGuid());
        Assert.True(items[1].TryGetProperty("visitId", out var middleVisitId));
        Assert.Equal(JsonValueKind.Null, middleVisitId.ValueKind);
    }

    [Fact]
    public async Task Lab_results_return_every_patient_record_newest_test_first()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var linkedVisitId = Guid.NewGuid();
        var newestId = Guid.NewGuid();
        var unlinkedId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
            db.Add(ApiData.Visit(linkedVisitId, patientId, doctorId));
            db.Add(LabResult(newestId, patientId, linkedVisitId, today));
            db.Add(LabResult(unlinkedId, patientId, null, today.AddDays(-1)));
        });

        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.GetAsync($"{LabResultsUrl(patientId)}?pageNumber=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var root = body.RootElement;
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(10, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, root.GetProperty("totalCount").GetInt32());

        var items = root.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal(newestId, items[0].GetProperty("id").GetGuid());
        Assert.Equal(linkedVisitId, items[0].GetProperty("visitId").GetGuid());
        Assert.Equal("Hemoglobin", items[0].GetProperty("testName").GetString());
        Assert.Equal(unlinkedId, items[1].GetProperty("id").GetGuid());
        Assert.True(items[1].TryGetProperty("visitId", out var unlinkedVisitId));
        Assert.Equal(JsonValueKind.Null, unlinkedVisitId.ValueKind);
    }

    [Fact]
    public async Task Lab_results_beyond_the_last_page_return_200_with_empty_items()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
            db.Add(LabResult(Guid.NewGuid(), patientId, null, DateOnly.FromDateTime(DateTime.UtcNow)));
        });

        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.GetAsync($"{LabResultsUrl(patientId)}?pageNumber=2&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var root = body.RootElement;
        Assert.Equal(2, root.GetProperty("page").GetInt32());
        Assert.Equal(0, root.GetProperty("items").GetArrayLength());
        Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, root.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Notes_with_no_records_return_200_with_an_empty_page()
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
        var response = await client.GetAsync(NotesUrl(patientId));

        // Assert: omitted query uses the shared defaults, empty result set stays a success.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var root = body.RootElement;
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(10, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(0, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, root.GetProperty("totalPages").GetInt32());
        Assert.Equal(0, root.GetProperty("items").GetArrayLength());
    }

    /// <summary>
    /// F3-C3b Q-12: the timeline is one chronological feed over the four persisted
    /// event sources. Seeded instants are chosen so the global order is provider
    /// independent: the audit interceptor stamps the note at the SaveChanges
    /// instant (newest), the documents are backdated relative to each other, the
    /// visit sits two days back, and the lab event is midnight UTC of today —
    /// always later than any instant ≥ 48h ago and earlier than any instant today.
    /// A second patient of the same doctor proves event isolation, and the raw
    /// body proves the document storage location never leaks (Q-13).
    /// </summary>
    [Fact]
    public async Task Timeline_returns_all_four_event_types_newest_first_with_document_metadata()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var otherPatientId = Guid.NewGuid();
        var visitId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        var labId = Guid.NewGuid();
        var namedDocId = Guid.NewGuid();
        var anonymousDocId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
            db.Add(ApiData.Patient(otherPatientId, doctorId));
            db.Add(ApiData.Visit(visitId, patientId, doctorId));
            db.Add(ApiData.Diagnosis(Guid.NewGuid(), visitId, patientId));
            db.Add(Note(noteId, patientId, doctorId, visitId));
            db.Add(LabResult(labId, patientId, null, DateOnly.FromDateTime(now.UtcDateTime)));
            db.Add(MedicalDocument(namedDocId, patientId, now.AddHours(-2), doctorId, "mri-scan.pdf",
                MedicalDocumentType.Pdf, "MRI of the knee"));
            db.Add(MedicalDocument(anonymousDocId, patientId, now.AddHours(-1), null, "x-ray.png",
                MedicalDocumentType.Png));
            // Another patient of the same doctor: these events must never leak in.
            db.Add(VisitAt(Guid.NewGuid(), otherPatientId, doctorId, now.AddHours(-3)));
            db.Add(MedicalDocument(Guid.NewGuid(), otherPatientId, now.AddHours(-3)));
        });

        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.GetAsync($"{TimelineUrl(patientId)}?pageNumber=1&pageSize=10");

        // Assert: shared envelope with only this patient's five events.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rawBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("location", rawBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sentinel", rawBody, StringComparison.OrdinalIgnoreCase);

        using var body = await ReadJsonAsync(response);
        var root = body.RootElement;
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(10, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(5, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, root.GetProperty("totalPages").GetInt32());

        // Global order: note (SaveChanges instant), documents (newest first),
        // lab (midnight UTC of today — later than any instant ≥ 48h ago),
        // visit (two days back).
        var items = root.GetProperty("items");
        Assert.Equal(5, items.GetArrayLength());
        Assert.Equal(
            new[] { noteId, anonymousDocId, namedDocId, labId, visitId },
            items.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray());
        Assert.Equal(
            new[] { "note", "document", "document", "lab_result", "visit" },
            items.EnumerateArray().Select(item => item.GetProperty("eventType").GetString()).ToArray());

        // Note event: full content (D-T2) and the owning visit id for deep-linking.
        var noteEvent = items[0];
        Assert.Equal(visitId, noteEvent.GetProperty("visitId").GetGuid());
        Assert.Equal("test-only note", noteEvent.GetProperty("note").GetProperty("noteContent").GetString());
        Assert.Equal("Doctor Under Test", noteEvent.GetProperty("note").GetProperty("doctorName").GetString());

        // Document events: metadata only, no location anywhere (Q-13); the newer
        // upload has no uploader (D-T5) and therefore no uploader name.
        var anonymousEvent = items[1];
        Assert.True(anonymousEvent.TryGetProperty("visitId", out var anonymousVisitId));
        Assert.Equal(JsonValueKind.Null, anonymousVisitId.ValueKind);
        Assert.Equal("x-ray.png", anonymousEvent.GetProperty("document").GetProperty("fileName").GetString());
        Assert.Equal("Png", anonymousEvent.GetProperty("document").GetProperty("fileType").GetString());
        Assert.Null(anonymousEvent.GetProperty("document").GetProperty("uploadedByDoctorName").GetString());

        var namedEvent = items[2];
        Assert.Equal("mri-scan.pdf", namedEvent.GetProperty("document").GetProperty("fileName").GetString());
        Assert.Equal("MRI of the knee", namedEvent.GetProperty("document").GetProperty("description").GetString());
        Assert.Equal("Doctor Under Test", namedEvent.GetProperty("document").GetProperty("uploadedByDoctorName").GetString());

        // Lab event: dated at midnight UTC of the test date (documented
        // convention); payload carries name, value and unit.
        var labEvent = items[3];
        Assert.True(labEvent.TryGetProperty("visitId", out var labVisitId));
        Assert.Equal(JsonValueKind.Null, labVisitId.ValueKind);
        Assert.Equal("Hemoglobin", labEvent.GetProperty("labResult").GetProperty("testName").GetString());
        Assert.Equal("g/dL", labEvent.GetProperty("labResult").GetProperty("unit").GetString());

        // Visit event: a summary with child counts (D-T1), never the full visit.
        var visitEvent = items[4];
        Assert.Equal(visitId, visitEvent.GetProperty("visitId").GetGuid());
        var visitPayload = visitEvent.GetProperty("visit");
        Assert.Equal("Doctor Under Test", visitPayload.GetProperty("doctorName").GetString());
        Assert.Equal(1, visitPayload.GetProperty("diagnosesCount").GetInt32());
        Assert.Equal(0, visitPayload.GetProperty("medicationsCount").GetInt32());
        // The linked note (noteId) counts toward the visit's note count.
        Assert.Equal(1, visitPayload.GetProperty("notesCount").GetInt32());
        Assert.Equal(0, visitPayload.GetProperty("labResultsCount").GetInt32());
        Assert.True(visitEvent.TryGetProperty("note", out var visitNotePayload));
        Assert.Equal(JsonValueKind.Null, visitNotePayload.ValueKind);
    }

    /// <summary>
    /// The Q-12 tie-breaker on equal instants: with occurredAtUtc equal the
    /// ordinal event type decides before the id — "document" &lt; "visit" — so
    /// the document event precedes the visit event even when the visit has the
    /// smaller id. This proves the event type outranks the id.
    /// </summary>
    [Fact]
    public async Task Timeline_equal_instants_are_broken_by_event_type_before_id()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var sameInstant = DateTimeOffset.UtcNow.AddDays(-1);

        // The visit deliberately has the smaller id: an id-first ordering would
        // put it first and fail this test.
        var visitId = new Guid("00000000-0000-0000-0000-000000000001");
        var documentId = new Guid("00000000-0000-0000-0000-000000000002");

        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
            db.Add(VisitAt(visitId, patientId, doctorId, sameInstant));
            db.Add(MedicalDocument(documentId, patientId, sameInstant));
        });

        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.GetAsync($"{TimelineUrl(patientId)}?pageNumber=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var items = body.RootElement.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal("document", items[0].GetProperty("eventType").GetString());
        Assert.Equal(documentId, items[0].GetProperty("id").GetGuid());
        Assert.Equal("visit", items[1].GetProperty("eventType").GetString());
        Assert.Equal(visitId, items[1].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Timeline_beyond_the_last_page_returns_200_with_empty_items()
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
        var response = await client.GetAsync($"{TimelineUrl(patientId)}?pageNumber=2&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var root = body.RootElement;
        Assert.Equal(2, root.GetProperty("page").GetInt32());
        Assert.Equal(0, root.GetProperty("items").GetArrayLength());
        Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, root.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Timeline_with_no_events_returns_200_with_an_empty_page()
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
        var response = await client.GetAsync(TimelineUrl(patientId));

        // Assert: omitted query uses the shared defaults, empty result set stays a success.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var root = body.RootElement;
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(10, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(0, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, root.GetProperty("totalPages").GetInt32());
        Assert.Equal(0, root.GetProperty("items").GetArrayLength());
    }

    /// <summary>
    /// F3-C3b Q-13: the documents endpoint lists metadata only — file name, type,
    /// upload date, description and the uploader's name — newest upload first,
    /// isolated to the requested patient. The raw serialized body is asserted
    /// directly: no <c>location</c>, no storage path and no file URL may exist
    /// anywhere in the response, for any page.
    /// </summary>
    [Fact]
    public async Task Documents_return_metadata_only_and_never_expose_the_storage_location()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var otherPatientId = Guid.NewGuid();
        var olderId = Guid.NewGuid();
        var newerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
            db.Add(ApiData.Patient(otherPatientId, doctorId));
            db.Add(MedicalDocument(olderId, patientId, now.AddDays(-2), doctorId, "report-2029.pdf",
                MedicalDocumentType.Pdf, "Annual report"));
            db.Add(MedicalDocument(newerId, patientId, now.AddDays(-1), null, "scan.jpg",
                MedicalDocumentType.Jpg));
            // Non-document rows and another patient's rows must stay out.
            db.Add(VisitAt(Guid.NewGuid(), patientId, doctorId, now.AddDays(-3)));
            db.Add(MedicalDocument(Guid.NewGuid(), otherPatientId, now));
        });

        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.GetAsync($"{DocumentsUrl(patientId)}?pageNumber=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rawBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("location", rawBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sentinel", rawBody, StringComparison.OrdinalIgnoreCase);

        using var body = await ReadJsonAsync(response);
        var root = body.RootElement;
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(10, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, root.GetProperty("totalPages").GetInt32());

        var items = root.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal(newerId, items[0].GetProperty("id").GetGuid());
        Assert.Equal("scan.jpg", items[0].GetProperty("fileName").GetString());
        Assert.Equal("Jpg", items[0].GetProperty("type").GetString());
        Assert.Null(items[0].GetProperty("uploadedByDoctorName").GetString()); // D-T5

        Assert.Equal(olderId, items[1].GetProperty("id").GetGuid());
        Assert.Equal("report-2029.pdf", items[1].GetProperty("fileName").GetString());
        Assert.Equal("Pdf", items[1].GetProperty("type").GetString());
        Assert.Equal("Doctor Under Test", items[1].GetProperty("uploadedByDoctorName").GetString());
        Assert.Equal("Annual report", items[1].GetProperty("description").GetString());

        // Act: beyond the last page stays a success with no data.
        var secondPage = await client.GetAsync($"{DocumentsUrl(patientId)}?pageNumber=2&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, secondPage.StatusCode);

        var secondRaw = await secondPage.Content.ReadAsStringAsync();
        Assert.DoesNotContain("location", secondRaw, StringComparison.OrdinalIgnoreCase);

        using var secondBody = await ReadJsonAsync(secondPage);
        Assert.Equal(0, secondBody.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(2, secondBody.RootElement.GetProperty("totalCount").GetInt32());
    }
}
