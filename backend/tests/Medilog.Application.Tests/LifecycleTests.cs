using Medilog.Application.Common.Exceptions;
using Medilog.Application.Features.Appointments.Requests;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Common;
using Medilog.Domain.Enums;
using static Medilog.Application.Tests.Support.Scenario;

namespace Medilog.Application.Tests;

public class LifecycleTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static RescheduleAppointmentRequest Move(string start, string? end, DateOnly? date = null) =>
        new(date ?? Day, T(start), end is null ? null : T(end));

    [Fact]
    public async Task Reschedule_changes_date_and_time_but_keeps_patient_doctor_and_status()
    {
        var s = new Scenario();
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30", AppointmentStatus.Confirmed);

        var result = await s.Service.RescheduleAsync(appointment.Id, Move("14:00", "14:45", Day.AddDays(3)), Ct);

        Assert.Equal(Day.AddDays(3), result.AppointmentDate);
        Assert.Equal(T("14:00"), result.StartTime);
        Assert.Equal(T("14:45"), result.EndTime);
        Assert.Equal(s.DoctorA.Id, result.DoctorId);
        Assert.Equal(s.PatientA.Id, result.PatientId);
        Assert.Equal(AppointmentStatus.Confirmed, result.Status);
        Assert.True(s.Store.Transactions.Single().Committed);
    }

    [Fact]
    public async Task Reschedule_may_overlap_the_appointments_own_previous_slot()
    {
        var s = new Scenario();
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");

        var result = await s.Service.RescheduleAsync(appointment.Id, Move("10:15", "10:45"), Ct);

        Assert.Equal(T("10:15"), result.StartTime);
    }

    [Fact]
    public async Task Reschedule_into_another_appointments_slot_is_a_conflict()
    {
        var s = new Scenario();
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");
        s.Seed(s.DoctorA, s.PatientA, "11:00", "11:30");

        await Assert.ThrowsAsync<ConflictException>(() => s.Service.RescheduleAsync(appointment.Id, Move("11:15", "11:45"), Ct));

        Assert.False(s.Store.Transactions.Single().Committed);
    }

    [Fact]
    public async Task Reschedule_into_a_cancelled_appointments_slot_is_allowed()
    {
        var s = new Scenario();
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");
        s.Seed(s.DoctorA, s.PatientA, "11:00", "11:30", AppointmentStatus.Cancelled);

        var result = await s.Service.RescheduleAsync(appointment.Id, Move("11:00", "11:30"), Ct);

        Assert.Equal(T("11:00"), result.StartTime);
    }

    [Fact]
    public async Task Reschedule_to_the_past_is_rejected()
    {
        var s = new Scenario();
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");

        await Assert.ThrowsAsync<DomainException>(
            () => s.Service.RescheduleAsync(appointment.Id, Move("10:00", "10:30", Day.AddDays(-30)), Ct));
    }

    [Theory]
    [InlineData("10:00", null)]
    [InlineData("10:00", "10:00")]
    public async Task Reschedule_requires_an_end_time_later_than_the_start_time(string start, string? end)
    {
        var s = new Scenario();
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");

        await Assert.ThrowsAsync<RequestValidationException>(
            () => s.Service.RescheduleAsync(appointment.Id, Move(start, end), Ct));
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task Closed_appointments_cannot_be_rescheduled(AppointmentStatus status)
    {
        var s = new Scenario();
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30", status);

        await Assert.ThrowsAsync<DomainException>(
            () => s.Service.RescheduleAsync(appointment.Id, Move("14:00", "14:30"), Ct));
    }

    [Fact]
    public async Task Cancel_stores_the_reason_and_frees_the_slot()
    {
        var s = new Scenario();
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");

        var result = await s.Service.CancelAsync(appointment.Id, new CancelAppointmentRequest("  patient asked  "), Ct);

        Assert.Equal(AppointmentStatus.Cancelled, result.Status);
        Assert.Equal("patient asked", result.CancellationReason);

        await s.BookAsync("10:00", "10:30"); // slot is free again
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Cancel_requires_a_reason(string? reason)
    {
        var s = new Scenario();
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");

        await Assert.ThrowsAsync<RequestValidationException>(
            () => s.Service.CancelAsync(appointment.Id, new CancelAppointmentRequest(reason), Ct));
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
    }

    [Fact]
    public async Task Cancel_accepts_500_characters_and_rejects_501()
    {
        var s = new Scenario();
        var ok = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");
        var tooLong = s.Seed(s.DoctorA, s.PatientA, "11:00", "11:30");

        await s.Service.CancelAsync(ok.Id, new CancelAppointmentRequest(new string('a', 500)), Ct);
        await Assert.ThrowsAsync<RequestValidationException>(
            () => s.Service.CancelAsync(tooLong.Id, new CancelAppointmentRequest(new string('a', 501)), Ct));

        Assert.Equal(AppointmentStatus.Cancelled, ok.Status);
        Assert.Equal(AppointmentStatus.Scheduled, tooLong.Status);
    }

    [Fact]
    public async Task Cancelling_twice_is_rejected()
    {
        var s = new Scenario();
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");
        await s.Service.CancelAsync(appointment.Id, new CancelAppointmentRequest("first"), Ct);

        await Assert.ThrowsAsync<DomainException>(
            () => s.Service.CancelAsync(appointment.Id, new CancelAppointmentRequest("second"), Ct));
    }

    [Fact]
    public async Task Status_update_cannot_be_used_to_cancel()
    {
        var s = new Scenario();
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");

        await Assert.ThrowsAsync<RequestValidationException>(
            () => s.Service.UpdateStatusAsync(appointment.Id, new UpdateAppointmentStatusRequest(AppointmentStatus.Cancelled), Ct));
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task Final_statuses_cannot_change(AppointmentStatus current)
    {
        var s = new Scenario();
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30", current);

        await Assert.ThrowsAsync<DomainException>(
            () => s.Service.UpdateStatusAsync(appointment.Id, new UpdateAppointmentStatusRequest(AppointmentStatus.Confirmed), Ct));
    }

    [Fact]
    public async Task Confirmed_cannot_go_back_to_scheduled()
    {
        var s = new Scenario();
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30", AppointmentStatus.Confirmed);

        await Assert.ThrowsAsync<DomainException>(
            () => s.Service.UpdateStatusAsync(appointment.Id, new UpdateAppointmentStatusRequest(AppointmentStatus.Scheduled), Ct));
    }

    [Fact]
    public async Task A_failed_change_is_rolled_back_not_committed()
    {
        var s = new Scenario();
        var appointment = s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30", AppointmentStatus.Cancelled);

        await Assert.ThrowsAsync<DomainException>(
            () => s.Service.CancelAsync(appointment.Id, new CancelAppointmentRequest("again"), Ct));

        var transaction = s.Store.Transactions.Single();
        Assert.False(transaction.Committed);
        Assert.True(transaction.Disposed);
    }
}
