namespace Medilog.Domain.Common;

/// <summary>
/// Raised when a domain invariant or an allowed state change is violated.
/// The Application layer is responsible for translating it into a
/// client-readable failure; the message never contains sensitive data.
/// <see cref="Code"/> is a stable, machine-readable identifier (see
/// <see cref="DomainErrorCodes"/>) that clients use to localize the message.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message, string code = DomainErrorCodes.RuleViolation)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>Stable error codes for domain rule violations. Do not rename once published.</summary>
public static class DomainErrorCodes
{
    public const string RuleViolation = "domain.rule_violation";

    public const string MissingReference = "appointment.missing_reference";

    public const string EndTimeRequired = "appointment.end_time_required";

    public const string InvalidInterval = "appointment.invalid_interval";

    public const string InThePast = "appointment.in_the_past";

    public const string NotOpen = "appointment.not_open";

    public const string CancellationReasonRequired = "appointment.cancellation_reason_required";

    public const string CancellationReasonTooLong = "appointment.cancellation_reason_too_long";

    public const string UseCancelOperation = "appointment.use_cancel_operation";

    public const string InvalidStatusTransition = "appointment.invalid_status_transition";

    public const string PaymentMissingReference = "payment.missing_reference";

    public const string PaymentInvalidAmount = "payment.invalid_amount";

    public const string PaymentInvalidMethod = "payment.invalid_method";

    public const string PaymentNotesTooLong = "payment.notes_too_long";

    public const string PaymentInvalidStatusTransition = "payment.invalid_status_transition";

    public const string PaymentPaidFinal = "payment.paid_final";

    public const string PatientOwnerRequired = "patient.owner_required";

    public const string PatientInvalidName = "patient.invalid_name";

    public const string PatientInvalidDateOfBirth = "patient.invalid_date_of_birth";

    public const string PatientInvalidGender = "patient.invalid_gender";

    public const string PatientInvalidPhone = "patient.invalid_phone";

    public const string PatientInvalidAddress = "patient.invalid_address";

    public const string PatientInvalidEmergencyContact = "patient.invalid_emergency_contact";

    public const string PatientInvalidBloodType = "patient.invalid_blood_type";
}
