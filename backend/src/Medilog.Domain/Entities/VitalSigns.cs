using Medilog.Domain.Common;

namespace Medilog.Domain.Entities;

/// <summary>
/// Measurements taken during a medical visit. The Class Specification does
/// not mark any measurement as required, so all measurements are nullable.
/// </summary>
public sealed class VitalSigns : BaseEntity
{
    public Guid VisitId { get; set; }

    public MedicalVisit Visit { get; set; } = null!;

    public string? BloodPressure { get; set; }

    public int? HeartRate { get; set; }

    public decimal? Temperature { get; set; }

    public int? RespiratoryRate { get; set; }

    public decimal? Weight { get; set; }

    public decimal? Height { get; set; }

    public int? SpO2 { get; set; }
}
