using Medilog.Application.Common.Paging;
using Medilog.Application.Features.Payments.Requests;
using Medilog.Application.Features.Payments.Responses;

namespace Medilog.Application.Features.Payments;

/// <summary>
/// Payment use cases. The acting user always comes from the authenticated
/// identity. Out-of-scope ids behave like missing ones, and disallowed status
/// changes are reported as conflicts.
/// </summary>
public interface IPaymentService
{
    Task<PaymentResponse> CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken);

    Task<PaymentResponse> GetAsync(Guid paymentId, CancellationToken cancellationToken);

    Task<PagedResult<PaymentResponse>> ListAsync(ListPaymentsQuery query, CancellationToken cancellationToken);

    Task<PaymentResponse> UpdateAsync(
        Guid paymentId,
        UpdatePaymentRequest request,
        CancellationToken cancellationToken);

    Task<PaymentResponse> UpdateStatusAsync(
        Guid paymentId,
        UpdatePaymentStatusRequest request,
        CancellationToken cancellationToken);

    /// <summary>Appointments in the caller's scope that do not have a payment yet.</summary>
    Task<IReadOnlyList<PayableAppointmentResponse>> GetPayableAppointmentsAsync(
        PayableAppointmentsQuery query,
        CancellationToken cancellationToken);
}
