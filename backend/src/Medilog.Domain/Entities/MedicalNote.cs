using Medilog.Domain.Common;

namespace Medilog.Domain.Entities;

/// <summary>
/// A clinical note written by a doctor about a patient or a visit.
/// The inherited <see cref="CreatedEntity.CreatedAtUtc"/> maps to the
/// specification's <c>createdDate</c> and
/// <see cref="AuditableEntity.UpdatedAtUtc"/> maps to <c>updatedDate</c>.
/// </summary>
public sealed class MedicalNote : AuditableEntity
{
    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public Guid DoctorId { get; set; }

    public Doctor Doctor { get; set; } = null!;

    /// <summary>
    /// Optional link to the visit that produced the note.
    /// </summary>
    public Guid? VisitId { get; set; }

    public MedicalVisit? Visit { get; set; }

    public string NoteContent { get; set; } = string.Empty;
}
