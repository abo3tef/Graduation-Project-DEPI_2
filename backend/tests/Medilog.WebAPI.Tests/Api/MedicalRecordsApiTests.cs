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

    private static string TimelineUrl(Guid patientId) => $"/api/patients/{patientId}/timeline";

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

    private static MedicalNote Note(Guid id, Guid patientId, Guid doctorId, Guid? visitId) => new()
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
        foreach (var url in new[]
                 {
                     ProfileUrl(patientId),
                     $"{HistoryUrl(patientId)}?pageNumber=1&pageSize=10",
                     $"{TimelineUrl(patientId)}?pageNumber=1&pageSize=10"
                 })
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
        foreach (var url in new[]
                 {
                     ProfileUrl(patientId),
                     $"{HistoryUrl(patientId)}?pageNumber=1&pageSize=10",
                     $"{TimelineUrl(patientId)}?pageNumber=1&pageSize=10"
                 })
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
        foreach (var url in new[]
                 {
                     ProfileUrl(patientId),
                     $"{HistoryUrl(patientId)}?pageNumber=1&pageSize=10",
                     $"{TimelineUrl(patientId)}?pageNumber=1&pageSize=10"
                 })
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
        var timelineResponse = await client.GetAsync($"{TimelineUrl(unknownPatientId)}?pageNumber=1&pageSize=10");

        // Assert
        foreach (var response in new[] { profileResponse, historyResponse, timelineResponse })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            Assert.Equal("patient.not_found", body.RootElement.GetProperty("code").GetString());
        }
    }

    // ---------------------------------------------------------------------
    // F10-C1: GET /api/patients/{id}/timeline — visits, diagnoses, medications
    // and clinical notes in one newest-first chronological feed (Q-20).
    // ---------------------------------------------------------------------

    /// <summary>
    /// Seeds one visit (id …0001, one day back) with a diagnosis (id …0002,
    /// yesterday), two prescription items (ids …0003, …0005) and one unlinked
    /// clinical note (id …0004, stamped at seed time — always the newest).
    /// The global order is therefore note, then the shared visit instant
    /// (medications first: "Medication" &lt; "Visit"), then the diagnosis at
    /// midnight UTC of yesterday — five events, independent of the wall clock.
    /// </summary>
    private static async Task<(Guid PatientId, Guid DoctorId, Guid VisitId, Guid NoteId)> SeedTimelineAsync(
        WebApiFactory factory)
    {
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var visitId = new Guid("00000000-0000-0000-0000-000000000001");
        var noteId = new Guid("00000000-0000-0000-0000-000000000004");

        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));

            var visit = VisitAt(visitId, patientId, doctorId, DateTimeOffset.UtcNow.AddDays(-1));
            visit.ChiefComplaint = "Persistent headache";
            db.Add(visit);
            db.Add(ApiData.Diagnosis(
                new Guid("00000000-0000-0000-0000-000000000002"), visitId, patientId, "Tension headache"));
            db.Add(ApiData.Medication(new Guid("00000000-0000-0000-0000-000000000003"), visitId, patientId));
            db.Add(ApiData.Medication(new Guid("00000000-0000-0000-0000-000000000005"), visitId, patientId));
            db.Add(Note(noteId, patientId, doctorId, null));
        });

        return (patientId, doctorId, visitId, noteId);
    }

    [Fact]
    public async Task Timeline_returns_the_Q04_envelope_mixing_all_event_types_newest_first()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var (patientId, doctorId, visitId, noteId) = await SeedTimelineAsync(factory);
        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.GetAsync($"{TimelineUrl(patientId)}?pageNumber=1&pageSize=10");

        // Assert: the shared envelope.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var page = body.RootElement;
        Assert.Equal(1, page.GetProperty("page").GetInt32());
        Assert.Equal(10, page.GetProperty("pageSize").GetInt32());
        Assert.Equal(5, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, page.GetProperty("totalPages").GetInt32());

        // Assert: the global order — note, medications, visit, diagnosis.
        var items = page.GetProperty("items");
        Assert.Equal(5, items.GetArrayLength());
        Assert.Equal(
            new[]
            {
                "MedicalNote", "Medication", "Medication", "Visit", "Diagnosis"
            },
            items.EnumerateArray().Select(@event => @event.GetProperty("type").GetString()).ToArray());
        Assert.Equal(
            new[]
            {
                noteId,
                new Guid("00000000-0000-0000-0000-000000000003"),
                new Guid("00000000-0000-0000-0000-000000000005"),
                visitId,
                new Guid("00000000-0000-0000-0000-000000000002")
            },
            items.EnumerateArray().Select(@event => @event.GetProperty("id").GetGuid()).ToArray());

        // Assert: the display projection per type.
        var visitEvent = items[3];
        Assert.Equal("Medical Visit", visitEvent.GetProperty("title").GetString());
        Assert.Equal("Persistent headache", visitEvent.GetProperty("summary").GetString());
        Assert.Equal(visitId, visitEvent.GetProperty("relatedVisitId").GetGuid());

        var medicationEvent = items[1];
        Assert.Equal("Amoxicillin", medicationEvent.GetProperty("title").GetString());
        Assert.Equal("500 mg · Every 8 hours", medicationEvent.GetProperty("summary").GetString());
        Assert.Equal(visitId, medicationEvent.GetProperty("relatedVisitId").GetGuid());

        var diagnosisEvent = items[4];
        Assert.Equal("Tension headache", diagnosisEvent.GetProperty("title").GetString());
        Assert.Equal("Tension headache description", diagnosisEvent.GetProperty("summary").GetString());
        Assert.Equal(visitId, diagnosisEvent.GetProperty("relatedVisitId").GetGuid());

        var noteEvent = items[0];
        Assert.Equal("Clinical Note", noteEvent.GetProperty("title").GetString());
        Assert.Equal("test-only note", noteEvent.GetProperty("summary").GetString());
        Assert.Equal(
            JsonValueKind.Null,
            noteEvent.GetProperty("relatedVisitId").ValueKind);
    }

    [Fact]
    public async Task Timeline_eventType_filter_is_case_insensitive_and_scopes_the_total()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var (patientId, doctorId, visitId, _) = await SeedTimelineAsync(factory);
        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.GetAsync($"{TimelineUrl(patientId)}?eventType=visit");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var page = body.RootElement;
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, page.GetProperty("items").GetArrayLength());
        Assert.Equal("Visit", page.GetProperty("items")[0].GetProperty("type").GetString());
        Assert.Equal(visitId, page.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Timeline_from_to_window_keeps_only_events_inside_it()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var recentVisitId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
            db.Add(VisitAt(recentVisitId, patientId, doctorId, DateTimeOffset.UtcNow.AddDays(-1)));
            db.Add(VisitAt(Guid.NewGuid(), patientId, doctorId, DateTimeOffset.UtcNow.AddDays(-10)));
            db.Add(new Diagnosis
            {
                Id = Guid.NewGuid(),
                VisitId = recentVisitId,
                PatientId = patientId,
                DiagnosisName = "Recent",
                DiagnosisDate = DateOnly.FromDateTime(DateTime.UtcNow),
            });
            db.Add(Note(noteId, patientId, doctorId, null));
        });
        using var client = factory.CreateClientFor(doctorId);

        // Act: a five-day window. Inside: the note (stamped now), today's
        // diagnosis (its midnight-UTC instant) and the recent visit. Outside:
        // the ten-day-old visit.
        var response = await client.GetAsync(
            $"{TimelineUrl(patientId)}?from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-5).ToString("O"))}" +
            $"&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"))}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var page = body.RootElement;
        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(
            new[] { "MedicalNote", "Diagnosis", "Visit" },
            page.GetProperty("items").EnumerateArray().Select(@event => @event.GetProperty("type").GetString()).ToArray());
        Assert.Equal(
            new[] { noteId, recentVisitId },
            new[]
            {
                page.GetProperty("items")[0].GetProperty("id").GetGuid(),
                page.GetProperty("items")[2].GetProperty("id").GetGuid()
            });
    }

    [Fact]
    public async Task Timeline_pages_are_stable_without_repeats_up_to_the_empty_last_page()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var (patientId, doctorId, _, noteId) = await SeedTimelineAsync(factory);
        using var client = factory.CreateClientFor(doctorId);

        // Act
        var first = await client.GetAsync($"{TimelineUrl(patientId)}?pageNumber=1&pageSize=2");
        var second = await client.GetAsync($"{TimelineUrl(patientId)}?pageNumber=2&pageSize=2");
        var third = await client.GetAsync($"{TimelineUrl(patientId)}?pageNumber=3&pageSize=2");
        var beyond = await client.GetAsync($"{TimelineUrl(patientId)}?pageNumber=4&pageSize=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
        Assert.Equal(HttpStatusCode.OK, beyond.StatusCode);

        using var firstBody = await ReadJsonAsync(first);
        using var secondBody = await ReadJsonAsync(second);
        using var thirdBody = await ReadJsonAsync(third);
        using var beyondBody = await ReadJsonAsync(beyond);

        foreach (var root in new[] { firstBody.RootElement, secondBody.RootElement, thirdBody.RootElement })
        {
            Assert.Equal(5, root.GetProperty("totalCount").GetInt32());
        }

        Assert.Equal(2, firstBody.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(2, secondBody.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(1, thirdBody.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(0, beyondBody.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(3, beyondBody.RootElement.GetProperty("totalPages").GetInt32());

        var allIds = firstBody.RootElement.GetProperty("items").EnumerateArray()
            .Concat(secondBody.RootElement.GetProperty("items").EnumerateArray())
            .Concat(thirdBody.RootElement.GetProperty("items").EnumerateArray())
            .Select(@event => @event.GetProperty("id").GetGuid())
            .ToArray();

        Assert.Equal(5, allIds.Length);
        Assert.Equal(allIds.Length, allIds.Distinct().Count()); // pages never repeat an event
        Assert.Equal(noteId, allIds[0]); // the note stays the newest across pages
    }

    [Fact]
    public async Task Timeline_tie_breaks_equal_instants_by_type_then_id()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(ApiData.Doctor(doctorId));
            db.Add(ApiData.Patient(patientId, doctorId));
            // Both events sit at 2030-01-05 00:00:00 UTC: the visit instant and
            // midnight UTC of the diagnosis day. The visit deliberately has the
            // smaller id, so only the type tie-breaker can order them.
            db.Add(VisitAt(
                new Guid("00000000-0000-0000-0000-000000000001"),
                patientId,
                doctorId,
                new DateTimeOffset(2030, 1, 5, 0, 0, 0, TimeSpan.Zero)));
            db.Add(new Diagnosis
            {
                Id = new Guid("00000000-0000-0000-0000-000000000002"),
                VisitId = new Guid("00000000-0000-0000-0000-000000000001"),
                PatientId = patientId,
                DiagnosisName = "Same-day diagnosis",
                DiagnosisDate = new DateOnly(2030, 1, 5),
            });
        });
        using var client = factory.CreateClientFor(doctorId);

        // Act
        var response = await client.GetAsync($"{TimelineUrl(patientId)}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var items = body.RootElement.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal("Diagnosis", items[0].GetProperty("type").GetString()); // "Diagnosis" < "Visit"
        Assert.Equal("Visit", items[1].GetProperty("type").GetString());
    }

    [Fact]
    public async Task Timeline_huge_page_number_returns_200_with_empty_items_and_correct_metadata()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var (patientId, doctorId, _, _) = await SeedTimelineAsync(factory);
        using var client = factory.CreateClientFor(doctorId);

        // Act: the largest bindable page number. The overflow regression made
        // (pageNumber - 1) * pageSize wrap into a negative OFFSET (a real SQL
        // error on MySQL); it must instead be a plain beyond-last page.
        var response = await client.GetAsync($"{TimelineUrl(patientId)}?pageNumber={int.MaxValue}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var root = body.RootElement;
        Assert.Equal(int.MaxValue, root.GetProperty("page").GetInt32());
        Assert.Equal(0, root.GetProperty("items").GetArrayLength());
        Assert.Equal(5, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, root.GetProperty("totalPages").GetInt32());
    }

    [Theory]
    [InlineData("pageNumber=0", "pageNumber")]
    [InlineData("pageSize=51", "pageSize")]
    [InlineData("eventType=Bogus", "eventType")]
    [InlineData("from=2030-01-05T00:00:00Z&to=2030-01-01T00:00:00Z", "from")]
    public async Task Invalid_timeline_filters_return_400_with_the_shared_validation_envelope(
        string query,
        string expectedField)
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
        var response = await client.GetAsync($"{TimelineUrl(patientId)}?{query}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var root = body.RootElement;
        Assert.Equal("validation.failed", root.GetProperty("code").GetString());
        Assert.True(root.TryGetProperty("errors", out var errors), "The 400 must list field errors.");
        Assert.True(
            errors.TryGetProperty(expectedField, out _),
            $"The '{expectedField}' error must be reported.");

        Assert.True(root.TryGetProperty("errorCodes", out var errorCodes));
        Assert.True(errorCodes.TryGetProperty(expectedField, out _));
    }
}
