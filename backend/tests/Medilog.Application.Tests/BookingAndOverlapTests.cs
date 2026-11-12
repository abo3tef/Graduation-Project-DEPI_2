using Medilog.Application.Common.Exceptions;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Common;
using Medilog.Domain.Enums;
using static Medilog.Application.Tests.Support.Scenario;

namespace Medilog.Application.Tests;

public class BookingAndOverlapTests
{
    [Fact]
    public async Task Book_creates_scheduled_appointment_with_creator_from_current_user()
    {
        var s = new Scenario();

        var result = await s.BookAsync("10:00", "10:30");

        Assert.Equal(AppointmentStatus.Scheduled, result.Status);
        Assert.Equal(s.Secretary.Id, result.CreatedBy);
        Assert.Equal(s.DoctorA.Id, result.DoctorId);
        Assert.Equal(s.PatientA.Id, result.PatientId);
        Assert.Single(s.Store.Appointments);
        Assert.True(s.Store.Transactions.Single().Committed);
        Assert.Equal(s.DoctorA.Id, s.Store.Transactions.Single().DoctorId);
    }

    // existing 10:00-10:30 (or 10:00-11:00 for containment) vs the new interval.
    [Theory]
    [InlineData("10:00", "10:30", "10:30", "11:00", false)] // adjacent after
    [InlineData("10:30", "11:00", "10:00", "10:30", false)] // adjacent before
    [InlineData("10:00", "10:30", "10:15", "10:45", true)]  // partial: new starts inside
    [InlineData("10:15", "10:45", "10:00", "10:30", true)]  // partial: new ends inside
    [InlineData("10:00", "11:00", "10:15", "10:30", true)]  // new inside existing
    [InlineData("10:15", "10:30", "10:00", "11:00", true)]  // new wraps existing
    [InlineData("10:00", "10:30", "10:00", "10:30", true)]  // identical
    [InlineData("10:00", "10:30", "09:00", "09:30", false)] // clearly apart
    public async Task Book_applies_the_overlap_rule(
        string existingStart, string existingEnd, string newStart, string newEnd, bool expectConflict)
    {
        var s = new Scenario();
        s.Seed(s.DoctorA, s.PatientA, existingStart, existingEnd);

        var act = () => s.BookAsync(newStart, newEnd);

        if (expectConflict)
        {
            await Assert.ThrowsAsync<ConflictException>(act);
            Assert.False(s.Store.Transactions.Single().Committed);
            Assert.Single(s.Store.Appointments);
        }
        else
        {
            await act();
            Assert.Equal(2, s.Store.Appointments.Count);
        }
    }

    [Fact]
    public async Task Book_allows_same_time_for_a_different_doctor()
    {
        var s = new Scenario();
        s.AssignSecretaryTo(s.DoctorB);
        s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");

        await s.BookAsync("10:00", "10:30", s.DoctorB, s.PatientB);

        Assert.Equal(2, s.Store.Appointments.Count);
    }

    [Fact]
    public async Task Book_allows_same_time_on_a_different_date()
    {
        var s = new Scenario();
        s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30");

        await s.BookAsync("10:00", "10:30", date: Day.AddDays(1));

        Assert.Equal(2, s.Store.Appointments.Count);
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task Book_reuses_the_slot_of_a_cancelled_or_no_show_appointment(AppointmentStatus status)
    {
        var s = new Scenario();
        s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30", status);

        await s.BookAsync("10:00", "10:30");

        Assert.Equal(2, s.Store.Appointments.Count);
    }

    [Theory]
    [InlineData(AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Completed)]
    public async Task Book_rejects_a_slot_held_by_scheduled_confirmed_or_completed(AppointmentStatus status)
    {
        var s = new Scenario();
        s.Seed(s.DoctorA, s.PatientA, "10:00", "10:30", status);

        await Assert.ThrowsAsync<ConflictException>(() => s.BookAsync("10:15", "10:45"));
    }

    [Theory]
    [InlineData("10:00", "10:30", true)]  // legacy point 10:15 lies inside
    [InlineData("10:30", "11:00", false)] // legacy point 10:15 is before the new interval
    [InlineData("10:15", "10:45", true)]  // interval starts exactly at the point
    public async Task Book_treats_a_legacy_row_without_end_time_as_a_point_at_its_start(
        string newStart, string newEnd, bool expectConflict)
    {
        var s = new Scenario();
        s.Seed(s.DoctorA, s.PatientA, "10:15", end: null);

        var act = () => s.BookAsync(newStart, newEnd);

        if (expectConflict)
        {
            await Assert.ThrowsAsync<ConflictException>(act);
        }
        else
        {
            await act();
        }
    }

    [Fact]
    public async Task Book_rejects_a_past_date()
    {
        var s = new Scenario();

        await Assert.ThrowsAsync<DomainException>(() => s.BookAsync("10:00", "10:30", date: Day.AddDays(-5)));
    }

    [Fact]
    public async Task Book_rejects_an_earlier_time_today_but_accepts_a_later_one()
    {
        var s = new Scenario();
        s.Clock.LocalNow = Day.ToDateTime(T("12:00"));

        await Assert.ThrowsAsync<DomainException>(() => s.BookAsync("11:00", "11:30"));
        await s.BookAsync("12:30", "13:00");

        Assert.Single(s.Store.Appointments);
    }

    [Fact]
    public async Task Book_rejects_a_start_time_that_does_not_exist_in_the_clinic_time_zone()
    {
        var s = new Scenario();
        s.Clock.NonExistentTimes.Add(Day.ToDateTime(T("10:00")));

        await Assert.ThrowsAsync<RequestValidationException>(() => s.BookAsync("10:00", "10:30"));
    }

    [Theory]
    [InlineData("10:00", null)]
    [InlineData("10:00", "10:00")]
    [InlineData("10:30", "10:00")]
    public async Task Book_requires_an_end_time_later_than_the_start_time(string start, string? end)
    {
        var s = new Scenario();

        await Assert.ThrowsAsync<RequestValidationException>(() => s.BookAsync(start, end));
        Assert.Empty(s.Store.Appointments);
    }

    [Fact]
    public async Task Book_rejects_a_patient_who_belongs_to_another_doctor()
    {
        var s = new Scenario();

        await Assert.ThrowsAsync<NotFoundException>(() => s.BookAsync("10:00", "10:30", s.DoctorA, s.PatientB));
    }

    [Fact]
    public async Task Book_rejects_a_patient_without_a_doctor()
    {
        var s = new Scenario();
        s.PatientA.DoctorId = null;

        await Assert.ThrowsAsync<NotFoundException>(() => s.BookAsync("10:00", "10:30"));
    }

    [Fact]
    public async Task Book_rejects_an_inactive_doctor()
    {
        var s = new Scenario();
        s.DoctorA.AccountStatus = AccountStatus.Inactive;

        await Assert.ThrowsAsync<ConflictException>(() => s.BookAsync("10:00", "10:30"));
    }

    [Fact]
    public async Task Book_stores_times_as_whole_seconds_because_the_column_is_time0()
    {
        var s = new Scenario();

        var result = await s.Service.BookAsync(
            new Features.Appointments.Requests.BookAppointmentRequest(
                s.PatientA.Id, s.DoctorA.Id, Day, new TimeOnly(10, 0, 0, 900), new TimeOnly(10, 30, 0, 400), "  "),
            CancellationToken.None);

        Assert.Equal(T("10:00:00"), result.StartTime);
        Assert.Equal(T("10:30:00"), result.EndTime);
        Assert.Null(result.Reason); // whitespace-only reason is stored as null
    }
}
