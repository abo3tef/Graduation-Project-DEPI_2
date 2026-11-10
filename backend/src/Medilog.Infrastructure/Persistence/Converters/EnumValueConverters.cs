using Medilog.Domain.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Medilog.Infrastructure.Persistence.Converters;

/// <summary>
/// Converts domain enums to the exact value labels used by the Class
/// Specification. Storing labels instead of numeric values keeps the database
/// readable and makes the allowed values explicit in the generated SQL.
/// </summary>
internal static class EnumValueConverters
{
    public static readonly ValueConverter<AccountStatus, string> AccountStatusConverter = new(
        value => AccountStatusToLabel(value),
        value => AccountStatusFromLabel(value));

    public static readonly ValueConverter<Gender, string> GenderConverter = new(
        value => GenderToLabel(value),
        value => GenderFromLabel(value));

    public static readonly ValueConverter<BloodType, string> BloodTypeConverter = new(
        value => BloodTypeToLabel(value),
        value => BloodTypeFromLabel(value));

    public static readonly ValueConverter<AllergySeverity, string> AllergySeverityConverter = new(
        value => AllergySeverityToLabel(value),
        value => AllergySeverityFromLabel(value));

    public static readonly ValueConverter<ChronicConditionStatus, string> ChronicConditionStatusConverter = new(
        value => ChronicConditionStatusToLabel(value),
        value => ChronicConditionStatusFromLabel(value));

    public static readonly ValueConverter<MedicalDocumentType, string> MedicalDocumentTypeConverter = new(
        value => MedicalDocumentTypeToLabel(value),
        value => MedicalDocumentTypeFromLabel(value));

    public static readonly ValueConverter<AppointmentStatus, string> AppointmentStatusConverter = new(
        value => AppointmentStatusToLabel(value),
        value => AppointmentStatusFromLabel(value));

    private static string AccountStatusToLabel(AccountStatus value) => value switch
    {
        AccountStatus.Active => "Active",
        AccountStatus.Inactive => "Inactive",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown account status.")
    };

    private static AccountStatus AccountStatusFromLabel(string value) => value switch
    {
        "Active" => AccountStatus.Active,
        "Inactive" => AccountStatus.Inactive,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown account status.")
    };

    private static string GenderToLabel(Gender value) => value switch
    {
        Gender.Male => "Male",
        Gender.Female => "Female",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown gender.")
    };

    private static Gender GenderFromLabel(string value) => value switch
    {
        "Male" => Gender.Male,
        "Female" => Gender.Female,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown gender.")
    };

    private static string BloodTypeToLabel(BloodType value) => value switch
    {
        BloodType.APositive => "A+",
        BloodType.ANegative => "A-",
        BloodType.BPositive => "B+",
        BloodType.BNegative => "B-",
        BloodType.ABPositive => "AB+",
        BloodType.ABNegative => "AB-",
        BloodType.OPositive => "O+",
        BloodType.ONegative => "O-",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown blood type.")
    };

    private static BloodType BloodTypeFromLabel(string value) => value switch
    {
        "A+" => BloodType.APositive,
        "A-" => BloodType.ANegative,
        "B+" => BloodType.BPositive,
        "B-" => BloodType.BNegative,
        "AB+" => BloodType.ABPositive,
        "AB-" => BloodType.ABNegative,
        "O+" => BloodType.OPositive,
        "O-" => BloodType.ONegative,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown blood type.")
    };

    private static string AllergySeverityToLabel(AllergySeverity value) => value switch
    {
        AllergySeverity.Mild => "Mild",
        AllergySeverity.Moderate => "Moderate",
        AllergySeverity.Severe => "Severe",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown allergy severity.")
    };

    private static AllergySeverity AllergySeverityFromLabel(string value) => value switch
    {
        "Mild" => AllergySeverity.Mild,
        "Moderate" => AllergySeverity.Moderate,
        "Severe" => AllergySeverity.Severe,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown allergy severity.")
    };

    private static string ChronicConditionStatusToLabel(ChronicConditionStatus value) => value switch
    {
        ChronicConditionStatus.Active => "Active",
        ChronicConditionStatus.Controlled => "Controlled",
        ChronicConditionStatus.Resolved => "Resolved",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown chronic condition status.")
    };

    private static ChronicConditionStatus ChronicConditionStatusFromLabel(string value) => value switch
    {
        "Active" => ChronicConditionStatus.Active,
        "Controlled" => ChronicConditionStatus.Controlled,
        "Resolved" => ChronicConditionStatus.Resolved,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown chronic condition status.")
    };

    private static string MedicalDocumentTypeToLabel(MedicalDocumentType value) => value switch
    {
        MedicalDocumentType.Pdf => "PDF",
        MedicalDocumentType.Jpg => "JPG",
        MedicalDocumentType.Jpeg => "JPEG",
        MedicalDocumentType.Png => "PNG",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown medical document type.")
    };

    private static MedicalDocumentType MedicalDocumentTypeFromLabel(string value) => value switch
    {
        "PDF" => MedicalDocumentType.Pdf,
        "JPG" => MedicalDocumentType.Jpg,
        "JPEG" => MedicalDocumentType.Jpeg,
        "PNG" => MedicalDocumentType.Png,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown medical document type.")
    };

    private static string AppointmentStatusToLabel(AppointmentStatus value) => value switch
    {
        AppointmentStatus.Scheduled => "Scheduled",
        AppointmentStatus.Confirmed => "Confirmed",
        AppointmentStatus.Completed => "Completed",
        AppointmentStatus.Cancelled => "Cancelled",
        AppointmentStatus.NoShow => "No-Show",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown appointment status.")
    };

    private static AppointmentStatus AppointmentStatusFromLabel(string value) => value switch
    {
        "Scheduled" => AppointmentStatus.Scheduled,
        "Confirmed" => AppointmentStatus.Confirmed,
        "Completed" => AppointmentStatus.Completed,
        "Cancelled" => AppointmentStatus.Cancelled,
        "No-Show" => AppointmentStatus.NoShow,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown appointment status.")
    };
}
