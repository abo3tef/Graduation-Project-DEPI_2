namespace Medilog.Application.Features.Auth;

/// <summary>
/// The plain-text transactional emails sent by the authentication use cases.
/// </summary>
/// <remarks>
/// Emails contain the one-time link and the approved expiry, never a password or a password hash.
/// They are written once, when the token is issued, and are never logged (AGENTS.md §5).
/// </remarks>
internal static class AuthEmailTemplates
{
    public const string VerificationSubject = "Verify your Medilog email address";

    public const string ResetSubject = "Reset your Medilog password";

    public static string VerificationBody(string fullName, string link, TimeSpan lifetime) =>
        $"""
        Hello {fullName},

        Confirm your Medilog email address by opening this link:
        {link}

        The link expires in {Describe(lifetime)} and can be used once.
        If you did not create a Medilog account, ignore this email.
        """;

    public static string ResetBody(string fullName, string link, TimeSpan lifetime) =>
        $"""
        Hello {fullName},

        Choose a new Medilog password by opening this link:
        {link}

        The link expires in {Describe(lifetime)} and can be used once.
        If you did not ask to reset your password, ignore this email; your password stays unchanged.
        """;

    private static string Describe(TimeSpan lifetime) =>
        lifetime.TotalHours >= 1 && lifetime.TotalMinutes % 60 == 0
            ? $"{(int)lifetime.TotalHours} hour(s)"
            : $"{(int)lifetime.TotalMinutes} minute(s)";
}
