namespace Medilog.Domain.Common;

/// <summary>
/// Raised when a domain invariant or an allowed state change is violated.
/// The Application layer is responsible for translating it into a
/// client-readable failure; the message never contains sensitive data.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }
}
