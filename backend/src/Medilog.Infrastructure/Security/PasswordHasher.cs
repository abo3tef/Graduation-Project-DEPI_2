using System.Globalization;
using System.Security.Cryptography;
using Medilog.Application.Common.Abstractions;

namespace Medilog.Infrastructure.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hasher (decision D-34).
/// </summary>
/// <remarks>
/// <para>
/// The stored value is self-describing, <c>v1$iterations$salt$hash</c>, so the work factor can be
/// raised later without breaking existing accounts. Salt and derived key are Base64; the
/// separator (<c>$</c>) cannot occur in Base64, so parsing is unambiguous.
/// </para>
/// <para>
/// Verification uses <see cref="CryptographicOperations.FixedTimeEquals"/> to avoid leaking
/// timing information. A malformed or unknown-version hash is treated as a failed match instead
/// of throwing, so a corrupted row can never become a 500 response.
/// </para>
/// </remarks>
public sealed class PasswordHasher : IPasswordHasher
{
    /// <summary>Algorithm description for documentation and tests.</summary>
    public const string Algorithm = "PBKDF2-HMAC-SHA256";

    /// <summary>Iteration count approved for the demo (decision D-34).</summary>
    public const int Iterations = 600_000;

    private const string VersionLabel = "v1";
    private const int SaltBytes = 16;
    private const int KeyBytes = 32;
    private const char Separator = '$';

    // Bounds for a hash read back from storage. They stop a corrupted or hostile row from
    // turning sign-in into an unbounded CPU operation.
    private const int MinimumIterations = 100_000;
    private const int MaximumIterations = 2_000_000;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeyBytes);

        var saltText = Convert.ToBase64String(salt);
        var keyText = Convert.ToBase64String(key);

        return $"{VersionLabel}{Separator}{Iterations.ToString(CultureInfo.InvariantCulture)}{Separator}{saltText}{Separator}{keyText}";
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash))
        {
            return false;
        }

        var parts = passwordHash.Split(Separator);

        if (parts.Length != 4 || !string.Equals(parts[0], VersionLabel, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) ||
            iterations < MinimumIterations ||
            iterations > MaximumIterations)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedKey;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedKey = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length == 0 || expectedKey.Length == 0)
        {
            return false;
        }

        var actualKey = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedKey.Length);

        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }
}
