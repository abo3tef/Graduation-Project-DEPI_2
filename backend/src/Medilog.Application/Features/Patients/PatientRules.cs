using System.Text.RegularExpressions;

namespace Medilog.Application.Features.Patients;

/// <summary>Request-level limits for the patient use cases.</summary>
public static partial class PatientRules
{
    public const int SearchMaxLength = 100;

    public const int DefaultLookupLimit = 10;

    public const int MaxLookupLimit = 20;

    /// <summary>
    /// Optional leading <c>+</c>, then digits, spaces, dashes or parentheses; 7-20 characters and at
    /// least 7 digits. Intentionally permissive: no country format has been approved.
    /// </summary>
    [GeneratedRegex(@"^\+?[0-9 ()\-]{7,20}$", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();

    public static bool IsValidPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return true;
        }

        var trimmed = phone.Trim();

        return PhonePattern().IsMatch(trimmed) && trimmed.Count(char.IsDigit) >= 7;
    }
}
