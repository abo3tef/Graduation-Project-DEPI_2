using Medilog.Domain.Common;
using Medilog.Domain.Enums;

namespace Medilog.Domain.Entities;

/// <summary>
/// A scheduled booking between a patient and a doctor on a specific local
/// date and time.
/// </summary>
public sealed class Appointment : CreatedEntity
{
    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public Guid DoctorId { get; set; }

    public Doctor Doctor { get; set; } = null!;

    /// <summary>
    /// The secretary or doctor who booked the appointment. The Class
    /// Specification declares this as a polymorphic FK; MySQL cannot enforce
    /// a single foreign key against two tables, so this column stores the
    /// identifier without a database-level FK. Referential integrity for this
    /// attribute must be enforced by the application flow.
    /// </summary>
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// The appointment date in the clinic's local wall-clock calendar.
    /// </summary>
    public DateOnly AppointmentDate { get; set; }

    /// <summary>
    /// The appointment start time in the clinic's local wall-clock time.
    /// </summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>
    /// The appointment end time in the clinic's local wall-clock time.
    /// The Class Specification does not mark this attribute as required.
    /// </summary>
    public TimeOnly? EndTime { get; set; }

    public string? Reason { get; set; }

    public AppointmentStatus Status { get; set; }

    public string? Notes { get; set; }

    public string? CancellationReason { get; set; }

    /// <summary>
    /// Maximum length of <see cref="CancellationReason"/> enforced by the domain.
    /// The database column is <c>text</c>; this limit is a domain rule, not a schema one.
    /// </summary>
    public const int CancellationReasonMaxLength = 500;

    /// <summary>
    /// Whether the appointment can still change (rescheduled, cancelled or moved
    /// to another status). Completed, Cancelled and No-Show are final.
    /// </summary>
    public bool IsOpen => Status is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed;

    /// <summary>
    /// Whether the appointment holds its time slot. Cancelled and No-Show
    /// appointments free the slot for new bookings (decision Q-09).
    /// </summary>
    public bool OccupiesSlot => Status is not (AppointmentStatus.Cancelled or AppointmentStatus.NoShow);

    /// <summary>
    /// Creates a new appointment in the <see cref="AppointmentStatus.Scheduled"/> status.
    /// <paramref name="createdBy"/> is the authenticated doctor or secretary resolved
    /// by the Application layer; it is never taken from client input.
    /// </summary>
    /// <param name="clinicLocalNow">The current wall-clock time in the clinic time zone.</param>
    public static Appointment Create(
        Guid patientId,
        Guid doctorId,
        Guid createdBy,
        DateOnly appointmentDate,
        TimeOnly startTime,
        TimeOnly? endTime,
        string? reason,
        DateTime clinicLocalNow)
    {
        if (patientId == Guid.Empty)
        {
            throw new DomainException("An appointment must belong to a patient.", DomainErrorCodes.MissingReference);
        }

        if (doctorId == Guid.Empty)
        {
            throw new DomainException("An appointment must belong to a doctor.", DomainErrorCodes.MissingReference);
        }

        if (createdBy == Guid.Empty)
        {
            throw new DomainException("The appointment creator must be identified.", DomainErrorCodes.MissingReference);
        }

        EnsureValidInterval(startTime, endTime);
        EnsureNotInPast(appointmentDate, startTime, clinicLocalNow);

        return new Appointment
        {
            PatientId = patientId,
            DoctorId = doctorId,
            CreatedBy = createdBy,
            AppointmentDate = appointmentDate,
            StartTime = startTime,
            EndTime = endTime,
            Reason = reason,
            Status = AppointmentStatus.Scheduled
        };
    }

    /// <summary>
    /// Moves the appointment to a new date and time. The patient and doctor never
    /// change; moving to another doctor means cancelling and creating a new
    /// appointment. The status is unchanged. The caller must have checked that the
    /// new interval does not overlap another appointment of the same doctor
    /// (excluding this appointment).
    /// </summary>
    public void Reschedule(
        DateOnly newDate,
        TimeOnly newStartTime,
        TimeOnly? newEndTime,
        DateTime clinicLocalNow)
    {
        EnsureOpen("rescheduled");
        EnsureValidInterval(newStartTime, newEndTime);
        EnsureNotInPast(newDate, newStartTime, clinicLocalNow);

        AppointmentDate = newDate;
        StartTime = newStartTime;
        EndTime = newEndTime;
    }

