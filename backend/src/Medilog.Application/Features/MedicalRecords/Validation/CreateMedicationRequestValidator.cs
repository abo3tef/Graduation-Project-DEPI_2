using FluentValidation;
using Medilog.Application.Features.MedicalRecords.Contracts;

namespace Medilog.Application.Features.MedicalRecords.Validation;

/// <summary>
/// Validates the shape of a create-medication request.
/// </summary>
/// <remarks>
/// The visit and patient identifiers are not part of the request; they come from the route and the
/// persisted visit. The limits mirror the persisted column lengths (decision D-26).
/// </remarks>
public sealed class CreateMedicationRequestValidator : AbstractValidator<CreateMedicationRequest>
{
    public CreateMedicationRequestValidator()
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
