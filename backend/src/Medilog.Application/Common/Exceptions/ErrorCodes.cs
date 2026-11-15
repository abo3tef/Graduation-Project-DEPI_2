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

    public const string MedicalRecordAccessDenied = "medical.access_denied";

    public const string DoctorUnavailable = "doctor.unavailable";

    public const string AppointmentOverlap = "appointment.overlap";

    public const string ScheduleBusy = "schedule.busy";

    public const string CompleteRequiresDoctor = "appointment.complete_requires_doctor";

    public const string TimeNotInTimeZone = "appointment.time_not_in_time_zone";

    public const string ValidationFailed = "validation.failed";

    /// <summary>Framework model binding failed (malformed JSON, wrong types, missing body).</summary>
    public const string RequestInvalid = "request.invalid";

    public const string Unauthorized = "auth.unauthorized";

    /// <summary>Login failed: the email is unknown or the password is wrong (identical answer).</summary>
    public const string AuthInvalidCredentials = "auth.invalid_credentials";

    /// <summary>The account exists but its email address is not verified yet (403, Q-11).</summary>
    public const string AuthEmailUnverified = "auth.email_unverified";

    /// <summary>The account was deactivated by an administrator (403, Q-11).</summary>
    public const string AuthAccountInactive = "auth.account_inactive";

    /// <summary>Registration was refused because the email already has an account (409).</summary>
    public const string AuthEmailAlreadyRegistered = "auth.email_already_registered";

    /// <summary>A verification or reset token is unknown, already used or expired (401).</summary>
    public const string AuthTokenInvalid = "auth.token_invalid";

    /// <summary>The Firebase ID token could not be validated (401, Q-12).</summary>
    public const string AuthInvalidGoogleToken = "auth.invalid_google_token";

    /// <summary>Google Sign-In was attempted while Firebase is not configured (503, Q-12).</summary>
    public const string AuthGoogleNotConfigured = "auth.google_not_configured";

    /// <summary>Authentication cannot run because required configuration or secrets are missing (503).</summary>
    public const string AuthNotConfigured = "auth.not_configured";


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
