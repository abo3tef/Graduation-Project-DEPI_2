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
}
