using Medilog.Application.Common.Paging;
using Medilog.Application.Features.Appointments.Requests;
using Medilog.Application.Features.Appointments.Responses;

namespace Medilog.Application.Features.Appointments;

/// <summary>
/// Appointment use cases. The acting user always comes from the authenticated
/// identity. Failures are reported with the typed Application exceptions and
/// <c>DomainException</c>; out-of-scope ids behave like missing ones.
/// </summary>
public interface IAppointmentService
{
    Task<AppointmentResponse> BookAsync(BookAppointmentRequest request, CancellationToken cancellationToken);

    Task<AppointmentResponse> RescheduleAsync(
        Guid appointmentId,
        RescheduleAppointmentRequest request,
        CancellationToken cancellationToken);

    Task<AppointmentResponse> CancelAsync(
        Guid appointmentId,
        CancelAppointmentRequest request,
        CancellationToken cancellationToken);

    Task<AppointmentResponse> UpdateStatusAsync(
        Guid appointmentId,
        UpdateAppointmentStatusRequest request,
        CancellationToken cancellationToken);

    Task<AppointmentResponse> GetAsync(Guid appointmentId, CancellationToken cancellationToken);

    Task<PagedResult<AppointmentResponse>> ListAsync(
        ListAppointmentsQuery query,
        CancellationToken cancellationToken);
}
