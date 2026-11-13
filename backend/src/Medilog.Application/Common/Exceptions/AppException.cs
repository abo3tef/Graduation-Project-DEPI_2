namespace Medilog.Application.Common.Exceptions;

/// <summary>
/// Base type for expected Application-level failures. The WebAPI layer maps the
/// derived types to HTTP status codes. Messages are client-readable English text
/// for logs and developers; <see cref="Code"/> is the stable, machine-readable
/// identifier (see <see cref="ErrorCodes"/>) that clients localize.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message, string code)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>The resource does not exist or is outside the caller's scope (404).</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message, string? code = null)
        : base(message, code ?? ErrorCodes.NotFound)
    {
    }
}

/// <summary>The caller is authenticated but not allowed to perform the action (403).</summary>
public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message, string? code = null)
        : base(message, code ?? ErrorCodes.Forbidden)
    {
    }
}

/// <summary>The request conflicts with the current state, e.g. an overlap (409).</summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string message, string? code = null)
        : base(message, code ?? ErrorCodes.Conflict)
    {
    }
}

/// <summary>
/// The request failed input validation (400). Carries per-field messages and, when
/// available, per-field machine-readable validator codes for localization.
/// </summary>
public sealed class RequestValidationException : AppException
{
    public RequestValidationException(
        IReadOnlyDictionary<string, string[]> errors,
        IReadOnlyDictionary<string, string[]>? errorCodes = null)
        : base("One or more validation errors occurred.", ErrorCodes.ValidationFailed)
    {
        Errors = errors;
        FieldErrorCodes = errorCodes ?? new Dictionary<string, string[]>();
    }

    public RequestValidationException(string field, string error, string errorCode)
        : this(
            new Dictionary<string, string[]> { [field] = [error] },
            new Dictionary<string, string[]> { [field] = [errorCode] })
    {
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>Per-field validator codes, parallel to <see cref="Errors"/>.</summary>
    public IReadOnlyDictionary<string, string[]> FieldErrorCodes { get; }
}
