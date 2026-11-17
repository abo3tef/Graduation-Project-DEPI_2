using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.MedicalRecords;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Xunit;

namespace Medilog.Application.Tests;

/// <summary>
/// The F10-C1 patient timeline contract: one chronological feed over visits,
/// diagnoses, medications and clinical notes (Q-20), newest first with the
/// type-then-id tie-breaker, filtered by event type and date window, paged at
/// the data source with an accurate total count. Ordering runs through the real
/// EF Core InMemory provider (D-15 pattern); authorization still resolves
/// through the medical-record store, matching the production wiring.
/// </summary>
public class PatientTimelineTests
{
    // Instants chosen so the global order is independent of the wall clock:
    // the note is stamped by the audit hook at the SaveChanges instant (always
    // the newest), the visit sits one day back, the diagnosis at midnight UTC
    // of yesterday (earlier than the visit's later time-of-day), and the
    // medication inherits the same parent-visit instant and sorts before the
    // visit through the type tie-breaker ("Medication" < "Visit").
    private static readonly DateTimeOffset VisitDate = DateTimeOffset.UtcNow.AddDays(-1);
    private static readonly DateOnly DiagnosisDay = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

    private static readonly Guid VisitId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid DiagnosisId = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid MedicationId = new("00000000-0000-0000-0000-000000000003");
    private static readonly Guid NoteId = new("00000000-0000-0000-0000-000000000004");

    /// <summary>Seeds the four event types and returns the persisted note (newest).</summary>
    private static MedicalNote SeedMixedEvents(MedicalRecordScenario s)
    {
        var visit = s.SeedDbVisit(s.PatientA, VisitDate, "Persistent headache", VisitId);
        s.SeedDbDiagnosis(visit, "Tension headache", DiagnosisDay, DiagnosisId);
        s.SeedDbMedication(visit, id: MedicationId);

        return s.SeedDbNote(s.PatientA, null, "Follow up in one week.", NoteId);
    }

    [Fact]
    public async Task Timeline_mixes_all_four_event_types_newest_first_with_display_fields()
    {
        var s = new MedicalRecordScenario();
        var note = SeedMixedEvents(s);
        s.ActAsDoctor(s.DoctorA);

        var page = await s.TimelineAsync();

        Assert.Equal(4, page.TotalCount);
        // Newest first: the note, then the visit instant shared by its
        // medication (medication sorts first on the type tie-breaker), then
        // the diagnosis at midnight UTC of yesterday.
        Assert.Equal(
            new[] { note.Id, MedicationId, VisitId, DiagnosisId },
            page.Items.Select(@event => @event.Id).ToArray());
        Assert.Equal(
            new[]
            {
                PatientTimelineEventTypes.MedicalNote,
                PatientTimelineEventTypes.Medication,
                PatientTimelineEventTypes.Visit,
                PatientTimelineEventTypes.Diagnosis
            },
            page.Items.Select(@event => @event.Type).ToArray());

        // Visit event: title is fixed, summary is the chief complaint, and the
        // related visit points at the visit itself.
        var visitEvent = page.Items.Single(@event => @event.Type == PatientTimelineEventTypes.Visit);
        Assert.Equal("Medical Visit", visitEvent.Title);
        Assert.Equal("Persistent headache", visitEvent.Summary);
        Assert.Equal(VisitId, visitEvent.RelatedVisitId);
        Assert.Equal(VisitDate, visitEvent.OccurredAt);

        // Diagnosis event: the name is the title; the instant is midnight UTC
        // of the diagnosis day (the column has no time-of-day).
        var diagnosisEvent = page.Items.Single(@event => @event.Type == PatientTimelineEventTypes.Diagnosis);
        Assert.Equal("Tension headache", diagnosisEvent.Title);
        Assert.Equal("Tension headache description", diagnosisEvent.Summary);
        Assert.Equal(VisitId, diagnosisEvent.RelatedVisitId);
        Assert.Equal(new DateTimeOffset(DiagnosisDay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), diagnosisEvent.OccurredAt);

        // Medication event: the timeline instant is the parent visit's date —
        // here the same visit as above — and the summary composes dosage and
        // frequency.
        var medicationEvent = page.Items.Single(@event => @event.Type == PatientTimelineEventTypes.Medication);
        Assert.Equal("Amoxicillin", medicationEvent.Title);
        Assert.Equal("500 mg · Every 8 hours", medicationEvent.Summary);
        Assert.Equal(VisitId, medicationEvent.RelatedVisitId);
        Assert.Equal(VisitDate, medicationEvent.OccurredAt);

        // Note event: the audit-stamped creation instant and the full content.
        var noteEvent = page.Items.Single(@event => @event.Type == PatientTimelineEventTypes.MedicalNote);
        Assert.Equal("Clinical Note", noteEvent.Title);
        Assert.Equal("Follow up in one week.", noteEvent.Summary);
        Assert.Null(noteEvent.RelatedVisitId); // unlinked note
    }

