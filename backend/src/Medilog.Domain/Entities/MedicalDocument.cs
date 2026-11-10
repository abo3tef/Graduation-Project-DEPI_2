using Medilog.Domain.Common;
using Medilog.Domain.Enums;

namespace Medilog.Domain.Entities;

/// <summary>
/// A document (PDF or image) uploaded for a patient. The storage location is
/// a path or URL; file-upload handling is outside this persistence foundation.
/// </summary>
public sealed class MedicalDocument : BaseEntity
{
    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    /// <summary>
    /// The doctor who uploaded the document. The Class Specification does not
    /// mark this attribute as required.
    /// </summary>
    public Guid? UploadedBy { get; set; }

    public Doctor? UploadedByDoctor { get; set; }

    public string FileName { get; set; } = string.Empty;

    public MedicalDocumentType Type { get; set; }

    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Upload instant. This is a domain timestamp and is not populated by the
    /// generic audit hook; the upload flow must set it from server UTC time.
    /// </summary>
    public DateTimeOffset UploadDate { get; set; }

    public string? Description { get; set; }
}
