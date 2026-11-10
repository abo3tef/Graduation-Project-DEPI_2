namespace Medilog.Infrastructure.Persistence.Configurations;

/// <summary>
/// Shared string-length limits so that a column's maximum length is defined
/// once and stays consistent across entities.
/// </summary>
internal static class ColumnLengths
{
    public const int Name = 200;
    public const int Email = 320;
    public const int PasswordHash = 255;
    public const int Phone = 30;
    public const int Specialization = 150;
    public const int Address = 500;
    public const int EmergencyContact = 100;
    public const int FileName = 260;
    public const int Location = 2048;
    public const int EnumLabel = 20;
    public const int ShortLabel = 100;
    public const int Unit = 50;
    public const int ReferenceRange = 100;
    public const int BloodPressure = 20;
}