    [Fact]
    public async Task Timeline_orders_equal_instants_by_type_then_id()
    {
        var s = new MedicalRecordScenario();
        var sameInstant = new DateTimeOffset(2030, 1, 5, 0, 0, 0, TimeSpan.Zero);

        // The visit deliberately has the smaller id: an id-first ordering would
        // put it first and fail this test.
        var visit = s.SeedDbVisit(s.PatientA, sameInstant, id: new Guid("00000000-0000-0000-0000-000000000001"));
        s.SeedDbDiagnosis(visit, "Migraine", new DateOnly(2030, 1, 5), new Guid("00000000-0000-0000-0000-000000000002"));
        s.ActAsDoctor(s.DoctorA);

        var page = await s.TimelineAsync();

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(PatientTimelineEventTypes.Diagnosis, page.Items[0].Type); // "Diagnosis" < "Visit"
        Assert.Equal(PatientTimelineEventTypes.Visit, page.Items[1].Type);
    }

    [Fact]
    public async Task Timeline_breaks_same_instant_notes_by_id()
    {
        var s = new MedicalRecordScenario();

        // Both notes are stamped by one SaveChanges batch, so their creation
        // instants are equal and only the id decides.
        var older = s.SeedDbNote(s.PatientA, null, "First note", new Guid("00000000-0000-0000-0000-000000000001"));
        var newer = s.SeedDbNote(s.PatientA, null, "Second note", new Guid("00000000-0000-0000-0000-000000000002"));
        s.ActAsDoctor(s.DoctorA);

        var page = await s.TimelineAsync();

        Assert.Equal(new[] { older.Id, newer.Id }, page.Items.Select(@event => @event.Id).ToArray());
    }

    [Fact]
    public async Task EventType_filter_accepts_case_insensitive_spellings()
    {
        var s = new MedicalRecordScenario();
        var visit = s.SeedDbVisit(s.PatientA, VisitDate);
        s.SeedDbDiagnosis(visit, "Migraine", DiagnosisDay);
        s.SeedDbNote(s.PatientA, null, "Note");
        s.ActAsDoctor(s.DoctorA);

        var visits = await s.TimelineAsync(eventType: "visit"); // lowercase input
        var notes = await s.TimelineAsync(eventType: "medicalNote"); // mixed-case input

        Assert.Equal(1, visits.TotalCount);
        Assert.All(visits.Items, @event => Assert.Equal(PatientTimelineEventTypes.Visit, @event.Type));
        Assert.Equal(1, notes.TotalCount);
        Assert.All(notes.Items, @event => Assert.Equal(PatientTimelineEventTypes.MedicalNote, @event.Type));
    }

    [Fact]
    public async Task EventType_filter_reports_the_filtered_total_count()
    {
        var s = new MedicalRecordScenario();
        var visit = s.SeedDbVisit(s.PatientA, VisitDate);
        s.SeedDbDiagnosis(visit, "Migraine", DiagnosisDay);
        s.SeedDbDiagnosis(visit, "Flu", DiagnosisDay.AddDays(-1));
        s.SeedDbNote(s.PatientA, null, "Note");
        s.ActAsDoctor(s.DoctorA);

        var page = await s.TimelineAsync(eventType: "diagnosis");

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
        Assert.All(page.Items, @event => Assert.Equal(PatientTimelineEventTypes.Diagnosis, @event.Type));
    }

