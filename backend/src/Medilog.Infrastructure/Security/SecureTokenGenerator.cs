using System.Security.Cryptography;
using System.Text;
using Medilog.Application.Common.Abstractions;

namespace Medilog.Infrastructure.Security;

/// <summary>
/// Generates 256-bit verification/reset tokens and stores only their SHA-256 hash
/// (decision D-33).
/// </summary>
/// <remarks>
/// The raw token is URL-safe Base64 so it can travel inside an emailed link. The stored hash is
/// lower-case hex with a fixed 64-character length, which is why the column can be indexed
/// uniquely. Hashing with a fast digest is correct here: the token already has 256 bits of
/// entropy, so it is not guessable and does not need a slow password hash.
/// </remarks>
public sealed class SecureTokenGenerator : ISecureTokenGenerator
{
    private const int TokenBytes = 32;

    public string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenBytes))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public string HashToken(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
