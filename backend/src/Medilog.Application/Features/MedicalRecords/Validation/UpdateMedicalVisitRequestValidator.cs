using FluentValidation;
using Medilog.Application.Features.MedicalRecords.Contracts;

namespace Medilog.Application.Features.MedicalRecords.Validation;

/// <summary>
/// Validates the shape of an update-visit request.
/// </summary>
/// <remarks>
/// The request has no patient, doctor or appointment member, so those values cannot be changed
/// through the API (AGENTS.md §3.3).
/// </remarks>
public sealed class UpdateMedicalVisitRequestValidator : AbstractValidator<UpdateMedicalVisitRequest>
{
    public UpdateMedicalVisitRequestValidator()
    {
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
