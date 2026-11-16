using Medilog.Domain.Common;
using Medilog.Domain.Enums;

namespace Medilog.Domain.Entities;

/// <summary>
/// Money recorded for one appointment (Q-21). Exactly one payment exists per
/// appointment; partial collection is represented by the <see cref="PaymentStatus.Partial"/>
/// status on that single row, not by extra rows.
/// </summary>
/// <remarks>
/// <see cref="Amount"/> is the payment amount in EGP with at most two decimals.
/// Revenue is defined as the sum of <see cref="Amount"/> over rows whose
/// <see cref="Status"/> is <see cref="PaymentStatus.Paid"/> only.
/// <see cref="CreatedBy"/> is the authenticated doctor or secretary; like
/// <c>appointments.created_by</c> it is stored without a database foreign key.
/// </remarks>
public sealed class Payment : CreatedEntity
{
    /// <summary>Maximum length of <see cref="Notes"/> enforced by the domain and the database.</summary>
    public const int NotesMaxLength = 500;

    /// <summary>Upper bound accepted for a payment amount, matching the F2 screen limit.</summary>
    public const decimal MaximumAmount = 1_000_000m;

    public Guid AppointmentId { get; set; }

    public Appointment Appointment { get; set; } = null!;

    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public Guid DoctorId { get; set; }

    public Doctor Doctor { get; set; } = null!;

    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }

    public PaymentStatus Status { get; set; }

    /// <summary>
    /// When the payment reached <see cref="PaymentStatus.Paid"/>; <c>null</c> while
    /// the payment is Pending or Partial. Set by the domain, never by a client.
    /// </summary>
    public DateTimeOffset? PaidAtUtc { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// The authenticated doctor or secretary who recorded the payment. The
    /// Class Specification declares this as a polymorphic reference; the column
    /// therefore stores the identifier without a database-level foreign key.
    /// </summary>
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// Creates a new payment in the <see cref="PaymentStatus.Pending"/> status.
    /// <paramref name="createdBy"/> is resolved by the Application layer from the
    /// authenticated identity, never from client input.
    /// </summary>
    public static Payment Create(
        Guid appointmentId,
        Guid patientId,
        Guid doctorId,
        Guid createdBy,
        decimal amount,
        PaymentMethod method,
        string? notes)
    {
        if (appointmentId == Guid.Empty || patientId == Guid.Empty || doctorId == Guid.Empty)
        {
            throw new DomainException(
                "A payment must reference an appointment, a patient and a doctor.",
                DomainErrorCodes.PaymentMissingReference);
        }

        if (createdBy == Guid.Empty)
        {
            throw new DomainException(
                "The payment recorder must be identified.",
                DomainErrorCodes.PaymentMissingReference);
        }

        return new Payment
        {
            AppointmentId = appointmentId,
            PatientId = patientId,
            DoctorId = doctorId,
            CreatedBy = createdBy,
            Amount = ValidateAmount(amount),
            Method = ValidateMethod(method),
            Status = PaymentStatus.Pending,
            Notes = NormalizeNotes(notes)
        };
    }

    /// <summary>
    /// Updates the editable details of a payment. A <see cref="PaymentStatus.Paid"/>
    /// payment is final and cannot be changed. The appointment, ownership and
    /// status never change here.
    /// </summary>
    public void UpdateDetails(decimal amount, PaymentMethod method, string? notes)
    {
        EnsureNotPaid("updated");

        Amount = ValidateAmount(amount);
        Method = ValidateMethod(method);
        Notes = NormalizeNotes(notes);
    }

    /// <summary>
    /// Applies an allowed transition: Pending → Partial / Paid, Partial → Paid.
    /// Paid is final. Which role may request a transition is decided by the
    /// Application layer; the service reports a disallowed transition as a
    /// conflict (409) using <see cref="IsAllowedTransition"/>.
    /// </summary>
    public void ChangeStatus(PaymentStatus newStatus, DateTimeOffset utcNow)
    {
        if (!IsAllowedTransition(Status, newStatus))
        {
            throw new DomainException(
                $"A payment cannot change from {Status} to {newStatus}.",
                DomainErrorCodes.PaymentInvalidStatusTransition);
        }

        Status = newStatus;

        if (newStatus == PaymentStatus.Paid)
        {
            PaidAtUtc = utcNow;
        }
    }

    /// <summary>Pure transition table shared with the Application layer.</summary>
    public static bool IsAllowedTransition(PaymentStatus from, PaymentStatus to) =>
        (from, to) switch
        {
            (PaymentStatus.Pending, PaymentStatus.Partial) => true,
            (PaymentStatus.Pending, PaymentStatus.Paid) => true,
            (PaymentStatus.Partial, PaymentStatus.Paid) => true,
            _ => false
        };

    private void EnsureNotPaid(string action)
    {
        if (Status == PaymentStatus.Paid)
        {
            throw new DomainException(
                $"A paid payment cannot be {action}.",
                DomainErrorCodes.PaymentPaidFinal);
        }
    }

    private static decimal ValidateAmount(decimal amount)
    {
        if (amount <= 0m || amount > MaximumAmount || decimal.Round(amount, 2) != amount)
        {
            throw new DomainException(
                $"The payment amount must be greater than zero, at most {MaximumAmount:0.##} and have at most two decimals.",
                DomainErrorCodes.PaymentInvalidAmount);
        }

        return amount;
    }

    private static PaymentMethod ValidateMethod(PaymentMethod method)
    {
        if (!Enum.IsDefined(method))
        {
            throw new DomainException(
                "The payment method is not supported.",
                DomainErrorCodes.PaymentInvalidMethod);
        }

        return method;
    }

    private static string? NormalizeNotes(string? notes)
    {
        var trimmed = notes?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > NotesMaxLength)
        {
            throw new DomainException(
                $"The payment notes must not exceed {NotesMaxLength} characters.",
                DomainErrorCodes.PaymentNotesTooLong);
        }

        return trimmed;
    }
}
