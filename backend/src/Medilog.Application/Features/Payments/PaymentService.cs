using AutoMapper;
using FluentValidation;
using Medilog.Application.Common;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Paging;
using Medilog.Application.Common.Validation;
using Medilog.Application.Features.Payments.Requests;
using Medilog.Application.Features.Payments.Responses;
using Medilog.Application.Interfaces;
using Medilog.Domain.Common;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;

namespace Medilog.Application.Features.Payments;

/// <inheritdoc cref="IPaymentService"/>
public sealed class PaymentService : IPaymentService
{
    private const string PaymentNotFoundMessage = "The payment was not found.";
    private const string AppointmentNotFoundMessage = "The appointment was not found.";

    private readonly IPaymentStore _store;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly TimeProvider _timeProvider;
    private readonly IValidator<CreatePaymentRequest> _createValidator;
    private readonly IValidator<UpdatePaymentRequest> _updateValidator;
    private readonly IValidator<UpdatePaymentStatusRequest> _statusValidator;
    private readonly IValidator<ListPaymentsQuery> _listValidator;
    private readonly IValidator<PayableAppointmentsQuery> _payableValidator;

    public PaymentService(
        IPaymentStore store,
        ICurrentUser currentUser,
        IMapper mapper,
        TimeProvider timeProvider,
        IValidator<CreatePaymentRequest> createValidator,
        IValidator<UpdatePaymentRequest> updateValidator,
        IValidator<UpdatePaymentStatusRequest> statusValidator,
        IValidator<ListPaymentsQuery> listValidator,
        IValidator<PayableAppointmentsQuery> payableValidator)
    {
        _store = store;
        _currentUser = currentUser;
        _mapper = mapper;
        _timeProvider = timeProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _statusValidator = statusValidator;
        _listValidator = listValidator;
        _payableValidator = payableValidator;
    }

