namespace Medilog.Application.Common.Exceptions;

/// <summary>
/// The requested record does not exist or is outside the caller's data scope.
/// </summary>
/// <remarks>
/// Out-of-scope records deliberately return "not found" instead of "forbidden" so the API does not
/// reveal that a medical record exists (AGENTS.md §5). Mapped to HTTP 404 in the WebAPI layer.
/// </remarks>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }
}
