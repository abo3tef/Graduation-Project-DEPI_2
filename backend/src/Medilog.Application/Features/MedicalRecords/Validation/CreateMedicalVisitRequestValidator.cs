using FluentValidation;
using Medilog.Application.Features.MedicalRecords.Contracts;

namespace Medilog.Application.Features.MedicalRecords.Validation;

/// <summary>
/// Validates the shape of a create-visit request.
/// </summary>
/// <remarks>
/// Only input shape is checked here. Existence, ownership and appointment consistency are state
/// rules enforced by <c>MedicalVisitService</c> (AGENTS.md §3.2).
/// </remarks>
public sealed class CreateMedicalVisitRequestValidator : AbstractValidator<CreateMedicalVisitRequest>
{
    public CreateMedicalVisitRequestValidator()
    {
        RuleFor(request => request.PatientId)
            .NotEmpty()
            .WithMessage("A patient identifier is required.");

        RuleFor(request => request.AppointmentId)
            .Must(appointmentId => appointmentId != Guid.Empty)
            .When(request => request.AppointmentId.HasValue)
            .WithMessage("The appointment identifier must not be empty.");

        RuleFor(request => request.VisitDate)
            .NotEmpty()
            .WithMessage("A visit date is required.");

        RuleFor(request => request.VisitDate)
            .Must(visitDate => visitDate <= DateTimeOffset.UtcNow.Add(MedicalRecordText.FutureTolerance))
            .WithMessage("The visit date cannot be in the future.");

        RuleFor(request => request.ChiefComplaint)
            .MaximumLength(MedicalRecordText.ChiefComplaintMaxLength);

        RuleFor(request => request.Symptoms)
            .MaximumLength(MedicalRecordText.SymptomsMaxLength);

        RuleFor(request => request.ClinicalNotes)
            .MaximumLength(MedicalRecordText.ClinicalNotesMaxLength);
    }
}
