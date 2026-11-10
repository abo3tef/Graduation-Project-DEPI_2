using Medilog.Domain.Common;

namespace Medilog.Domain.Entities;

/// <summary>
/// A single consultation and the parent of diagnoses, medications,
/// vital signs, and notes.
/// </summary>
public sealed class MedicalVisit : BaseEntity
{
    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public Guid DoctorId { get; set; }

    public Doctor Doctor { get; set; } = null!;

    /// <summary>
    /// Optional link to the appointment that produced this visit.
    /// </summary>
    public Guid? AppointmentId { get; set; }

    public Appointment? Appointment { get; set; }

    /// <summary>
    /// The consultation instant. Persisted as a normalized UTC value.
    /// </summary>
    public DateTimeOffset VisitDate { get; set; }

    public string? ChiefComplaint { get; set; }

    public string? Symptoms { get; set; }

    public string? ClinicalNotes { get; set; }

    public ICollection<Diagnosis> Diagnoses { get; set; } = new List<Diagnosis>();

    public ICollection<Medication> Medications { get; set; } = new List<Medication>();

    public ICollection<VitalSigns> VitalSigns { get; set; } = new List<VitalSigns>();

    public ICollection<MedicalNote> MedicalNotes { get; set; } = new List<MedicalNote>();

    public ICollection<LaboratoryResult> LaboratoryResults { get; set; } = new List<LaboratoryResult>();
}
