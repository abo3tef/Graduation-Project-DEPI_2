namespace Medilog.Application.Common.Exceptions;

/// <summary>
/// The caller is authenticated but is not allowed to perform this operation on this record.
/// </summary>
/// <remarks>Mapped to HTTP 403 in the WebAPI layer (AGENTS.md §5).</remarks>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message)
        : base(message)
    {
    }
}