    [Fact]
    public async Task EventType_filter_that_matches_nothing_returns_an_empty_page()
    {
        var s = new MedicalRecordScenario();
        s.SeedDbVisit(s.PatientA, VisitDate);
        s.ActAsDoctor(s.DoctorA);

        var page = await s.TimelineAsync(eventType: "Medication");

        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Date_window_filters_the_instant_sources_inclusively()
    {
        var s = new MedicalRecordScenario();
        var recentVisit = s.SeedDbVisit(s.PatientA, VisitDate); // one day back
        var oldVisit = s.SeedDbVisit(s.PatientA, DateTimeOffset.UtcNow.AddDays(-10));
        var recentMedication = s.SeedDbMedication(recentVisit);
        s.SeedDbMedication(oldVisit);
        var recentDiagnosis = s.SeedDbDiagnosis(recentVisit, "Recent", DateOnly.FromDateTime(DateTime.UtcNow));
        s.SeedDbDiagnosis(recentVisit, "Old", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10));
        var note = s.SeedDbNote(s.PatientA, null, "Note");
        s.ActAsDoctor(s.DoctorA);

        var page = await s.TimelineAsync(
            from: DateTimeOffset.UtcNow.AddDays(-5),
            to: DateTimeOffset.UtcNow.AddDays(1));

        // Inside the window: the note (stamped now), today's diagnosis (its
        // midnight-UTC instant), the recent visit and its medication (same
        // instant, medication first on the type tie-breaker). Outside:
        // everything ten days back.
        Assert.Equal(4, page.TotalCount);
        Assert.Equal(
            new[] { note.Id, recentDiagnosis.Id, recentMedication.Id, recentVisit.Id },
            page.Items.Select(@event => @event.Id).ToArray());
    }

    [Fact]
    public async Task Date_window_applies_to_diagnoses_in_utc_calendar_days()
    {
        var s = new MedicalRecordScenario();
        var visit = s.SeedDbVisit(s.PatientA, VisitDate); // one day back — below the window
        s.SeedDbDiagnosis(visit, "Recent", DateOnly.FromDateTime(DateTime.UtcNow));
        s.SeedDbDiagnosis(visit, "Tomorrow", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1));
        s.ActAsDoctor(s.DoctorA);

        // A single-instant window: only today's diagnosis fits its calendar day,
        // the visit sits below `from`, and the note is stamped after `to`.
        var page = await s.TimelineAsync(
            from: DateTimeOffset.UtcNow,
            to: DateTimeOffset.UtcNow);

