using Medilog.Domain.Common;
using Medilog.Domain.Enums;

namespace Medilog.Domain.Entities;

/// <summary>
/// The authenticated doctor who owns a workspace of patients and medical records.
/// </summary>
public sealed class Doctor : CreatedEntity
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Specialization { get; set; }

    public AccountStatus AccountStatus { get; set; }

    /// <summary>
    /// When the email address was confirmed, or <c>null</c> while it is still unverified.
    /// An unverified account cannot sign in (Q-11).
    /// </summary>
    public DateTimeOffset? EmailVerifiedAtUtc { get; set; }

    public ICollection<Patient> Patients { get; set; } = new List<Patient>();

    public ICollection<MedicalVisit> MedicalVisits { get; set; } = new List<MedicalVisit>();

    public ICollection<MedicalNote> MedicalNotes { get; set; } = new List<MedicalNote>();

    public ICollection<MedicalDocument> MedicalDocuments { get; set; } = new List<MedicalDocument>();

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public ICollection<Secretary> Secretaries { get; set; } = new List<Secretary>();
}
