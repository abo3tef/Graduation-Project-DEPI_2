namespace Medilog.Application.Common.Exceptions;

/// <summary>
/// The request is valid but conflicts with the current persisted state.
/// </summary>
/// <remarks>Mapped to HTTP 409 in the WebAPI layer (AGENTS.md §4.4).</remarks>
public sealed class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }
}
