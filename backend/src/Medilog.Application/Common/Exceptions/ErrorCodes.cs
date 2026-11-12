namespace Medilog.Application.Common.Exceptions;

/// <summary>
/// Stable, machine-readable error codes returned in the <c>code</c> field of
/// problem responses so clients can localize messages. Do not rename once
/// published; add new codes instead. Domain rule codes live in
/// <c>DomainErrorCodes</c>.
/// </summary>
public static class ErrorCodes
{
    public const string IdentityInvalid = "identity.invalid";

    public const string DoctorNotFound = "doctor.not_found";

    public const string PatientNotFound = "patient.not_found";

    public const string AppointmentNotFound = "appointment.not_found";

    public const string DoctorUnavailable = "doctor.unavailable";

    public const string AppointmentOverlap = "appointment.overlap";

    public const string ScheduleBusy = "schedule.busy";

    public const string CompleteRequiresDoctor = "appointment.complete_requires_doctor";

    public const string TimeNotInTimeZone = "appointment.time_not_in_time_zone";

    public const string ValidationFailed = "validation.failed";

    public const string ServerError = "server.error";
}
