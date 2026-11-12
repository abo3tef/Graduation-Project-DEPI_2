namespace Medilog.Application.Common.Exceptions;

/// <summary>
/// Base type for expected Application-level failures. The WebAPI layer maps the
/// derived types to HTTP status codes (F1-C3). Messages are client-readable and
/// never contain sensitive data.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message)
        : base(message)
    {
    }
}

/// <summary>The resource does not exist or is outside the caller's scope (404).</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message)
        : base(message)
    {
    }
}

/// <summary>The caller is authenticated but not allowed to perform the action (403).</summary>
public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message)
        : base(message)
    {
    }
}

/// <summary>The request conflicts with the current state, e.g. an overlap (409).</summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string message)
        : base(message)
    {
    }
}

/// <summary>The request failed input validation (400). Carries per-field errors.</summary>
public sealed class RequestValidationException : AppException
{
    public RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public RequestValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = [error] })
    {
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
