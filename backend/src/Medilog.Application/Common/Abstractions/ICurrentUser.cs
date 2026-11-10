namespace Medilog.Application.Common.Abstractions;

/// <summary>
/// The identity of the authenticated caller, resolved on the server.
/// </summary>
/// <remarks>
/// AGENTS.md §5: the acting user is never taken from the request body. The implementation lives in
/// the WebAPI layer and reads the authentication claims (decision D-11). Until authentication is
/// answered (Q-07), no implementation is registered and the medical endpoints cannot be built.
/// </remarks>
public interface ICurrentUser
{
    /// <summary>Identifier of the authenticated doctor, or <c>null</c> when unauthenticated.</summary>
    Guid? UserId { get; }

    /// <summary>Role of the authenticated caller, or <c>null</c> when unauthenticated.</summary>
    string? Role { get; }
}
