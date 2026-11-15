using Medilog.Application.Common.Paging;
using Medilog.Domain.Enums;

namespace Medilog.Application.Features.Payments.Requests;

/// <summary>
/// Payment creation input. Ownership, recorder, status and timestamps are never
/// part of the request; they come from the authenticated identity, the linked
/// appointment and the domain.
/// </summary>
public sealed record CreatePaymentRequest(
    Guid AppointmentId,
    decimal? Amount,
    PaymentMethod? Method,
    string? Notes);

/// <summary>
/// Editable payment details only. The appointment, patient, doctor, status,
/// recorder and paid time cannot be changed through this request.
/// </summary>
public sealed record UpdatePaymentRequest(
    decimal? Amount,
    PaymentMethod? Method,
    string? Notes);

/// <summary>Pending, Partial or Paid. Transitions are enforced by the domain.</summary>
public sealed record UpdatePaymentStatusRequest(PaymentStatus? Status);

/// <summary>
/// Optional filters plus the shared paging shape. <see cref="DateFrom"/> and
/// <see cref="DateTo"/> filter the billed appointment date (the date shown for a
/// payment in the F2 screen).
/// </summary>
public sealed record ListPaymentsQuery(
    string? Search = null,
    PaymentStatus? Status = null,
    PaymentMethod? Method = null,
    Guid? PatientId = null,
    Guid? AppointmentId = null,
    Guid? DoctorId = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    int PageNumber = PagingDefaults.DefaultPageNumber,
    int PageSize = PagingDefaults.DefaultPageSize) : IPagedQuery;

/// <summary>
/// Bounded lookup query for the appointments that can still be billed (not
/// Cancelled or No-Show) and do not have a payment yet.
/// </summary>
public sealed record PayableAppointmentsQuery(
    string? Search = null,
    int Limit = PaymentRules.DefaultPayableLimit);