        Assert.Equal(1, page.TotalCount);
        Assert.Equal(PatientTimelineEventTypes.Diagnosis, Assert.Single(page.Items).Type);
        Assert.Equal("Recent", Assert.Single(page.Items).Title);
    }

    [Fact]
    public async Task Timeline_pages_are_deterministic_and_stable()
    {
        var s = new MedicalRecordScenario();
        var note = SeedMixedEvents(s);

        // A second medication on the same (oldest) visit: equal instants, so the
        // crafted ids decide between the two. Its id deliberately differs from
        // the visit's (...0001) so the response ids stay unique.
        var firstMedication = s.SeedDbMedication(
            s.Db.MedicalVisits.Local.Single(visit => visit.Id == VisitId),
            name: "Paracetamol",
            id: new Guid("00000000-0000-0000-0000-000000000005"));
        s.ActAsDoctor(s.DoctorA);

        var page1 = await s.TimelineAsync(page: 1, pageSize: 2);
        var page2 = await s.TimelineAsync(page: 2, pageSize: 2);
        var page3 = await s.TimelineAsync(page: 3, pageSize: 2);
        var beyondLast = await s.TimelineAsync(page: 4, pageSize: 2);

        Assert.All(new[] { page1, page2, page3, beyondLast }, page => Assert.Equal(5, page.TotalCount));
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
        Assert.Single(page3.Items);
        Assert.Empty(beyondLast.Items);
        Assert.Equal(3, beyondLast.TotalPages);

        var allIds = page1.Items
            .Concat(page2.Items)
            .Concat(page3.Items)
            .Select(@event => @event.Id)
            .ToArray();
        Assert.Equal(allIds.Length, allIds.Distinct().Count()); // pages never repeat an event

        var noteFirst = allIds[0];
        Assert.Equal(note.Id, noteFirst);
        // The two equal-instant medications are separated by the id tie-breaker.
        Assert.True(Array.IndexOf(allIds, MedicationId) < Array.IndexOf(allIds, firstMedication.Id));
    }

    [Fact]
    public async Task Timeline_never_contains_another_patients_events()
    {
        var s = new MedicalRecordScenario();
        SeedMixedEvents(s);

        // LeapPatient is owned by the same acting doctor: its events must still
        // stay out of Patient A's timeline.
        var otherVisit = s.SeedDbVisit(s.LeapPatient, DateTimeOffset.UtcNow);
        s.SeedDbDiagnosis(otherVisit, "Other patient's diagnosis", DateOnly.FromDateTime(DateTime.UtcNow));
        s.SeedDbNote(s.LeapPatient, null, "Other patient's note");
        s.ActAsDoctor(s.DoctorA);

        var page = await s.TimelineAsync();

        Assert.Equal(4, page.TotalCount);
        // Exactly Patient A's four seeded events — the other patient's visit,
        // diagnosis and note must not appear even though the doctor owns both.
        Assert.Equal(
            new[] { NoteId, MedicationId, VisitId, DiagnosisId },
            page.Items.Select(@event => @event.Id).ToArray());
    }

    [Fact]
    public async Task Note_sorts_before_medication_at_the_same_instant()
    {
        var s = new MedicalRecordScenario();

        // The note's creation instant is assigned by the audit hook at insert
        // time and stays readable on the tracked instance, so the visit and its
        // medication can be seeded at exactly that instant. The true ordinal
        // type order at one shared instant is
        // MedicalNote < Medication < Visit.
        var note = s.SeedDbNote(s.PatientA, null, "Recorded during the visit");
        await s.Db.SaveChangesAsync();
        var visit = s.SeedDbVisit(s.PatientA, note.CreatedAtUtc);
        s.SeedDbMedication(visit);
        s.ActAsDoctor(s.DoctorA);

        var page = await s.TimelineAsync();

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(
            new[]
            {
                PatientTimelineEventTypes.MedicalNote,
                PatientTimelineEventTypes.Medication,
                PatientTimelineEventTypes.Visit
            },
            page.Items.Select(@event => @event.Type).ToArray());
    }

    [Fact]
    public async Task Medication_tie_breaks_in_canonical_guid_string_order()
    {
        var s = new MedicalRecordScenario();
        var visit = s.SeedDbVisit(
            s.PatientA, VisitDate, id: new Guid("00000000-0000-0000-0000-000000000001"));

        // The two ids order OPPOSITELY under .NET Guid comparison and canonical
        // string order: Guid compares the first group as a signed little-endian
        // int (0x80000000 is negative, so …80 sorts before …7f), while the
        // character order of the canonical form puts …7f before …80 ('7' < '8').
        // The contract tie-breaker is the canonical string order — the same
        // order the char(36) id column gives the SQL sources — so …7f must come
        // first. This test fails under a Guid-order tie-break.
        s.SeedDbMedication(visit, name: "Paracetamol", id: new Guid("7fffffff-0000-0000-0000-000000000001"));
        s.SeedDbMedication(visit, name: "Amoxicillin", id: new Guid("80000000-0000-0000-0000-000000000002"));
        s.ActAsDoctor(s.DoctorA);

        var page = await s.TimelineAsync();

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(
            new[]
            {
                new Guid("7fffffff-0000-0000-0000-000000000001"), // string order: '7' < '8'
                new Guid("80000000-0000-0000-0000-000000000002"),
                new Guid("00000000-0000-0000-0000-000000000001") // "Visit" after both medications
            },
            page.Items.Select(@event => @event.Id).ToArray());
    }

    [Fact]
    public async Task Extreme_page_number_returns_an_empty_page_with_correct_metadata()
    {
        var s = new MedicalRecordScenario();
        SeedMixedEvents(s);
        s.ActAsDoctor(s.DoctorA);

        // int.MaxValue page number binds cleanly; the overflow regression made
        // (pageNumber - 1) * pageSize wrap into a negative offset. It must stay
        // a success: an empty page whose metadata still describes the data.
        var page = await s.TimelineAsync(page: int.MaxValue);

        Assert.Equal(int.MaxValue, page.Page);
        Assert.Equal(4, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Count_only_pass_runs_no_fetch_queries()
    {
        // The count-only guards (take <= 0) in the four source helpers are
        // behaviorally invisible on InMemory — a LIMIT 0 fetch would return the
        // same empty rows — so the query shape is verified structurally instead:
        // a page beyond the last one must compile exactly the four COUNT
        // queries and nothing else (8 would mean the guards are missing).
        var s = new MedicalRecordScenario(countQueries: true);
        SeedMixedEvents(s);
        s.ActAsDoctor(s.DoctorA);

        var page = await s.TimelineAsync(page: int.MaxValue);

        Assert.Equal(int.MaxValue, page.Page);
        Assert.Equal(4, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
        Assert.Empty(page.Items);
        Assert.Equal(4, s.QueryCounting!.QueryCount);
    }

    [Fact]
    public async Task Secretary_is_rejected_before_any_timeline_data_is_read()
    {
        var s = new MedicalRecordScenario();
        SeedMixedEvents(s);
        s.ActAsSecretary();

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => s.TimelineAsync());

        Assert.Equal(ErrorCodes.MedicalRecordAccessDenied, exception.Code);
    }

    [Fact]
    public async Task Another_doctors_patient_is_not_found_for_the_timeline()
    {
        var s = new MedicalRecordScenario();
        var otherVisit = s.SeedDbVisit(s.PatientB, DateTimeOffset.UtcNow);
        s.SeedDbDiagnosis(otherVisit, "Migraine", DateOnly.FromDateTime(DateTime.UtcNow));
        s.ActAsDoctor(s.DoctorA);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => s.TimelineAsync(s.PatientB));

        Assert.Equal(ErrorCodes.PatientNotFound, exception.Code);
    }

    [Fact]
    public async Task Unknown_patient_is_not_found_for_the_timeline()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => s.TimelineAsync(s.UnownedPatient));

        Assert.Equal(ErrorCodes.PatientNotFound, exception.Code);
    }

    [Fact]
    public async Task Missing_user_id_is_an_identity_failure_for_the_timeline()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        s.User.UserId = null;

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => s.TimelineAsync());

        Assert.Equal(ErrorCodes.IdentityInvalid, exception.Code);
    }

    [Fact]
    public async Task Timeline_with_no_events_returns_an_empty_page()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);

        var page = await s.TimelineAsync();

        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task From_after_To_is_rejected_by_the_shared_validator()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);

        await Assert.ThrowsAsync<RequestValidationException>(
            () => s.TimelineAsync(
                from: DateTimeOffset.UtcNow,
                to: DateTimeOffset.UtcNow.AddDays(-1)));
    }

    [Fact]
    public async Task Unknown_event_type_is_rejected_by_the_validator()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);

        await Assert.ThrowsAsync<RequestValidationException>(
            () => s.TimelineAsync(eventType: "Bogus"));
    }

    [Fact]
    public async Task Out_of_range_paging_is_rejected_by_the_shared_validator()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);

        await Assert.ThrowsAsync<RequestValidationException>(
            () => s.TimelineAsync(page: 0));
        await Assert.ThrowsAsync<RequestValidationException>(
            () => s.TimelineAsync(pageSize: PagingDefaults.MaxPageSize + 1));
    }
}
