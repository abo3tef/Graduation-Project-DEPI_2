namespace Medilog.Application.Common.Abstractions;

/// <summary>
/// Hashes and verifies account passwords.
/// </summary>
/// <remarks>
/// Implemented in Infrastructure with PBKDF2-HMAC-SHA256 (decision D-34). The application layer
/// only ever sees the stored hash string; the raw password is never persisted, logged or returned
/// (AGENTS.md §5).
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>Returns a self-describing hash string that includes the algorithm parameters.</summary>
    string Hash(string password);

    /// <summary>Verifies a candidate password against a stored hash in constant time.</summary>
    bool Verify(string password, string passwordHash);
}
