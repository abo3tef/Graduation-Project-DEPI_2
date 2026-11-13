namespace Medilog.Application.Interfaces;

/// <summary>
/// Clinic wall-clock time, based on the <c>Clinic:TimeZone</c> setting.
/// </summary>
public interface IClinicClock
{
    /// <summary>The current local date and time in the clinic time zone.</summary>
    DateTime LocalNow { get; }

    /// <summary>
    /// True when the local wall-clock time does not exist in the clinic time zone
    /// (the skipped hour when daylight saving time starts).
    /// </summary>
    bool IsNonExistentLocalTime(DateTime localTime);

    /// <summary>The configured clinic time zone.</summary>
    TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
}
