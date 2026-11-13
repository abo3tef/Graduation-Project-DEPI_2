using FluentValidation;
using Medilog.Application.Features.MedicalRecords.Contracts;

namespace Medilog.Application.Features.MedicalRecords.Validation;

/// <summary>Validates the shape of an update-medication request.</summary>
public sealed class UpdateMedicationRequestValidator : AbstractValidator<UpdateMedicationRequest>
{
    public UpdateMedicationRequestValidator()
    {
        RuleFor(request => request.MedicationName)
            .NotEmpty()
            .WithMessage("A medication name is required.")
            .MaximumLength(MedicalRecordText.MedicationNameMaxLength);

        RuleFor(request => request.Dosage)
            .NotEmpty()
            .WithMessage("A dosage is required.")
            .MaximumLength(MedicalRecordText.MedicationDosageMaxLength);

        RuleFor(request => request.Frequency)
            .NotEmpty()
            .WithMessage("A frequency is required.")
            .MaximumLength(MedicalRecordText.MedicationFrequencyMaxLength);

        RuleFor(request => request.Duration)
            .MaximumLength(MedicalRecordText.MedicationDurationMaxLength);

        RuleFor(request => request.Instructions)
            .MaximumLength(MedicalRecordText.MedicationInstructionsMaxLength);
    }
}
