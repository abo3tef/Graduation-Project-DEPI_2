using Medilog.Application.Common.Exceptions;
using Medilog.Application.Features.Appointments.Lookups;
using Medilog.Application.Features.Appointments.Requests;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Common;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using static Medilog.Application.Tests.Support.Scenario;

namespace Medilog.Application.Tests;

public class LookupAndErrorCodeTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    // ---------------------------------------------------------------- doctor lookups

    [Fact]
    public async Task Secretary_sees_only_assigned_active_doctors_ordered_by_name()
    {
        var s = new Scenario();
        s.DoctorA.FullName = "Zeinab";
        var second = new Doctor { FullName = "Amr", Specialization = "Dental", AccountStatus = AccountStatus.Active };
        var inactive = new Doctor { FullName = "Inactive", AccountStatus = AccountStatus.Inactive };
        s.Store.Doctors.AddRange([second, inactive]);
        s.Store.Assignments[s.Secretary.Id].UnionWith([second.Id, inactive.Id]);

        var result = await s.Service.GetDoctorLookupsAsync(Ct);

        // Doctor B is not assigned; the inactive doctor cannot be booked.
        Assert.Equal(["Amr", "Zeinab"], result.Select(d => d.FullName));
        Assert.Equal("Dental", result[0].Specialization);
    }

    [Fact]
    public async Task Doctor_sees_only_themself()
    {
        var s = new Scenario();
        s.ActAsDoctor(s.DoctorA);

        var result = await s.Service.GetDoctorLookupsAsync(Ct);

        Assert.Equal([s.DoctorA.Id], result.Select(d => d.Id));
    }

    [Fact]
    public async Task Secretary_without_assignments_gets_an_empty_list_without_querying_doctors()
    {
        var s = new Scenario();
        s.Store.Assignments[s.Secretary.Id].Clear();

        var result = await s.Service.GetDoctorLookupsAsync(Ct);

        Assert.Empty(result);
        Assert.Empty(s.Store.LookupCalls);
    }

    [Fact]
    public async Task Doctor_lookups_reject_an_unknown_or_inactive_caller()
    {
        var s = new Scenario();
        s.User.UserId = Guid.NewGuid();
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.GetDoctorLookupsAsync(Ct));

        s.ActAsSecretary();
        s.Secretary.AccountStatus = AccountStatus.Inactive;
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.GetDoctorLookupsAsync(Ct));
        Assert.Equal(ErrorCodes.IdentityInvalid, ex.Code);
    }

    // ---------------------------------------------------------------- patient lookups

    private static Patient NewPatient(Doctor doctor, string name, string? phone) =>
        new() { FullName = name, Phone = phone, DoctorId = doctor.Id };

    [Fact]
    public async Task Patient_search_returns_only_that_doctors_patients_matching_name_or_phone()
    {
        var s = new Scenario();
        s.Store.Patients.AddRange([
            NewPatient(s.DoctorA, "Mona Said", "01012345678"),
            NewPatient(s.DoctorA, "Omar Hassan", "01199999999"),
            NewPatient(s.DoctorB, "Mona Other", "01012340000")]);

        var byName = await s.Service.SearchPatientsAsync(new PatientLookupQuery(s.DoctorA.Id, "mona"), Ct);
        var byPhone = await s.Service.SearchPatientsAsync(new PatientLookupQuery(s.DoctorA.Id, "0119"), Ct);

        Assert.Equal(["Mona Said"], byName.Select(p => p.FullName));
        Assert.Equal(["Omar Hassan"], byPhone.Select(p => p.FullName));
    }

    [Fact]
    public async Task Patient_search_trims_the_text_limits_the_rows_and_orders_by_name()
    {
        var s = new Scenario();
        for (var i = 0; i < 15; i++)
        {
            s.Store.Patients.Add(NewPatient(s.DoctorA, $"Patient {(char)('Z' - i)}", null));
        }

        var result = await s.Service.SearchPatientsAsync(new PatientLookupQuery(s.DoctorA.Id, "  patient  ", Limit: 5), Ct);

        Assert.Equal(5, result.Count);
        Assert.Equal(result.Select(p => p.FullName).Order().ToArray(), result.Select(p => p.FullName).ToArray());
        Assert.Equal("patient", s.Store.LookupCalls.Last().Search);
        Assert.Equal(5, s.Store.LookupCalls.Last().Limit);
    }

    [Fact]
    public async Task Patient_search_without_text_returns_the_first_page_of_the_doctors_patients()
    {
        var s = new Scenario();

        var result = await s.Service.SearchPatientsAsync(new PatientLookupQuery(s.DoctorA.Id), Ct);

        Assert.Equal([s.PatientA.Id], result.Select(p => p.Id));
        Assert.Null(s.Store.LookupCalls.Last().Search);
    }

    [Fact]
    public async Task Patient_search_for_an_unassigned_doctor_looks_like_a_missing_doctor()
    {
        var s = new Scenario();

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => s.Service.SearchPatientsAsync(new PatientLookupQuery(s.DoctorB.Id, "Patient"), Ct));

        Assert.Equal(ErrorCodes.DoctorNotFound, ex.Code);
        Assert.Empty(s.Store.LookupCalls);
    }

    [Fact]
    public async Task A_doctor_can_search_only_their_own_patients()
    {
        var s = new Scenario();
        s.ActAsDoctor(s.DoctorA);

        var own = await s.Service.SearchPatientsAsync(new PatientLookupQuery(s.DoctorA.Id), Ct);
        await Assert.ThrowsAsync<NotFoundException>(
            () => s.Service.SearchPatientsAsync(new PatientLookupQuery(s.DoctorB.Id), Ct));

        Assert.Single(own);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    public async Task Patient_search_rejects_an_invalid_limit(int limit)
    {
        var s = new Scenario();

        await Assert.ThrowsAsync<RequestValidationException>(
            () => s.Service.SearchPatientsAsync(new PatientLookupQuery(s.DoctorA.Id, null, limit), Ct));
    }

    [Fact]
    public async Task Patient_search_requires_a_doctor_and_limits_the_text_length()
    {
        var s = new Scenario();

        await Assert.ThrowsAsync<RequestValidationException>(
            () => s.Service.SearchPatientsAsync(new PatientLookupQuery(Guid.Empty), Ct));
        await Assert.ThrowsAsync<RequestValidationException>(
            () => s.Service.SearchPatientsAsync(new PatientLookupQuery(s.DoctorA.Id, new string('a', 101)), Ct));
    }

    [Fact]
    public void Lookup_responses_expose_only_minimal_administrative_fields()
    {
        Assert.Equal(["FullName", "Id", "Phone"], typeof(PatientLookupResponse).GetProperties().Select(p => p.Name).Order());
        Assert.Equal(["FullName", "Id", "Specialization"], typeof(DoctorLookupResponse).GetProperties().Select(p => p.Name).Order());
    }

    // ---------------------------------------------------------------- machine-readable error codes

    [Fact]
    public async Task Overlap_conflict_carries_its_code()
    {
        var s = new Scenario();
        s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");

        var ex = await Assert.ThrowsAsync<ConflictException>(() => s.BookAsync("10:15", "10:45"));

        Assert.Equal(ErrorCodes.AppointmentOverlap, ex.Code);
    }

    [Fact]
    public async Task Not_found_forbidden_and_unavailable_failures_carry_their_codes()
    {
        var s = new Scenario();

        var unassigned = await Assert.ThrowsAsync<NotFoundException>(() => s.BookAsync("10:00", "10:30", s.DoctorB, s.PatientB));
        var otherPatient = await Assert.ThrowsAsync<NotFoundException>(() => s.BookAsync("10:00", "10:30", s.DoctorA, s.PatientB));
        var missing = await Assert.ThrowsAsync<NotFoundException>(() => s.Service.GetAsync(Guid.NewGuid(), Ct));
        s.DoctorA.AccountStatus = AccountStatus.Inactive;
        var unavailable = await Assert.ThrowsAsync<ConflictException>(() => s.BookAsync("10:00", "10:30"));
        var appointment = s.Seed(s.DoctorA, s.PatientA, "12:00", "12:30", AppointmentStatus.Confirmed);
        var notDoctor = await Assert.ThrowsAsync<ForbiddenException>(
            () => s.Service.UpdateStatusAsync(appointment.Id, new UpdateAppointmentStatusRequest(AppointmentStatus.Completed), Ct));

        Assert.Equal(ErrorCodes.DoctorNotFound, unassigned.Code);
        Assert.Equal(ErrorCodes.PatientNotFound, otherPatient.Code);
        Assert.Equal(ErrorCodes.AppointmentNotFound, missing.Code);
        Assert.Equal(ErrorCodes.DoctorUnavailable, unavailable.Code);
        Assert.Equal(ErrorCodes.CompleteRequiresDoctor, notDoctor.Code);
    }

    [Fact]
    public async Task Validation_failures_carry_a_code_and_per_field_validator_codes()
    {
        var s = new Scenario();

        var ex = await Assert.ThrowsAsync<RequestValidationException>(() => s.BookAsync("10:00", null));

        Assert.Equal(ErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("NotNullValidator", ex.FieldErrorCodes["EndTime"]);
    }

    [Fact]
    public async Task A_time_skipped_by_daylight_saving_carries_its_code()
    {
        var s = new Scenario();
        s.Clock.NonExistentTimes.Add(Day.ToDateTime(T("10:00")));

        var ex = await Assert.ThrowsAsync<RequestValidationException>(() => s.BookAsync("10:00", "10:30"));

        Assert.Contains(ErrorCodes.TimeNotInTimeZone, ex.FieldErrorCodes["StartTime"]);
    }

    [Fact]
    public async Task Domain_rule_violations_carry_stable_domain_codes()
    {
        var s = new Scenario();
        var past = await Assert.ThrowsAsync<DomainException>(() => s.BookAsync("10:00", "10:30", date: Day.AddDays(-5)));

        var cancelled = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30", AppointmentStatus.Cancelled);
        var notOpen = await Assert.ThrowsAsync<DomainException>(
            () => s.Service.CancelAsync(cancelled.Id, new CancelAppointmentRequest("again"), Ct));

        var completed = s.Seed(s.DoctorA, s.PatientA, "11:00", "11:30", AppointmentStatus.Completed);
        var transition = await Assert.ThrowsAsync<DomainException>(
            () => s.Service.UpdateStatusAsync(completed.Id, new UpdateAppointmentStatusRequest(AppointmentStatus.Confirmed), Ct));

        Assert.Equal(DomainErrorCodes.InThePast, past.Code);
        Assert.Equal(DomainErrorCodes.NotOpen, notOpen.Code);
        Assert.Equal(DomainErrorCodes.InvalidStatusTransition, transition.Code);
    }

    [Fact]
    public void Direct_domain_failures_carry_their_codes()
    {
        var now = new DateTime(2030, 1, 1, 8, 0, 0);
        Guid id() => Guid.NewGuid();

        Assert.Equal(DomainErrorCodes.EndTimeRequired,
            Assert.Throws<DomainException>(() => Appointment.Create(id(), id(), id(), Day, T("10:00"), null, null, now)).Code);
        Assert.Equal(DomainErrorCodes.InvalidInterval,
            Assert.Throws<DomainException>(() => Appointment.Create(id(), id(), id(), Day, T("10:00"), T("10:00"), null, now)).Code);
        Assert.Equal(DomainErrorCodes.MissingReference,
            Assert.Throws<DomainException>(() => Appointment.Create(Guid.Empty, id(), id(), Day, T("10:00"), T("10:30"), null, now)).Code);

        var open = Appointment.Create(id(), id(), id(), Day, T("10:00"), T("10:30"), null, now);
        Assert.Equal(DomainErrorCodes.CancellationReasonRequired, Assert.Throws<DomainException>(() => open.Cancel(" ")).Code);
        Assert.Equal(DomainErrorCodes.CancellationReasonTooLong, Assert.Throws<DomainException>(() => open.Cancel(new string('a', 501))).Code);
        Assert.Equal(DomainErrorCodes.UseCancelOperation,
            Assert.Throws<DomainException>(() => open.UpdateStatus(AppointmentStatus.Cancelled)).Code);
    }

    [Fact]
    public void Error_codes_are_unique_stable_dotted_identifiers()
    {
        var codes = typeof(ErrorCodes).GetFields().Select(f => (string)f.GetRawConstantValue()!)
            .Concat(typeof(DomainErrorCodes).GetFields().Select(f => (string)f.GetRawConstantValue()!))
            .ToArray();

        Assert.Equal(codes.Length, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches("^[a-z]+(\\.[a-z_]+)+$", code));
    }
}
