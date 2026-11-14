using Medilog.Infrastructure.Security;

namespace Medilog.Application.Tests.Auth;

/// <summary>
/// Verifies the PBKDF2 password hasher against the approved format and failure behaviour
/// (decision D-34).
/// </summary>
public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_never_returns_the_password_and_is_self_describing()
    {
        // Arrange
        const string password = "Valid#Passw0rd";

        // Act
        var hash = _hasher.Hash(password);

        // Assert
        Assert.NotEqual(password, hash);
        Assert.DoesNotContain(password, hash, StringComparison.Ordinal);
        Assert.StartsWith("v1$600000$", hash, StringComparison.Ordinal);
        Assert.Equal(4, hash.Split('$').Length);
    }

    [Fact]
    public void Hash_uses_a_fresh_salt_so_equal_passwords_differ()
    {
        // Arrange
        const string password = "Valid#Passw0rd";

        // Act
        var first = _hasher.Hash(password);
        var second = _hasher.Hash(password);

        // Assert
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Verify_accepts_the_correct_password()
    {
        // Arrange
        const string password = "Valid#Passw0rd";
        var hash = _hasher.Hash(password);

        // Act
        var result = _hasher.Verify(password, hash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Verify_rejects_a_wrong_password()
    {
        // Arrange
        var hash = _hasher.Hash("Valid#Passw0rd");

        // Act
        var result = _hasher.Verify("Valid#Passw0rd!", hash);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("v2$600000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("v1$10$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("v1$600000$***$***")]
    public void Verify_rejects_malformed_or_unsupported_hashes_instead_of_throwing(string storedHash)
    {
        // Act
        var result = _hasher.Verify("Valid#Passw0rd", storedHash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Verify_rejects_an_empty_candidate_password()
    {
        // Arrange
        var hash = _hasher.Hash("Valid#Passw0rd");

        // Act / Assert
        Assert.False(_hasher.Verify(string.Empty, hash));
    }
}
