namespace Medilog.Application.Features.Auth;

/// <summary>
/// The approved registration/reset password policy (F6-C1 plan, item 1).
/// </summary>
/// <remarks>
/// Length and character-class rules are request-shape constraints and live in the validators
/// (AGENTS.md §3.2). They are applied when a password is <em>chosen</em> (register / reset), not on
/// sign-in, so an existing account is never locked out by a policy change.
/// </remarks>
public static class PasswordPolicy
{
    public const int MinimumLength = 8;

    public const int MaximumLength = 128;

    public const string RequirementMessage =
        "The password must be 8-128 characters and contain an upper-case letter, a lower-case letter, a digit and a symbol.";

    /// <summary>True when the password satisfies the length and character-class rules.</summary>
    public static bool IsSatisfiedBy(string? password)
    {
        if (password is null ||
            password.Length < MinimumLength ||
            password.Length > MaximumLength)
        {
            return false;
        }

        var hasUpper = false;
        var hasLower = false;
        var hasDigit = false;
        var hasSymbol = false;

        foreach (var character in password)
        {
            if (char.IsUpper(character))
            {
                hasUpper = true;
            }
            else if (char.IsLower(character))
            {
                hasLower = true;
            }
            else if (char.IsDigit(character))
            {
                hasDigit = true;
            }
            else if (!char.IsWhiteSpace(character))
            {
                hasSymbol = true;
            }
        }

        return hasUpper && hasLower && hasDigit && hasSymbol;
    }
}
