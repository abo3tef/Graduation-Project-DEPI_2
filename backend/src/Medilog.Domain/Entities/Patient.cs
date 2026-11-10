using Medilog.Domain.Common;
using Medilog.Domain.Enums;

namespace Medilog.Domain.Entities;

/// <summary>
/// The central medical-record entity. Patients do not have accounts or credentials.
/// </summary>
public sealed class Patient : CreatedEntity
{
    /// <summary>
    /// The owning doctor. The Class Specification table does not mark this
    /// attribute as required, although BRULE-03 describes the patient record
    /// as belonging to the creating doctor's workspace. It is therefore
    /// nullable at the database level; ownership must be enforced by the
    /// application flow when a patient is created.
    /// </summary>
    public Guid? DoctorId { get; set; }

    public Doctor? Doctor { get; set; }

    public string FullName { get; set; } = string.Empty;

    public DateOnly DateOfBirth { get; set; }

    public Gender Gender { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public BloodType? BloodType { get; set; }

    public string? EmergencyContact { get; set; }

    public ICollection<MedicalVisit> MedicalVisits { get; set; } = new List<MedicalVisit>();

    public ICollection<Diagnosis> Diagnoses { get; set; } = new List<Diagnosis>();

    public ICollection<Medication> Medications { get; set; } = new List<Medication>();

    public ICollection<Allergy> Allergies { get; set; } = new List<Allergy>();

    public ICollection<ChronicCondition> ChronicConditions { get; set; } = new List<ChronicCondition>();

    public ICollection<LaboratoryResult> LaboratoryResults { get; set; } = new List<LaboratoryResult>();

    public ICollection<MedicalNote> MedicalNotes { get; set; } = new List<MedicalNote>();

    public ICollection<MedicalDocument> MedicalDocuments { get; set; } = new List<MedicalDocument>();

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