    public async Task<PaymentResponse> CreateAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_createValidator, request, cancellationToken);
        var actor = await RequireActorAsync(cancellationToken);

        var appointment = await _store.GetAppointmentAsync(request.AppointmentId, cancellationToken);

        // Out-of-scope appointments look exactly like missing ones.
        if (appointment is null || !await CanAccessDoctorAsync(appointment.DoctorId, cancellationToken))
        {
            throw new NotFoundException(AppointmentNotFoundMessage, ErrorCodes.PaymentAppointmentNotFound);
        }

        if (appointment.HasPayment)
        {
            throw new ConflictException(
                "A payment already exists for this appointment.",
                ErrorCodes.PaymentAlreadyExists);
        }

        var payment = Payment.Create(
            appointment.Id,
            appointment.PatientId,
            appointment.DoctorId,
            actor.Id,
            request.Amount!.Value,
            request.Method!.Value,
            request.Notes);

        await _store.AddAsync(payment, cancellationToken);
        await _store.SaveChangesAsync(cancellationToken);

        return await GetResponseAsync(payment.Id, cancellationToken);
    }

    public async Task<PaymentResponse> GetAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        await RequireActorAsync(cancellationToken);

        var payment = await _store.GetPaymentWithDetailsAsync(paymentId, cancellationToken);

        if (payment is null || !await CanAccessDoctorAsync(payment.DoctorId, cancellationToken))
        {
            throw new NotFoundException(PaymentNotFoundMessage, ErrorCodes.PaymentNotFound);
        }

        return _mapper.Map<PaymentResponse>(payment);
    }

    public async Task<PagedResult<PaymentResponse>> ListAsync(
        ListPaymentsQuery query,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_listValidator, query, cancellationToken);
        var actor = await RequireActorAsync(cancellationToken);

        // The visible doctors always come from the server-side identity. A client
        // supplied doctor filter can only narrow the set, never widen it.
        var visibleDoctorIds = await GetVisibleDoctorIdsAsync(actor, cancellationToken);

        if (query.DoctorId.HasValue)
        {
            visibleDoctorIds = visibleDoctorIds.Contains(query.DoctorId.Value)
                ? [query.DoctorId.Value]
                : [];
        }

        if (visibleDoctorIds.Count == 0)
        {
            return new PagedResult<PaymentResponse>([], query.PageNumber, query.PageSize, 0);
        }

        var criteria = new PaymentListCriteria(
            visibleDoctorIds,
            NormalizeSearch(query.Search),
            query.Status,
            query.Method,
            query.PatientId,
            query.AppointmentId,
            query.DateFrom,
            query.DateTo,
            query.PageNumber,
            query.PageSize);

        return await _store.ListAsync(criteria, cancellationToken);
    }

    public async Task<PaymentResponse> UpdateAsync(
        Guid paymentId,
        UpdatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_updateValidator, request, cancellationToken);
        await RequireActorAsync(cancellationToken);

        var payment = await GetModifiablePaymentAsync(paymentId, cancellationToken);

        // Paid is final; report it as a state conflict rather than a shape error.
        if (payment.Status == PaymentStatus.Paid)
        {
            throw new ConflictException("A paid payment cannot be changed.", DomainErrorCodes.PaymentPaidFinal);
        }

        payment.UpdateDetails(request.Amount!.Value, request.Method!.Value, request.Notes);

        await _store.SaveChangesAsync(cancellationToken);

        return await GetResponseAsync(payment.Id, cancellationToken);
    }

    public async Task<PaymentResponse> UpdateStatusAsync(
        Guid paymentId,
        UpdatePaymentStatusRequest request,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_statusValidator, request, cancellationToken);
        await RequireActorAsync(cancellationToken);

        var payment = await GetModifiablePaymentAsync(paymentId, cancellationToken);
        var newStatus = request.Status!.Value;

        // The Application layer owns the HTTP meaning of an invalid transition (409).
        if (!Payment.IsAllowedTransition(payment.Status, newStatus))
        {
            throw new ConflictException(
                $"A payment cannot change from {payment.Status} to {newStatus}.",
                DomainErrorCodes.PaymentInvalidStatusTransition);
        }

        payment.ChangeStatus(newStatus, _timeProvider.GetUtcNow());

        await _store.SaveChangesAsync(cancellationToken);

        return await GetResponseAsync(payment.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<PayableAppointmentResponse>> GetPayableAppointmentsAsync(
        PayableAppointmentsQuery query,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_payableValidator, query, cancellationToken);
        var actor = await RequireActorAsync(cancellationToken);

        var visibleDoctorIds = await GetVisibleDoctorIdsAsync(actor, cancellationToken);

        return visibleDoctorIds.Count == 0
            ? []
            : await _store.GetPayableAppointmentsAsync(
                visibleDoctorIds,
                NormalizeSearch(query.Search),
                query.Limit,
                cancellationToken);
    }

    /// <summary>
    /// Scope-checks a payment on a non-tracked read, then loads it for
    /// modification. Out-of-scope payments look like missing ones.
    /// </summary>
    private async Task<Payment> GetModifiablePaymentAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var doctorId = await _store.GetPaymentDoctorIdAsync(paymentId, cancellationToken);

        if (doctorId is null || !await CanAccessDoctorAsync(doctorId.Value, cancellationToken))
        {
            throw new NotFoundException(PaymentNotFoundMessage, ErrorCodes.PaymentNotFound);
        }

        return await _store.GetPaymentAsync(paymentId, cancellationToken)
            ?? throw new NotFoundException(PaymentNotFoundMessage, ErrorCodes.PaymentNotFound);
    }

    private async Task<ActorInfo> RequireActorAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId || userId == Guid.Empty ||
            _currentUser.Role is not { } role || !Enum.IsDefined(role))
        {
            throw new ForbiddenException("The current user cannot manage payments.", ErrorCodes.IdentityInvalid);
        }

        var actor = await _store.GetActorAsync(role, userId, cancellationToken);

        if (actor is null || !actor.IsActive)
        {
            throw new ForbiddenException("The current user cannot manage payments.", ErrorCodes.IdentityInvalid);
        }

        return actor;
    }

    private async Task<bool> CanAccessDoctorAsync(Guid doctorId, CancellationToken cancellationToken) =>
        _currentUser.Role == UserRole.Doctor
            ? doctorId == _currentUser.UserId
            : await _store.IsDoctorAssignedAsync(_currentUser.UserId!.Value, doctorId, cancellationToken);

    private async Task<IReadOnlyCollection<Guid>> GetVisibleDoctorIdsAsync(
        ActorInfo actor,
        CancellationToken cancellationToken) =>
        _currentUser.Role == UserRole.Doctor
            ? [actor.Id]
            : await _store.GetAssignedDoctorIdsAsync(actor.Id, cancellationToken);

    private async Task<PaymentResponse> GetResponseAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await _store.GetPaymentWithDetailsAsync(paymentId, cancellationToken)
            ?? throw new NotFoundException(PaymentNotFoundMessage, ErrorCodes.PaymentNotFound);

        return _mapper.Map<PaymentResponse>(payment);
    }

    private static string? NormalizeSearch(string? search)
    {
        var trimmed = search?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
