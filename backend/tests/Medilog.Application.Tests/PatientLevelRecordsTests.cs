using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.MedicalRecords.Responses;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Enums;
using Xunit;

namespace Medilog.Application.Tests;

/// <summary>
/// Unit tests for the F3-C3a patient-level notes and laboratory-result use cases:
/// the C-2 all-records contract, the C-3 nullable visitId, the authorization
/// order (identical to every F3 read use case), and the validator boundary.
/// Store-level ordering is mirrored by the fake and proven against the real
/// store by the WebAPI integration tests.
/// </summary>
public sealed class PatientLevelRecordsTests
{
    [Fact]
    public async Task Notes_return_every_patient_record_linked_and_unlinked()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        var linkedVisit = s.SeedVisit(s.PatientA, s.DoctorA, DateTimeOffset.UtcNow.AddDays(-1));
        var linked = s.SeedNote(s.PatientA, s.DoctorA, DateTimeOffset.UtcNow.AddDays(-1), linkedVisit.Id);
        var unlinked = s.SeedNote(s.PatientA, s.DoctorA, DateTimeOffset.UtcNow.AddDays(-2));

        var page = await s.NotesAsync();

        Assert.Equal(2, page.TotalCount);
        var response = Assert.Single(page.Items, note => note.Id == linked.Id);
        Assert.Equal(linkedVisit.Id, response.VisitId);
        Assert.Equal("Doctor A", response.DoctorName);
        Assert.Contains(page.Items, note => note.Id == unlinked.Id && note.VisitId is null);
    }

    [Fact]
    public async Task Notes_are_newest_first()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        var oldest = s.SeedNote(s.PatientA, s.DoctorA, DateTimeOffset.UtcNow.AddDays(-3));
        var newest = s.SeedNote(s.PatientA, s.DoctorA, DateTimeOffset.UtcNow.AddDays(-1));
        var middle = s.SeedNote(s.PatientA, s.DoctorA, DateTimeOffset.UtcNow.AddDays(-2));

        var page = await s.NotesAsync();

        var ids = page.Items.Select(note => note.Id).ToArray();
        Assert.Equal([newest.Id, middle.Id, oldest.Id], ids);
    }

    [Fact]
    public async Task Lab_results_return_every_patient_record_with_nullable_visit_id()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        var linkedVisit = s.SeedVisit(s.PatientA, s.DoctorA, DateTimeOffset.UtcNow.AddDays(-1));
        var linked = s.SeedLabResult(s.PatientA, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), linkedVisit.Id);
        var unlinked = s.SeedLabResult(s.PatientA, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3));

        var page = await s.LabResultsAsync();

        Assert.Equal(2, page.TotalCount);
        var linkedResponse = Assert.Single(page.Items, labResult => labResult.Id == linked.Id);
        Assert.Equal(linkedVisit.Id, linkedResponse.VisitId);
        Assert.Contains(page.Items, labResult => labResult.Id == unlinked.Id && labResult.VisitId is null);
    }

    [Fact]
    public async Task Lab_results_are_newest_test_first()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        var oldest = s.SeedLabResult(s.PatientA, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3));
        var newest = s.SeedLabResult(s.PatientA, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1));
        var middle = s.SeedLabResult(s.PatientA, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2));

        var page = await s.LabResultsAsync();

        var ids = page.Items.Select(labResult => labResult.Id).ToArray();
        Assert.Equal([newest.Id, middle.Id, oldest.Id], ids);
    }

    [Fact]
    public async Task Secretary_is_rejected_with_zero_store_calls_for_notes()
    {
        var s = new MedicalRecordScenario();
        s.ActAsSecretary();
        s.SeedNote(s.PatientA, s.DoctorA, DateTimeOffset.UtcNow);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => s.NotesAsync());

        Assert.Equal(ErrorCodes.MedicalRecordAccessDenied, exception.Code);
        Assert.Equal(0, s.Store.CallCount);
    }

    [Fact]
    public async Task Secretary_is_rejected_with_zero_store_calls_for_lab_results()
    {
        var s = new MedicalRecordScenario();
        s.ActAsSecretary();
        s.SeedLabResult(s.PatientA, DateOnly.FromDateTime(DateTime.UtcNow));

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => s.LabResultsAsync());

        Assert.Equal(ErrorCodes.MedicalRecordAccessDenied, exception.Code);
        Assert.Equal(0, s.Store.CallCount);
    }

    [Fact]
    public async Task Another_doctors_patient_is_not_found_for_notes()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        s.SeedNote(s.PatientB, s.DoctorB, DateTimeOffset.UtcNow);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => s.NotesAsync(s.PatientB));

        Assert.Equal(ErrorCodes.PatientNotFound, exception.Code);
    }

    [Fact]
    public async Task Another_doctors_patient_is_not_found_for_lab_results()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        s.SeedLabResult(s.PatientB, DateOnly.FromDateTime(DateTime.UtcNow));

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => s.LabResultsAsync(s.PatientB));

        Assert.Equal(ErrorCodes.PatientNotFound, exception.Code);
    }

    [Fact]
    public async Task Missing_user_id_is_an_identity_failure_for_notes()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        s.User.UserId = null; // unauthenticated, per the nullable ICurrentUser contract

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => s.NotesAsync());

        Assert.Equal(ErrorCodes.IdentityInvalid, exception.Code);
    }

    [Fact]
    public async Task Paging_rules_come_from_the_shared_validator_for_notes()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);

        await Assert.ThrowsAsync<RequestValidationException>(
            () => s.NotesAsync(pageSize: PagingDefaults.MaxPageSize + 1));
    }

    [Fact]
    public async Task Paging_rules_come_from_the_shared_validator_for_lab_results()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);

        await Assert.ThrowsAsync<RequestValidationException>(
            () => s.LabResultsAsync(page: 0));
    }
}
