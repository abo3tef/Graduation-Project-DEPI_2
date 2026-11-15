using FluentValidation;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.Payments.Requests;
using Medilog.Domain.Entities;

namespace Medilog.Application.Features.Payments.Validators;

// Shape-level rules only. Scope, duplicate-payment, paid-final and status
// transition rules are enforced by the service and the domain.

public sealed class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    public CreatePaymentRequestValidator()
    {
        RuleFor(request => request.AppointmentId).NotEmpty();
        RuleFor(request => request.Amount).NotNull();
        RuleFor(request => request.Amount).SetValidator(new PaymentAmountValidator());
        RuleFor(request => request.Method).NotNull().IsInEnum();
        RuleFor(request => request.Notes).MaximumLength(Payment.NotesMaxLength);
    }
}

public sealed class UpdatePaymentRequestValidator : AbstractValidator<UpdatePaymentRequest>
{
    public UpdatePaymentRequestValidator()
    {
        RuleFor(request => request.Amount).NotNull();
        RuleFor(request => request.Amount).SetValidator(new PaymentAmountValidator());
        RuleFor(request => request.Method).NotNull().IsInEnum();
        RuleFor(request => request.Notes).MaximumLength(Payment.NotesMaxLength);
    }
}

public sealed class UpdatePaymentStatusRequestValidator : AbstractValidator<UpdatePaymentStatusRequest>
{
    public UpdatePaymentStatusRequestValidator()
    {
        RuleFor(request => request.Status).NotNull().IsInEnum();
    }
}

public sealed class ListPaymentsQueryValidator : PagedQueryValidator<ListPaymentsQuery>
{
    public ListPaymentsQueryValidator()
    {
        RuleFor(query => query.Search).MaximumLength(PaymentRules.SearchMaxLength);
        RuleFor(query => query.Status).IsInEnum().When(query => query.Status.HasValue);
        RuleFor(query => query.Method).IsInEnum().When(query => query.Method.HasValue);

        RuleFor(query => query.DateTo)
            .Must((query, to) => to >= query.DateFrom)
            .When(query => query.DateFrom.HasValue && query.DateTo.HasValue)
            .WithMessage("The end date must not be earlier than the start date.");
    }
}

public sealed class PayableAppointmentsQueryValidator : AbstractValidator<PayableAppointmentsQuery>
{
    public PayableAppointmentsQueryValidator()
    {
        RuleFor(query => query.Search).MaximumLength(PaymentRules.SearchMaxLength);

        RuleFor(query => query.Limit)
            .InclusiveBetween(1, PaymentRules.MaxPayableLimit);
    }
}

/// <summary>Shared amount rule so both create and update accept the same values.</summary>
internal sealed class PaymentAmountValidator : AbstractValidator<decimal?>
{
    public PaymentAmountValidator()
    {
        RuleFor(amount => amount).Must(amount => amount is > 0m and <= Payment.MaximumAmount)
            .WithMessage($"The amount must be greater than zero and at most {Payment.MaximumAmount:0.##}.");

        RuleFor(amount => amount).Must(amount => amount.HasValue && decimal.Round(amount.Value, 2) == amount.Value)
            .WithMessage("The amount must have at most two decimals.");
    }
}
