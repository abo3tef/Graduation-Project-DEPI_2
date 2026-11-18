namespace Medilog.Application.Features.MedicalRecords.Validation;

/// <summary>Temporary Q-19 limits authorized by Anton. Pending Tech Lead Approval.</summary>
public static class VitalSignsLimits
{
    public const int BloodPressureMaxLength = 20;
    public const int SystolicMin = 50;
    public const int SystolicMax = 300;
    public const int DiastolicMin = 20;
    public const int DiastolicMax = 200;
    public const int HeartRateMin = 20;
    public const int HeartRateMax = 300;
    public const decimal TemperatureMin = 30.0m;
    public const decimal TemperatureMax = 45.0m;
    public const int RespiratoryRateMin = 4;
    public const int RespiratoryRateMax = 80;
    public const int SpO2Min = 50;
    public const int SpO2Max = 100;
    public const decimal WeightMin = 0.3m;
    public const decimal WeightMax = 500m;
    public const decimal HeightMin = 20m;
    public const decimal HeightMax = 260m;
    public const string TemperatureUnit = "Celsius";
    public const string WeightUnit = "kg";
    public const string HeightUnit = "cm";
}
