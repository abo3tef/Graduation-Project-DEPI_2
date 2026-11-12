using Medilog.Application.Common;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Features.Appointments.Requests;
using Medilog.Application.Features.Appointments.Responses;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Enums;
using static Medilog.Application.Tests.Support.Scenario;

namespace Medilog.Application.Tests;

public class ScopeAndRoleTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Secretary_cannot_book_for_an_unassigned_doctor()
    {
        var s = new Scenario(); // secretary is assigned to doctor A only

        await Assert.ThrowsAsync<NotFoundException>(() => s.BookAsync("10:00", "10:30", s.DoctorB, s.PatientB));
        Assert.Empty(s.Store.Appointments);
    }

    [Fact]
    public async Task Secretary_cannot_get_reschedule_cancel_or_update_an_unassigned_doctors_appointment()
    {
        var s = new Scenario();
        var other = s.Seed(s.DoctorB, s.PatientB, "10:00", "10:30");
        var later = new RescheduleAppointmentRequest(Day.AddDays(1), T("11:00"), T("11:30"));

        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.GetAsync(other.Id, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.RescheduleAsync(other.Id, later, Ct));
        await Assert.ThrowsAsync<NotFoundException>(
            () => s.Service.CancelAsync(other.Id, new CancelAppointmentRequest("reason"), Ct));
        await Assert.ThrowsAsync<NotFoundException>(
            () => s.Service.UpdateStatusAsync(other.Id, new UpdateAppointmentStatusRequest(AppointmentStatus.Confirmed), Ct));

        Assert.Equal(AppointmentStatus.Scheduled, other.Status);
        Assert.Empty(s.Store.Transactions); // never reached the lock
    }

    [Fact]
    public async Task Missing_and_out_of_scope_appointments_look_the_same()
    {
        var s = new Scenario();
        var other = s.Seed(s.DoctorB, s.PatientB, "10:00", "10:30");

        var outOfScope = await Assert.ThrowsAsync<NotFoundException>(() => s.Service.GetAsync(other.Id, Ct));
        var missing = await Assert.ThrowsAsync<NotFoundException>(() => s.Service.GetAsync(Guid.NewGuid(), Ct));

        Assert.Equal(missing.Message, outOfScope.Message);
    }

    [Fact]
    public async Task Assigned_secretary_can_get_an_appointment()
    {
        var s = new Scenario();
        var own = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");

        var result = await s.Service.GetAsync(own.Id, Ct);

        Assert.Equal(own.Id, result.Id);
        Assert.Equal("Patient A", result.PatientName);
    }

    [Fact]
    public async Task Doctor_cannot_access_another_doctors_appointment()
    {
        var s = new Scenario();
        var other = s.Seed(s.DoctorB, s.PatientB, "10:00", "10:30");
        s.ActAsDoctor(s.DoctorA);

        await Assert.ThrowsAsync<NotFoundException>(() => s.Service.GetAsync(other.Id, Ct));
        await Assert.ThrowsAsync<NotFoundException>(
            () => s.Service.CancelAsync(other.Id, new CancelAppointmentRequest("reason"), Ct));
    }

    [Fact]
    public async Task Doctor_cannot_book_for_another_doctor()
    {
        var s = new Scenario();
        s.ActAsDoctor(s.DoctorA);

        await Assert.ThrowsAsync<NotFoundException>(() => s.BookAsync("10:00", "10:30", s.DoctorB, s.PatientB));
    }

    [Fact]
    public async Task Doctor_booking_records_the_doctor_as_creator()
    {
        var s = new Scenario();
        s.ActAsDoctor(s.DoctorA);

        var result = await s.BookAsync("10:00", "10:30");

        Assert.Equal(s.DoctorA.Id, result.CreatedBy);
    }

    [Fact]
    public async Task Inactive_unknown_or_invalid_actors_are_forbidden()
    {
        var s = new Scenario();

        s.Secretary.AccountStatus = AccountStatus.Inactive;
        await Assert.ThrowsAsync<ForbiddenException>(() => s.BookAsync("10:00", "10:30"));

        s.User.UserId = Guid.NewGuid(); // not a known secretary
        await Assert.ThrowsAsync<ForbiddenException>(() => s.BookAsync("10:00", "10:30"));

        s.User.Role = (UserRole)99;
        await Assert.ThrowsAsync<ForbiddenException>(() => s.BookAsync("10:00", "10:30"));

        s.User.UserId = Guid.Empty;
        s.User.Role = UserRole.Doctor;
        await Assert.ThrowsAsync<ForbiddenException>(() => s.BookAsync("10:00", "10:30"));
    }

    [Fact]
    public async Task Only_a_doctor_can_mark_an_appointment_completed()
    {
        var s = new Scenario();
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30", AppointmentStatus.Confirmed);
        var complete = new UpdateAppointmentStatusRequest(AppointmentStatus.Completed);

        await Assert.ThrowsAsync<ForbiddenException>(() => s.Service.UpdateStatusAsync(appointment.Id, complete, Ct));
        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
        Assert.False(s.Store.Transactions.Single().Committed);

        s.ActAsDoctor(s.DoctorA);
        var result = await s.Service.UpdateStatusAsync(appointment.Id, complete, Ct);

        Assert.Equal(AppointmentStatus.Completed, result.Status);
    }

    [Theory]
    [InlineData(AppointmentStatus.Scheduled, AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.NoShow)]
    public async Task Assigned_secretary_can_confirm_or_mark_no_show(AppointmentStatus from, AppointmentStatus to)
    {
        var s = new Scenario();
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30", from);

        var result = await s.Service.UpdateStatusAsync(appointment.Id, new UpdateAppointmentStatusRequest(to), Ct);

        Assert.Equal(to, result.Status);
    }

    [Fact]
    public async Task Secretary_list_is_limited_to_assigned_doctors()
    {
        var s = new Scenario();
        s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");
        s.Seed(s.DoctorB, s.PatientB, "11:00", "11:30");

        var result = await s.Service.ListAsync(new ListAppointmentsQuery(), Ct);

        Assert.Equal(1, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(s.DoctorA.Id, item.DoctorId));
        Assert.Equal([s.DoctorA.Id], s.Store.LastCriteria!.DoctorIds);
    }

    [Fact]
    public async Task A_doctor_filter_for_an_unassigned_doctor_cannot_widen_the_secretary_scope()
    {
        var s = new Scenario();
        s.Seed(s.DoctorB, s.PatientB, "11:00", "11:30");

        var result = await s.Service.ListAsync(new ListAppointmentsQuery(DoctorId: s.DoctorB.Id), Ct);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Doctor_list_is_limited_to_the_doctor_whatever_filter_is_sent()
    {
        var s = new Scenario();
        s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");
        s.Seed(s.DoctorB, s.PatientB, "11:00", "11:30");
        s.ActAsDoctor(s.DoctorA);

        var all = await s.Service.ListAsync(new ListAppointmentsQuery(), Ct);
        var forged = await s.Service.ListAsync(new ListAppointmentsQuery(DoctorId: s.DoctorB.Id), Ct);

        Assert.Single(all.Items);
        Assert.Empty(forged.Items);
    }

    [Fact]
    public async Task Secretary_can_list_cancelled_appointments_by_status_filter()
    {
        var s = new Scenario();
        s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30", AppointmentStatus.Cancelled);
        s.Seed(s.DoctorA, s.PatientA, "11:00", "11:30");

        var cancelled = await s.Service.ListAsync(new ListAppointmentsQuery(Status: AppointmentStatus.Cancelled), Ct);
        var everything = await s.Service.ListAsync(new ListAppointmentsQuery(), Ct);

        Assert.Single(cancelled.Items);
        Assert.Equal(2, everything.TotalCount);
    }

    [Fact]
    public void Appointment_response_exposes_only_administrative_fields()
    {
        var names = typeof(AppointmentResponse).GetProperties().Select(p => p.Name).Order().ToArray();

        string[] expected =
        [
            "AppointmentDate", "CancellationReason", "CreatedAtUtc", "CreatedBy", "DoctorId", "DoctorName",
            "EndTime", "Id", "Notes", "PatientId", "PatientName", "PatientPhone", "Reason", "StartTime", "Status"
        ];

        Assert.Equal(expected.Order().ToArray(), names);
    }
}
