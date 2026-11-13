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

    /// <summary>Framework model binding failed (malformed JSON, wrong types, missing body).</summary>
    public const string RequestInvalid = "request.invalid";

    public const string Unauthorized = "auth.unauthorized";

    public const string MethodNotAllowed = "request.method_not_allowed";

    public const string UnsupportedMediaType = "request.unsupported_media_type";

    public const string AppointmentMismatch = "appointment.mismatch";

    public const string ServerError = "server.error";

    /// <summary>Generic code used when a not-found failure has no domain-specific code.</summary>
    public const string NotFound = "resource.not_found";

    /// <summary>Generic code used when a forbidden failure has no domain-specific code.</summary>
    public const string Forbidden = "access.forbidden";

    /// <summary>Generic code used when a conflict has no domain-specific code.</summary>
    public const string Conflict = "resource.conflict";
}
