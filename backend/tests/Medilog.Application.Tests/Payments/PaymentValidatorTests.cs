using FluentValidation.TestHelper;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.Payments;
using Medilog.Application.Features.Payments.Requests;
using Medilog.Application.Features.Payments.Validators;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Xunit;

namespace Medilog.Application.Tests.Payments;

/// <summary>
/// Shape-level rules for payment requests. Business invariants (scope, duplicates,
/// transitions) are deliberately not duplicated here; they are enforced by the
/// service and the domain.
/// </summary>
public sealed class PaymentValidatorTests
{
    private readonly CreatePaymentRequestValidator _create = new();
    private readonly UpdatePaymentRequestValidator _update = new();
    private readonly UpdatePaymentStatusRequestValidator _status = new();
    private readonly ListPaymentsQueryValidator _list = new();
    private readonly PayableAppointmentsQueryValidator _payable = new();

    [Fact]
    public void A_valid_create_request_passes()
    {
        _create.TestValidate(ValidCreate()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Create_requires_an_appointment()
    {
        _create
            .TestValidate(ValidCreate() with { AppointmentId = Guid.Empty })
            .ShouldHaveValidationErrorFor(request => request.AppointmentId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    [InlineData(1_000_000.01)]
    public void Create_rejects_a_missing_or_out_of_range_amount(double? amount)
    {
        _create
            .TestValidate(ValidCreate() with { Amount = (decimal?)amount })
            .ShouldHaveValidationErrorFor(request => request.Amount);
    }

    [Theory]
    [InlineData(0.001)]
    [InlineData(10.123)]
    public void Create_rejects_more_than_two_decimals(double amount)
    {
        _create
            .TestValidate(ValidCreate() with { Amount = (decimal)amount })
            .ShouldHaveValidationErrorFor(request => request.Amount);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(500.0)]
    [InlineData(1_000_000.0)]
    public void Create_accepts_the_amount_range_inclusive(double amount)
    {
        _create
            .TestValidate(ValidCreate() with { Amount = (decimal)amount })
            .ShouldNotHaveValidationErrorFor(request => request.Amount);
    }

    [Fact]
    public void Create_requires_a_supported_method()
    {
        _create
            .TestValidate(ValidCreate() with { Method = null })
            .ShouldHaveValidationErrorFor(request => request.Method);

        _create
            .TestValidate(ValidCreate() with { Method = (PaymentMethod)99 })
            .ShouldHaveValidationErrorFor(request => request.Method);
    }

    [Fact]
    public void Notes_are_bounded_by_the_persisted_column_length()
    {
        _create
            .TestValidate(ValidCreate() with { Notes = new string('x', Payment.NotesMaxLength) })
            .ShouldNotHaveValidationErrorFor(request => request.Notes);

        _create
            .TestValidate(ValidCreate() with { Notes = new string('x', Payment.NotesMaxLength + 1) })
            .ShouldHaveValidationErrorFor(request => request.Notes);
    }

    [Fact]
    public void Update_applies_the_same_amount_and_method_rules()
    {
        _update.TestValidate(new UpdatePaymentRequest(10m, PaymentMethod.Cash, null))
            .ShouldNotHaveAnyValidationErrors();

        _update.TestValidate(new UpdatePaymentRequest(null, PaymentMethod.Cash, null))
            .ShouldHaveValidationErrorFor(request => request.Amount);

        _update.TestValidate(new UpdatePaymentRequest(10m, null, null))
            .ShouldHaveValidationErrorFor(request => request.Method);
    }

    [Fact]
    public void Status_update_requires_a_defined_status()
    {
        _status.TestValidate(new UpdatePaymentStatusRequest(PaymentStatus.Paid))
            .ShouldNotHaveAnyValidationErrors();

        _status.TestValidate(new UpdatePaymentStatusRequest(null))
            .ShouldHaveValidationErrorFor(request => request.Status);

        _status.TestValidate(new UpdatePaymentStatusRequest((PaymentStatus)99))
            .ShouldHaveValidationErrorFor(request => request.Status);
    }

    [Fact]
    public void List_paging_uses_the_shared_limits()
    {
        _list.TestValidate(new ListPaymentsQuery()).ShouldNotHaveAnyValidationErrors();

        _list.TestValidate(new ListPaymentsQuery(PageNumber: PagingDefaults.MinPageNumber - 1))
            .ShouldHaveValidationErrorFor(query => query.PageNumber);

        _list.TestValidate(new ListPaymentsQuery(PageSize: 0))
            .ShouldHaveValidationErrorFor(query => query.PageSize);

        _list.TestValidate(new ListPaymentsQuery(PageSize: PagingDefaults.MaxPageSize))
            .ShouldNotHaveValidationErrorFor(query => query.PageSize);

        _list.TestValidate(new ListPaymentsQuery(PageSize: PagingDefaults.MaxPageSize + 1))
            .ShouldHaveValidationErrorFor(query => query.PageSize);
    }

    [Fact]
    public void List_rejects_an_over_long_search_and_an_inverted_date_range()
    {
        _list.TestValidate(new ListPaymentsQuery(Search: new string('x', PaymentRules.SearchMaxLength + 1)))
            .ShouldHaveValidationErrorFor(query => query.Search);

        _list.TestValidate(new ListPaymentsQuery(
                DateFrom: new DateOnly(2030, 1, 10),
                DateTo: new DateOnly(2030, 1, 9)))
            .ShouldHaveValidationErrorFor(query => query.DateTo);

        _list.TestValidate(new ListPaymentsQuery(
                DateFrom: new DateOnly(2030, 1, 9),
                DateTo: new DateOnly(2030, 1, 9)))
            .ShouldNotHaveValidationErrorFor(query => query.DateTo);
    }

    [Fact]
    public void Payable_limit_is_positive_and_bounded()
    {
        _payable.TestValidate(new PayableAppointmentsQuery()).ShouldNotHaveAnyValidationErrors();

        _payable.TestValidate(new PayableAppointmentsQuery(Limit: 0))
            .ShouldHaveValidationErrorFor(query => query.Limit);

        _payable.TestValidate(new PayableAppointmentsQuery(Limit: PaymentRules.MaxPayableLimit))
            .ShouldNotHaveValidationErrorFor(query => query.Limit);

        _payable.TestValidate(new PayableAppointmentsQuery(Limit: PaymentRules.MaxPayableLimit + 1))
            .ShouldHaveValidationErrorFor(query => query.Limit);

        _payable.TestValidate(new PayableAppointmentsQuery(Search: new string('x', PaymentRules.SearchMaxLength + 1)))
            .ShouldHaveValidationErrorFor(query => query.Search);
    }

    private static CreatePaymentRequest ValidCreate() => new(
        Guid.NewGuid(),
        150.50m,
        PaymentMethod.Cash,
        "Initial visit");
}
