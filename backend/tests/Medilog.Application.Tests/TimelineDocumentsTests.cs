using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.MedicalRecords.Responses;
using Medilog.Application.Interfaces;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Enums;
using Xunit;

namespace Medilog.Application.Tests;

/// <summary>
/// Unit tests for the F3-C3b medical timeline (Q-12) and document metadata (Q-13)
/// use cases: the four-event composition with global ordering and tie-breakers,
/// the summary payloads (D-T1…D-T3), the never-exposed document location
/// (the read models simply have no such field), and the identical authorization
/// order of every F3 read use case.
/// </summary>
public sealed class TimelineDocumentsTests
{
    [Fact]
    public async Task Timeline_contains_all_four_event_types_of_the_patient()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        var visit = s.SeedVisit(s.PatientA, s.DoctorA, new DateTimeOffset(2030, 1, 10, 9, 0, 0, TimeSpan.Zero));
        s.SeedNote(s.PatientA, s.DoctorA, new DateTimeOffset(2030, 1, 11, 8, 0, 0, TimeSpan.Zero));
        s.SeedLabResult(s.PatientA, new DateOnly(2030, 1, 12));
        s.SeedDocument(s.PatientA, new DateTimeOffset(2030, 1, 13, 10, 0, 0, TimeSpan.Zero));

        var page = await s.TimelineAsync();

