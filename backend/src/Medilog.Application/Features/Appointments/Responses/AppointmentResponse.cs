using Medilog.Domain.Enums;

namespace Medilog.Application.Features.Appointments.Responses;

/// <summary>
/// Appointment view returned to clients. It carries only the minimum patient
/// administrative information (name, phone) and no medical data.
/// </summary>
public sealed record AppointmentResponse(
    Guid Id,
    Guid PatientId,
    string PatientName,
    string? PatientPhone,
    Guid DoctorId,
    string DoctorName,
    DateOnly AppointmentDate,
    TimeOnly StartTime,
    TimeOnly? EndTime,
    string? Reason,
    AppointmentStatus Status,
    string? Notes,
    string? CancellationReason,
    Guid CreatedBy,
    DateTimeOffset CreatedAtUtc);
