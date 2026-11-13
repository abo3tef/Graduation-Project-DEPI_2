using Medilog.Application.Common;

namespace Medilog.Application.Common.Abstractions;

/// <summary>
/// The identity of the authenticated caller, resolved on the server.
/// </summary>
/// <remarks>
/// AGENTS.md §5: the acting user is never taken from the request body. The implementation lives in
/// the WebAPI layer and reads the authentication claims (decision D-11). Properties are nullable
/// so an unauthenticated request is representable; use cases that require an actor must reject a
/// <c>null</c> value instead of assuming one is present.
/// </remarks>
public interface ICurrentUser
{
    /// <summary>Identifier of the authenticated user, or <c>null</c> when unauthenticated.</summary>
    Guid? UserId { get; }

    /// <summary>Role of the authenticated caller, or <c>null</c> when unauthenticated or unknown.</summary>
    UserRole? Role { get; }
}
