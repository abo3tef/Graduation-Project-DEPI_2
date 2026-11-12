using FluentValidation;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.Appointments.Requests;
using Medilog.Domain.Enums;

namespace Medilog.Application.Features.Appointments.Validators;

// Shape-level rules only. Business invariants (past dates, status transitions,
// overlap) are enforced by the domain and the service, not duplicated here.

public sealed class BookAppointmentRequestValidator : AbstractValidator<BookAppointmentRequest>
{
    public BookAppointmentRequestValidator()
    {
        RuleFor(request => request.PatientId).NotEmpty();
        RuleFor(request => request.DoctorId).NotEmpty();
        RuleFor(request => request.AppointmentDate).NotNull();
        RuleFor(request => request.StartTime).NotNull();
        RuleFor(request => request.EndTime).NotNull();

        RuleFor(request => request.EndTime)
            .Must((request, end) => end > request.StartTime)
            .When(request => request.StartTime.HasValue && request.EndTime.HasValue)
            .WithMessage("The end time must be later than the start time.");

        RuleFor(request => request.Reason).MaximumLength(AppointmentRules.ReasonMaxLength);
    }
}

public sealed class RescheduleAppointmentRequestValidator : AbstractValidator<RescheduleAppointmentRequest>
{
    public RescheduleAppointmentRequestValidator()
    {
        RuleFor(request => request.AppointmentDate).NotNull();
        RuleFor(request => request.StartTime).NotNull();
        RuleFor(request => request.EndTime).NotNull();

        RuleFor(request => request.EndTime)
            .Must((request, end) => end > request.StartTime)
            .When(request => request.StartTime.HasValue && request.EndTime.HasValue)
            .WithMessage("The end time must be later than the start time.");
    }
}

public sealed class CancelAppointmentRequestValidator : AbstractValidator<CancelAppointmentRequest>
{
    public CancelAppointmentRequestValidator()
    {
        RuleFor(request => request.CancellationReason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("A cancellation reason is required.");

        RuleFor(request => request.CancellationReason)
            .Must(reason => reason!.Trim().Length <= AppointmentRules.CancellationReasonMaxLength)
            .When(request => !string.IsNullOrWhiteSpace(request.CancellationReason))
            .WithMessage($"The cancellation reason must not exceed {AppointmentRules.CancellationReasonMaxLength} characters.");
    }
}

public sealed class UpdateAppointmentStatusRequestValidator : AbstractValidator<UpdateAppointmentStatusRequest>
{
    public UpdateAppointmentStatusRequestValidator()
    {
        RuleFor(request => request.Status).NotNull().IsInEnum();

        RuleFor(request => request.Status)
            .NotEqual(AppointmentStatus.Cancelled)
            .WithMessage("Use the cancel operation to cancel an appointment.");
    }
}

public sealed class ListAppointmentsQueryValidator : AbstractValidator<ListAppointmentsQuery>
{
    public ListAppointmentsQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, PagingDefaults.MaxPageSize);

        RuleFor(query => query.Status).IsInEnum().When(query => query.Status.HasValue);

        RuleFor(query => query.DateTo)
            .Must((query, to) => to >= query.DateFrom)
            .When(query => query.DateFrom.HasValue && query.DateTo.HasValue)
            .WithMessage("The end date must not be earlier than the start date.");
    }
}
