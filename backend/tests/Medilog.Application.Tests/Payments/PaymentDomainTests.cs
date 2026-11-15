using Medilog.Domain.Common;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Xunit;

namespace Medilog.Application.Tests.Payments;

/// <summary>
/// Domain rules for the single-payment-per-appointment model approved in Q-21.
/// Expected behavior comes from the approved model, not from the implementation:
/// new payments start Pending, Paid is final, and money is stored with two decimals.
/// </summary>
public sealed class PaymentDomainTests
{
    private static readonly Guid Appointment = Guid.NewGuid();
    private static readonly Guid Patient = Guid.NewGuid();
    private static readonly Guid Doctor = Guid.NewGuid();
    private static readonly Guid Recorder = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2030, 1, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_starts_pending_with_no_paid_timestamp()
    {
        var payment = Create();

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(payment.PaidAtUtc);
        Assert.Equal(500.25m, payment.Amount);
        Assert.Equal(PaymentMethod.Card, payment.Method);
        Assert.Equal(Recorder, payment.CreatedBy);
        Assert.Equal(Appointment, payment.AppointmentId);
        Assert.Equal(Patient, payment.PatientId);
        Assert.Equal(Doctor, payment.DoctorId);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Create_rejects_an_empty_reference(string empty)
    {
        var id = Guid.Parse(empty);

        Assert.Equal(
            DomainErrorCodes.PaymentMissingReference,
            Assert.Throws<DomainException>(() => Payment.Create(
                id, Patient, Doctor, Recorder, 10m, PaymentMethod.Cash, null)).Code);
        Assert.Equal(
            DomainErrorCodes.PaymentMissingReference,
            Assert.Throws<DomainException>(() => Payment.Create(
                Appointment, id, Doctor, Recorder, 10m, PaymentMethod.Cash, null)).Code);
        Assert.Equal(
            DomainErrorCodes.PaymentMissingReference,
            Assert.Throws<DomainException>(() => Payment.Create(
                Appointment, Patient, id, Recorder, 10m, PaymentMethod.Cash, null)).Code);
        Assert.Equal(
            DomainErrorCodes.PaymentMissingReference,
            Assert.Throws<DomainException>(() => Payment.Create(
                Appointment, Patient, Doctor, id, 10m, PaymentMethod.Cash, null)).Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1_000_000.01)]
    [InlineData(10.123)]
    public void Create_rejects_an_invalid_amount(double amount)
    {
        Assert.Equal(
            DomainErrorCodes.PaymentInvalidAmount,
            Assert.Throws<DomainException>(() => Payment.Create(
                Appointment, Patient, Doctor, Recorder, (decimal)amount, PaymentMethod.Cash, null)).Code);
    }

    [Fact]
    public void Create_rejects_an_unknown_method()
    {
        Assert.Equal(
            DomainErrorCodes.PaymentInvalidMethod,
            Assert.Throws<DomainException>(() => Payment.Create(
                Appointment, Patient, Doctor, Recorder, 10m, (PaymentMethod)99, null)).Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_normalizes_blank_notes_to_null(string? notes)
    {
        Assert.Null(Create(notes: notes).Notes);
    }

    [Fact]
    public void Create_trims_notes_and_rejects_an_over_long_value()
    {
        Assert.Equal("Follow-up", Create(notes: "  Follow-up  ").Notes);

        Assert.Equal(
            DomainErrorCodes.PaymentNotesTooLong,
            Assert.Throws<DomainException>(() => Create(notes: new string('x', Payment.NotesMaxLength + 1))).Code);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Partial, true)]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Paid, true)]
    [InlineData(PaymentStatus.Partial, PaymentStatus.Paid, true)]
    [InlineData(PaymentStatus.Paid, PaymentStatus.Partial, false)]
    [InlineData(PaymentStatus.Paid, PaymentStatus.Pending, false)]
    [InlineData(PaymentStatus.Partial, PaymentStatus.Pending, false)]
    [InlineData(PaymentStatus.Partial, PaymentStatus.Partial, false)]
    [InlineData(PaymentStatus.Paid, PaymentStatus.Paid, false)]
    public void The_transition_table_matches_the_approved_lifecycle(
        PaymentStatus from,
        PaymentStatus to,
        bool allowed)
    {
        Assert.Equal(allowed, Payment.IsAllowedTransition(from, to));
    }

    [Fact]
    public void Reaching_paid_records_the_paid_timestamp_but_partial_does_not()
    {
        var payment = Create();

        payment.ChangeStatus(PaymentStatus.Partial, Now);
        Assert.Null(payment.PaidAtUtc);

        payment.ChangeStatus(PaymentStatus.Paid, Now.AddMinutes(5));
        Assert.Equal(Now.AddMinutes(5), payment.PaidAtUtc);
    }

    [Fact]
    public void A_paid_payment_is_final()
    {
        var payment = Create();
        payment.ChangeStatus(PaymentStatus.Paid, Now);

        Assert.Equal(
            DomainErrorCodes.PaymentInvalidStatusTransition,
            Assert.Throws<DomainException>(() => payment.ChangeStatus(PaymentStatus.Partial, Now)).Code);

        Assert.Equal(
            DomainErrorCodes.PaymentPaidFinal,
            Assert.Throws<DomainException>(() =>
                payment.UpdateDetails(100m, PaymentMethod.Cash, null)).Code);
    }

    [Fact]
    public void Update_details_changes_only_the_editable_fields()
    {
        var payment = Create();

        payment.UpdateDetails(250.75m, PaymentMethod.Cash, "  corrected  ");

        Assert.Equal(250.75m, payment.Amount);
        Assert.Equal(PaymentMethod.Cash, payment.Method);
        Assert.Equal("corrected", payment.Notes);
        Assert.Equal(PaymentStatus.Pending, payment.Status);

        // Ownership and status can never be changed through the update path.
        Assert.Equal(Appointment, payment.AppointmentId);
        Assert.Equal(Patient, payment.PatientId);
        Assert.Equal(Doctor, payment.DoctorId);
        Assert.Equal(Recorder, payment.CreatedBy);
    }

    private static Payment Create(decimal amount = 500.25m, string? notes = null) =>
        Payment.Create(Appointment, Patient, Doctor, Recorder, amount, PaymentMethod.Card, notes);
}
