namespace Medilog.Application.Features.MedicalRecords.Validation;

/// <summary>
/// Request-level limits and the small text helpers shared by the medical-record validators and
/// services, so a rule is written once (AGENTS.md §3.2).
/// </summary>
/// <remarks>
/// The database stores these columns as MySQL <c>text</c>, which has no practical per-column limit;
/// the limits below are deliberate request-level guards, recorded as decision D-14.
/// </remarks>
public static class MedicalRecordText
{
    public const int ChiefComplaintMaxLength = 2000;

    public const int SymptomsMaxLength = 2000;

    public const int ClinicalNotesMaxLength = 4000;

    public const int DiagnosisNameMaxLength = 200;

    public const int DiagnosisDescriptionMaxLength = 4000;

    /// <summary>Tolerance applied when rejecting timestamps that lie in the future.</summary>
    public static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Trims a free-text value and turns an empty or whitespace-only value into <c>null</c>, so the
    /// database never stores meaningless whitespace.
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length == 0 ? null : trimmed;
    }
}
