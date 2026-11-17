using Medilog.Domain.Common;
using Medilog.Domain.Enums;

namespace Medilog.Domain.Entities;

/// <summary>
/// The central medical-record entity. Patients do not have accounts or credentials.
/// </summary>
public sealed class Patient : CreatedEntity
{
    public const int FullNameMaxLength = 200;

    public const int PhoneMaxLength = 30;

    public const int AddressMaxLength = 500;

    public const int EmergencyContactMaxLength = 100;

    /// <summary>Earliest date of birth the platform accepts (sanity bound, not a medical rule).</summary>
    public static readonly DateOnly EarliestDateOfBirth = new(1900, 1, 1);

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

    /// <summary>
    /// Creates a patient in the given doctor's workspace (BRULE-03). The owner is supplied by the
    /// application flow from the authenticated identity, never from client input.
    /// </summary>
    public static Patient Create(
        Guid doctorId,
        string fullName,
        DateOnly dateOfBirth,
        Gender gender,
        string? phone,
        string? address,
        string? emergencyContact,
        BloodType? bloodType,
        DateOnly today)
    {
        if (doctorId == Guid.Empty)
        {
            throw new DomainException(
                "A patient must belong to a doctor.",
                DomainErrorCodes.PatientOwnerRequired);
        }

        var patient = new Patient { DoctorId = doctorId, BloodType = bloodType };

        patient.UpdateAdministrativeData(fullName, dateOfBirth, gender, phone, address, emergencyContact, today);

        return patient;
    }

    /// <summary>
    /// Replaces the administrative fields (FR-49). Ownership, blood type and every medical
    /// property are untouched, so a secretary-level update can never reach them.
    /// </summary>
    public void UpdateAdministrativeData(
        string fullName,
        DateOnly dateOfBirth,
        Gender gender,
        string? phone,
        string? address,
        string? emergencyContact,
        DateOnly today)
    {
        var name = fullName?.Trim();

        if (string.IsNullOrEmpty(name) || name.Length > FullNameMaxLength)
        {
            throw new DomainException(
                $"The patient name is required and at most {FullNameMaxLength} characters.",
                DomainErrorCodes.PatientInvalidName);
        }

        if (dateOfBirth > today || dateOfBirth < EarliestDateOfBirth)
        {
            throw new DomainException(
                "The date of birth cannot be in the future or before 1900.",
                DomainErrorCodes.PatientInvalidDateOfBirth);
        }

        if (!Enum.IsDefined(gender))
        {
            throw new DomainException("The gender is not valid.", DomainErrorCodes.PatientInvalidGender);
        }

        FullName = name;
        DateOfBirth = dateOfBirth;
        Gender = gender;
        Phone = Normalize(phone, PhoneMaxLength, DomainErrorCodes.PatientInvalidPhone);
        Address = Normalize(address, AddressMaxLength, DomainErrorCodes.PatientInvalidAddress);
        EmergencyContact = Normalize(
            emergencyContact,
            EmergencyContactMaxLength,
            DomainErrorCodes.PatientInvalidEmergencyContact);
    }

    /// <summary>Doctor-only medical field; kept apart from the administrative update.</summary>
    public void ChangeBloodType(BloodType? bloodType)
    {
        if (bloodType.HasValue && !Enum.IsDefined(bloodType.Value))
        {
            throw new DomainException("The blood type is not valid.", DomainErrorCodes.PatientInvalidBloodType);
        }

        BloodType = bloodType;
    }

    private static string? Normalize(string? value, int maxLength, string code)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > maxLength
            ? throw new DomainException($"The value is longer than {maxLength} characters.", code)
            : trimmed;
    }
}
