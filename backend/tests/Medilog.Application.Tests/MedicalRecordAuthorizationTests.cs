using Medilog.Application.Common;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Features.MedicalRecords.Requests;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;

namespace Medilog.Application.Tests;

/// <summary>
/// The role/identity/ownership rules of the medical-record boundary (task file
/// §4.2). Derived from BRULE-01/03/04/10 and AGENTS §5, not from the code.
/// </summary>
public class MedicalRecordAuthorizationTests
{
    // A secretary is rejected before validation and before any store call, even
    // when the request is also invalid: F3 has no secretary-assignment concept,
    // the medical boundary is unconditional (BRULE-10, AGENTS §5).
    [Fact]
    public async Task Secretary_is_rejected_for_profile_before_any_validation_or_data_access()
    {
        var s = new MedicalRecordScenario();

        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => s.Service.GetMedicalProfileAsync(new GetMedicalProfileRequest(Guid.Empty), CancellationToken.None));

        Assert.Equal(ErrorCodes.MedicalRecordAccessDenied, exception.Code);
        Assert.Equal(0, s.Store.CallCount);
    }

    [Fact]
    public async Task Secretary_is_rejected_for_history_before_any_validation_or_data_access()
    {
        var s = new MedicalRecordScenario();

        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => s.Service.GetMedicalHistoryAsync(new GetMedicalHistoryRequest(s.PatientA.Id, 0, 0), CancellationToken.None));

        Assert.Equal(ErrorCodes.MedicalRecordAccessDenied, exception.Code);
        Assert.Equal(0, s.Store.CallCount);
    }

    [Fact]
    public async Task Undefined_role_is_rejected_as_non_doctor()
    {
        var s = new MedicalRecordScenario();
        s.User.Role = (UserRole)99;

        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => s.ProfileAsync());

        Assert.Equal(ErrorCodes.MedicalRecordAccessDenied, exception.Code);
    }

    [Fact]
    public async Task Missing_user_id_is_an_identity_failure()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        s.User.UserId = null; // unauthenticated, per the nullable ICurrentUser contract

        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => s.ProfileAsync(s.PatientA));

        Assert.Equal(ErrorCodes.IdentityInvalid, exception.Code);
    }

    [Fact]
    public async Task Unknown_doctor_is_an_identity_failure()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);
        s.User.UserId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => s.ProfileAsync(s.PatientA));

        Assert.Equal(ErrorCodes.IdentityInvalid, exception.Code);
    }

    [Fact]
    public async Task Inactive_doctor_cannot_read_even_their_own_patients()
    {
        var s = new MedicalRecordScenario();
        s.DoctorA.AccountStatus = AccountStatus.Inactive;
        s.ActAsDoctor(s.DoctorA);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => s.ProfileAsync(s.PatientA));

        Assert.Equal(ErrorCodes.IdentityInvalid, exception.Code);
    }

    [Fact]
    public async Task The_owning_doctor_can_read_their_patients()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);

        var profile = await s.ProfileAsync(s.PatientA);
        var history = await s.HistoryAsync(s.PatientA);

        Assert.Equal(s.PatientA.Id, profile.PatientId);
        Assert.Equal(0, history.TotalCount);
    }

    [Fact]
    public async Task Another_doctors_patient_is_reported_as_not_found()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => s.ProfileAsync(s.PatientB));

        Assert.Equal(ErrorCodes.PatientNotFound, exception.Code);
    }

    [Fact]
    public async Task Patient_with_no_owning_doctor_is_reported_as_not_found()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => s.ProfileAsync(s.UnownedPatient));

        Assert.Equal(ErrorCodes.PatientNotFound, exception.Code);
    }

    [Fact]
    public async Task Unknown_patient_is_reported_as_not_found()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => s.ProfileAsync(new Patient { FullName = "Ghost", DateOfBirth = new DateOnly(2000, 1, 1), Gender = Gender.Male }));

        Assert.Equal(ErrorCodes.PatientNotFound, exception.Code);
    }

    // Out-of-scope patients must not be distinguishable from missing ones
    // (task file D-05), so a caller cannot probe which patient ids exist.
    [Fact]
    public async Task The_three_not_found_cases_are_indistinguishable()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);

        var otherDoctors = await Capture.NotFound(() => s.ProfileAsync(s.PatientB));
        var unowned = await Capture.NotFound(() => s.ProfileAsync(s.UnownedPatient));
        var unknown = await Capture.NotFound(() =>
            s.ProfileAsync(new Patient { FullName = "Ghost", DateOfBirth = new DateOnly(2000, 1, 1), Gender = Gender.Male }));

        Assert.Equal(otherDoctors.Message, unowned.Message);
        Assert.Equal(otherDoctors.Message, unknown.Message);
        Assert.Equal(otherDoctors.Code, unowned.Code);
        Assert.Equal(otherDoctors.Code, unknown.Code);
    }

    [Fact]
    public async Task History_applies_the_same_role_and_ownership_rules()
    {
        var s = new MedicalRecordScenario();

        var forbidden = await Assert.ThrowsAsync<ForbiddenException>(
            () => s.HistoryAsync(s.PatientA));
        Assert.Equal(ErrorCodes.MedicalRecordAccessDenied, forbidden.Code);

        s.ActAsDoctor(s.DoctorA);
        s.User.UserId = null; // unauthenticated, per the nullable ICurrentUser contract
        var identity = await Assert.ThrowsAsync<ForbiddenException>(
            () => s.HistoryAsync(s.PatientA));
        Assert.Equal(ErrorCodes.IdentityInvalid, identity.Code);

        s.ActAsDoctor(s.DoctorB);
        var notFound = await Assert.ThrowsAsync<NotFoundException>(
            () => s.HistoryAsync(s.PatientA));
        Assert.Equal(ErrorCodes.PatientNotFound, notFound.Code);
    }
}

/// <summary>Captures the code and message of an expected not-found failure.</summary>
internal static class Capture
{
    public static async Task<(string Code, string Message)> NotFound(Func<Task> action)
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(action);
        return (exception.Code, exception.Message);
    }
}
