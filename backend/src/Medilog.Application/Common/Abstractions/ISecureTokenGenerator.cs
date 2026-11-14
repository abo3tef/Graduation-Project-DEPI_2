namespace Medilog.Application.Common.Abstractions;

/// <summary>
/// Creates and hashes the single-use tokens sent by email for verification and password reset.
/// </summary>
/// <remarks>
/// Only the hash is stored (decision D-33, AGENTS.md §5). The raw token goes into the emailed
/// link exactly once and is never logged.
/// </remarks>
public interface ISecureTokenGenerator
{
    /// <summary>Returns a fresh cryptographically secure, URL-safe token.</summary>
    string GenerateToken();

    /// <summary>Returns the stable hash used for storage and lookup.</summary>
    string HashToken(string token);
}
