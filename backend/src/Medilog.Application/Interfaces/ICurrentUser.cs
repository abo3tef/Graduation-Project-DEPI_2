using Medilog.Application.Common;

namespace Medilog.Application.Interfaces;

/// <summary>
/// The authenticated actor, resolved from the server-side identity. Never built
/// from client-supplied values. Implemented by the authentication work (Q-07).
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }

    UserRole Role { get; }
}