    /// <summary>
    /// Cancels an open appointment. A non-empty reason of at most
    /// <see cref="CancellationReasonMaxLength"/> characters is required.
    /// </summary>
    public void Cancel(string? cancellationReason)
    {
        EnsureOpen("cancelled");

        var reason = cancellationReason?.Trim();

        if (string.IsNullOrEmpty(reason))
        {
            throw new DomainException("A cancellation reason is required.", DomainErrorCodes.CancellationReasonRequired);
        }

        if (reason.Length > CancellationReasonMaxLength)
        {
            throw new DomainException(
                $"The cancellation reason must not exceed {CancellationReasonMaxLength} characters.",
                DomainErrorCodes.CancellationReasonTooLong);
        }

        Status = AppointmentStatus.Cancelled;
        CancellationReason = reason;
    }

    /// <summary>
    /// Applies an allowed status transition:
    /// Scheduled → Confirmed / Completed / No-Show; Confirmed → Completed / No-Show.
    /// Cancelling must go through <see cref="Cancel"/> so a reason is captured.
    /// Completed, Cancelled and No-Show are final. Which role may request a
    /// particular transition is decided by the Application layer.
    /// </summary>
    public void UpdateStatus(AppointmentStatus newStatus)
    {
        if (newStatus == AppointmentStatus.Cancelled)
        {
            throw new DomainException("Use the cancel operation to cancel an appointment.", DomainErrorCodes.UseCancelOperation);
        }

        var allowed = Status switch
        {
            AppointmentStatus.Scheduled => newStatus is AppointmentStatus.Confirmed
                or AppointmentStatus.Completed
                or AppointmentStatus.NoShow,
            AppointmentStatus.Confirmed => newStatus is AppointmentStatus.Completed
                or AppointmentStatus.NoShow,
            _ => false
        };

        if (!allowed)
        {
            throw new DomainException(
                $"An appointment cannot change from {Status} to {newStatus}.",
                DomainErrorCodes.InvalidStatusTransition);
        }

        Status = newStatus;
    }

    /// <summary>
    /// Pure overlap check against another appointment. Two appointments overlap
    /// only when they have the same doctor and date, both still hold their slot,
    /// and <c>newStart &lt; existingEnd &amp;&amp; newEnd &gt; existingStart</c>.
    /// Back-to-back appointments do not overlap. An appointment is never
    /// considered to overlap itself (so rescheduling can reuse its own slot).
    /// </summary>
    public bool OverlapsWith(Appointment other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Id == other.Id || DoctorId != other.DoctorId || !OccupiesSlot || !other.OccupiesSlot)
        {
            return false;
        }

        return OverlapsInterval(other.AppointmentDate, other.StartTime, other.EndTime);
    }

    /// <summary>
    /// Pure overlap check of this (existing) appointment against a candidate
    /// interval. The caller is responsible for comparing only appointments of the
    /// same doctor.
    /// </summary>
    public bool OverlapsInterval(DateOnly date, TimeOnly startTime, TimeOnly? endTime)
    {
        if (!OccupiesSlot || date != AppointmentDate)
        {
            return false;
        }

        if (EndTime is null || endTime is null)
        {
            throw new DomainException(
                "An appointment without an end time cannot be checked for overlap.",
                DomainErrorCodes.EndTimeRequired);
        }

        return startTime < EndTime.Value && endTime.Value > StartTime;
    }

    private void EnsureOpen(string action)
    {
        if (!IsOpen)
        {
            throw new DomainException($"A {Status} appointment cannot be {action}.", DomainErrorCodes.NotOpen);
        }
    }

    private static void EnsureValidInterval(TimeOnly startTime, TimeOnly? endTime)
    {
        if (endTime is null)
        {
            throw new DomainException("The appointment end time is required.", DomainErrorCodes.EndTimeRequired);
        }

        if (endTime.Value <= startTime)
        {
            throw new DomainException(
                "The appointment end time must be later than its start time.",
                DomainErrorCodes.InvalidInterval);
        }
    }

    private static void EnsureNotInPast(DateOnly date, TimeOnly startTime, DateTime clinicLocalNow)
    {
        if (date.ToDateTime(startTime) < clinicLocalNow)
        {
            throw new DomainException("An appointment cannot be booked in the past.", DomainErrorCodes.InThePast);
        }
    }
}
