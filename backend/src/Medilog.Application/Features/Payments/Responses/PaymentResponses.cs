using Medilog.Domain.Enums;

namespace Medilog.Application.Features.Payments.Responses;

/// <summary>
/// Payment view returned to clients. It carries only administrative billing
/// data and the patient's name; no medical data is exposed.
/// </summary>
public sealed record PaymentResponse(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    Guid AppointmentId,
    DateOnly AppointmentDate,
    decimal Amount,
    PaymentMethod Method,
    PaymentStatus Status,
    DateTimeOffset? PaidAt,
    string? Notes);

/// <summary>
/// An appointment in the caller's scope that does not have a payment yet. Used
/// by the F2 payment form instead of manual identifier entry.
/// </summary>
public sealed record PayableAppointmentResponse(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    DateOnly AppointmentDate,
    TimeOnly StartTime);