        Assert.Equal(4, page.TotalCount);
        Assert.Equal(
            [TimelineEventTypes.Document, TimelineEventTypes.LabResult, TimelineEventTypes.Note, TimelineEventTypes.Visit],
            page.Items.Select(@event => @event.EventType));
    }

    [Fact]
    public async Task Timeline_is_globally_newest_first_across_event_types()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        var oldestVisit = s.SeedVisit(s.PatientA, s.DoctorA, new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero));
        var newestVisit = s.SeedVisit(s.PatientA, s.DoctorA, new DateTimeOffset(2030, 1, 20, 9, 0, 0, TimeSpan.Zero));
        var middleNote = s.SeedNote(s.PatientA, s.DoctorA, new DateTimeOffset(2030, 1, 10, 8, 0, 0, TimeSpan.Zero));
        var middleLab = s.SeedLabResult(s.PatientA, new DateOnly(2030, 1, 15));
        var middleDocument = s.SeedDocument(s.PatientA, new DateTimeOffset(2030, 1, 5, 10, 0, 0, TimeSpan.Zero));

        var page = await s.TimelineAsync();

        var ids = page.Items.Select(@event => @event.Id).ToArray();
        Assert.Equal([newestVisit.Id, middleLab.Id, middleNote.Id, middleDocument.Id, oldestVisit.Id], ids);
    }

    [Fact]
    public async Task Equal_instants_are_broken_by_event_type_then_id()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        var sameInstant = new DateTimeOffset(2030, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var visit = s.SeedVisit(s.PatientA, s.DoctorA, sameInstant);
        var note = s.SeedNote(s.PatientA, s.DoctorA, sameInstant);

        var page = await s.TimelineAsync();

        // Ordinal: "note" < "visit", so the note precedes the visit at the same instant.
        var noteEvent = Assert.Single(page.Items, @event => @event.Id == note.Id);
        var visitEvent = Assert.Single(page.Items, @event => @event.Id == visit.Id);
        Assert.True(page.Items.ToList().IndexOf(noteEvent) < page.Items.ToList().IndexOf(visitEvent));
    }

    [Fact]
    public async Task Timeline_visit_events_carry_a_summary_with_child_counts()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        var visit = s.SeedVisit(s.PatientA, s.DoctorA, new DateTimeOffset(2030, 1, 10, 9, 0, 0, TimeSpan.Zero), "Chest pain");
        s.SeedNote(s.PatientA, s.DoctorA, new DateTimeOffset(2030, 1, 10, 10, 0, 0, TimeSpan.Zero), visit.Id);

        var page = await s.TimelineAsync();

        var visitEvent = Assert.Single(page.Items, @event => @event.Id == visit.Id);
        Assert.Equal(TimelineEventTypes.Visit, visitEvent.EventType);
        Assert.Equal(visit.Id, visitEvent.VisitId);
        Assert.NotNull(visitEvent.Visit);
        Assert.Null(visitEvent.Note);
        Assert.Null(visitEvent.LabResult);
        Assert.Null(visitEvent.Document);
        Assert.Equal("Doctor A", visitEvent.Visit.DoctorName);
        Assert.Equal("Chest pain", visitEvent.Visit.ChiefComplaint);
        Assert.Equal(1, visitEvent.Visit.NotesCount);
        Assert.Equal(0, visitEvent.Visit.DiagnosesCount);
        Assert.Equal(0, visitEvent.Visit.MedicationsCount);
        Assert.Equal(0, visitEvent.Visit.LabResultsCount);
    }

    [Fact]
    public async Task Timeline_note_events_carry_the_full_content_and_nullable_visit_id()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        var visit = s.SeedVisit(s.PatientA, s.DoctorA, new DateTimeOffset(2030, 1, 10, 9, 0, 0, TimeSpan.Zero));
        var linked = s.SeedNote(s.PatientA, s.DoctorA, new DateTimeOffset(2030, 1, 10, 10, 0, 0, TimeSpan.Zero), visit.Id);
        var unlinked = s.SeedNote(s.PatientA, s.DoctorA, new DateTimeOffset(2030, 1, 11, 10, 0, 0, TimeSpan.Zero));

        var page = await s.TimelineAsync();

        var linkedEvent = Assert.Single(page.Items, @event => @event.Id == linked.Id);
        Assert.Equal(TimelineEventTypes.Note, linkedEvent.EventType);
        Assert.Equal(visit.Id, linkedEvent.VisitId);
        Assert.Equal("Doctor A", linkedEvent.Note?.DoctorName);
        Assert.Equal("test-only note", linkedEvent.Note?.NoteContent);

        var unlinkedEvent = Assert.Single(page.Items, @event => @event.Id == unlinked.Id);
        Assert.Null(unlinkedEvent.VisitId);
    }

    [Fact]
    public async Task Timeline_lab_events_are_dated_at_midnight_utc_of_the_test_date()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        var labResult = s.SeedLabResult(s.PatientA, new DateOnly(2030, 1, 12));

        var page = await s.TimelineAsync();

        var labEvent = Assert.Single(page.Items, @event => @event.Id == labResult.Id);
        Assert.Equal(TimelineEventTypes.LabResult, labEvent.EventType);
        Assert.Equal(new DateTimeOffset(2030, 1, 12, 0, 0, 0, TimeSpan.Zero), labEvent.OccurredAtUtc);
        Assert.Equal("Hemoglobin", labEvent.LabResult?.TestName);
        Assert.Equal(13.5m, labEvent.LabResult?.Value);
        Assert.Equal("g/dL", labEvent.LabResult?.Unit);
    }

    [Fact]
    public async Task Timeline_document_events_carry_metadata_only()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        var document = s.SeedDocument(
            s.PatientA,
            new DateTimeOffset(2030, 1, 13, 10, 0, 0, TimeSpan.Zero),
            s.DoctorA,
            "mri-scan.pdf",
            MedicalDocumentType.Pdf,
            "MRI of the knee");

        var page = await s.TimelineAsync();

        var documentEvent = Assert.Single(page.Items, @event => @event.Id == document.Id);
        Assert.Equal(TimelineEventTypes.Document, documentEvent.EventType);
        Assert.Null(documentEvent.VisitId);
        Assert.Equal("mri-scan.pdf", documentEvent.Document?.FileName);
        Assert.Equal(MedicalDocumentType.Pdf, documentEvent.Document?.FileType);
        Assert.Equal("MRI of the knee", documentEvent.Document?.Description);
        Assert.Equal("Doctor A", documentEvent.Document?.UploadedByDoctorName);
    }

    [Fact]
    public async Task Documents_return_metadata_with_optional_uploader()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        var owned = s.SeedDocument(
            s.PatientA,
            new DateTimeOffset(2030, 1, 13, 10, 0, 0, TimeSpan.Zero),
            s.DoctorA,
            "report-one.pdf",
            MedicalDocumentType.Pdf,
            "First report");
        var anonymous = s.SeedDocument(
            s.PatientA,
            new DateTimeOffset(2030, 1, 14, 10, 0, 0, TimeSpan.Zero),
            uploadedBy: null,
            fileName: "report-two.png",
            type: MedicalDocumentType.Png);

        var page = await s.DocumentsAsync();

        Assert.Equal(2, page.TotalCount);
        // Newest upload first (D-T6 ordering; upload date desc, id tie-breaker).
        Assert.Equal([anonymous.Id, owned.Id], page.Items.Select(document => document.Id).ToArray());

        var anonymousResponse = Assert.Single(page.Items, document => document.Id == anonymous.Id);
        Assert.Equal("report-two.png", anonymousResponse.FileName);
        Assert.Equal(MedicalDocumentType.Png, anonymousResponse.Type);
        Assert.Null(anonymousResponse.UploadedByDoctorName); // D-T5
        Assert.Null(anonymousResponse.Description);

        var ownedResponse = Assert.Single(page.Items, document => document.Id == owned.Id);
        Assert.Equal("Doctor A", ownedResponse.UploadedByDoctorName);
        Assert.Equal("First report", ownedResponse.Description);
    }

    [Fact]
    public async Task Documents_are_paged_with_the_shared_envelope()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        for (var index = 0; index < 3; index++)
        {
            s.SeedDocument(s.PatientA, new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(index));
        }

        var firstPage = await s.DocumentsAsync(pageSize: 2);
        var secondPage = await s.DocumentsAsync(page: 2, pageSize: 2);
        var beyondLast = await s.DocumentsAsync(page: 3, pageSize: 2);

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Single(secondPage.Items);
        Assert.Empty(beyondLast.Items);
        Assert.Equal(3, beyondLast.TotalCount);
        Assert.Equal(2, beyondLast.TotalPages);
    }

    [Fact]
    public async Task Timeline_is_paged_with_the_shared_envelope()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        s.SeedVisit(s.PatientA, s.DoctorA, new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero));
        s.SeedNote(s.PatientA, s.DoctorA, new DateTimeOffset(2030, 1, 2, 9, 0, 0, TimeSpan.Zero));
        s.SeedLabResult(s.PatientA, new DateOnly(2030, 1, 3));

        var firstPage = await s.TimelineAsync(pageSize: 2);
        var secondPage = await s.TimelineAsync(page: 2, pageSize: 2);
        var beyondLast = await s.TimelineAsync(page: 3, pageSize: 2);

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Single(firstPage.Items, @event => @event.EventType == TimelineEventTypes.LabResult);
        // The oldest event (the visit) is on the second page.
        Assert.Single(secondPage.Items, @event => @event.EventType == TimelineEventTypes.Visit);
        Assert.Empty(beyondLast.Items);
        Assert.Equal(3, beyondLast.TotalCount);
        Assert.Equal(2, beyondLast.TotalPages);
    }

    [Fact]
    public async Task Timeline_never_contains_another_patients_events()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        s.SeedVisit(s.PatientA, s.DoctorA, new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero));
        s.SeedVisit(s.PatientB, s.DoctorB, new DateTimeOffset(2030, 1, 2, 9, 0, 0, TimeSpan.Zero));
        s.SeedDocument(s.PatientB, new DateTimeOffset(2030, 1, 3, 9, 0, 0, TimeSpan.Zero));

        var page = await s.TimelineAsync();

        Assert.Equal(1, page.TotalCount);
        Assert.Single(page.Items);
        Assert.Equal(TimelineEventTypes.Visit, Assert.Single(page.Items).EventType);
    }

    [Fact]
    public async Task Secretary_is_rejected_with_zero_store_calls_for_timeline()
    {
        var s = new MedicalRecordScenario();
        s.ActAsSecretary();
        s.SeedVisit(s.PatientA, s.DoctorA, DateTimeOffset.UtcNow);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => s.TimelineAsync());

        Assert.Equal(ErrorCodes.MedicalRecordAccessDenied, exception.Code);
        Assert.Equal(0, s.Store.CallCount);
    }

    [Fact]
    public async Task Secretary_is_rejected_with_zero_store_calls_for_documents()
    {
        var s = new MedicalRecordScenario();
        s.ActAsSecretary();
        s.SeedDocument(s.PatientA, DateTimeOffset.UtcNow);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => s.DocumentsAsync());

        Assert.Equal(ErrorCodes.MedicalRecordAccessDenied, exception.Code);
        Assert.Equal(0, s.Store.CallCount);
    }

    [Fact]
    public async Task Another_doctors_patient_is_not_found_for_timeline()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        s.SeedVisit(s.PatientB, s.DoctorB, DateTimeOffset.UtcNow);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => s.TimelineAsync(s.PatientB));

        Assert.Equal(ErrorCodes.PatientNotFound, exception.Code);
    }

    [Fact]
    public async Task Another_doctors_patient_is_not_found_for_documents()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        s.SeedDocument(s.PatientB, DateTimeOffset.UtcNow);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => s.DocumentsAsync(s.PatientB));

        Assert.Equal(ErrorCodes.PatientNotFound, exception.Code);
    }

    [Fact]
    public async Task Missing_user_id_is_an_identity_failure_for_timeline()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        s.User.UserId = null;

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => s.TimelineAsync());

        Assert.Equal(ErrorCodes.IdentityInvalid, exception.Code);
    }

    [Fact]
    public async Task Paging_rules_come_from_the_shared_validator_for_timeline()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);

        await Assert.ThrowsAsync<RequestValidationException>(
            () => s.TimelineAsync(pageSize: PagingDefaults.MaxPageSize + 1));
    }

    [Fact]
    public async Task Paging_rules_come_from_the_shared_validator_for_documents()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);

        await Assert.ThrowsAsync<RequestValidationException>(
            () => s.DocumentsAsync(page: 0));
    }
}
