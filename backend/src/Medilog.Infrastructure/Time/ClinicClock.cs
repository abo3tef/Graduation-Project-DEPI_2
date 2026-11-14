using Medilog.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Medilog.Infrastructure.Time;

/// <summary>
/// Clinic wall-clock time from the <c>Clinic:TimeZone</c> setting. A missing or
/// unknown time zone fails clearly; it never falls back to UTC.
/// </summary>
internal sealed class ClinicClock : IClinicClock
{
    private readonly TimeZoneInfo _timeZone;
    private readonly TimeProvider _timeProvider;

    public ClinicClock(IConfiguration configuration, TimeProvider timeProvider)
    {
        var timeZoneId = configuration["Clinic:TimeZone"];

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            throw new InvalidOperationException("The 'Clinic:TimeZone' setting is not configured.");
        }

        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidOperationException(
                $"The 'Clinic:TimeZone' setting '{timeZoneId}' is not a known time zone.",
                exception);
        }

        _timeProvider = timeProvider;
    }

    public DateTime LocalNow => TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _timeZone).DateTime;

    public TimeZoneInfo TimeZone => _timeZone;

    public bool IsNonExistentLocalTime(DateTime localTime) =>
        _timeZone.IsInvalidTime(DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified));
}
