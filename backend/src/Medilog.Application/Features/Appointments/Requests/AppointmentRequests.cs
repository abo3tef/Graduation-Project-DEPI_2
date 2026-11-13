using Medilog.Application.Common.Paging;
using Medilog.Domain.Enums;

namespace Medilog.Application.Features.Appointments.Requests;

/// <summary>
/// Booking input. The creator, role and status are never part of the request;
/// they come from the authenticated identity and the domain.
/// Date and time values are nullable so that "missing" can be reported.
/// </summary>
public sealed record BookAppointmentRequest(
    Guid PatientId,
    Guid DoctorId,
    DateOnly? AppointmentDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    string? Reason);

/// <summary>Reschedule input: date and time only.</summary>
public sealed record RescheduleAppointmentRequest(
    DateOnly? AppointmentDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime);

public sealed record CancelAppointmentRequest(string? CancellationReason);

/// <summary>Confirmed, Completed or NoShow. Cancelling uses the cancel operation.</summary>
public sealed record UpdateAppointmentStatusRequest(AppointmentStatus? Status);

/// <summary>Optional filters plus the shared paging shape for the appointment list.</summary>
public sealed record ListAppointmentsQuery(
    Guid? DoctorId = null,
    Guid? PatientId = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    AppointmentStatus? Status = null,
    int PageNumber = PagingDefaults.DefaultPageNumber,
    int PageSize = PagingDefaults.DefaultPageSize) : IPagedQuery;
