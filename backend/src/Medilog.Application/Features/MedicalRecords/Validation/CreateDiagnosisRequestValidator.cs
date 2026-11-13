using FluentValidation;
using Medilog.Application.Features.MedicalRecords.Contracts;

namespace Medilog.Application.Features.MedicalRecords.Validation;

/// <summary>
/// Validates the shape of a create-diagnosis request.
/// </summary>
/// <remarks>
/// The visit and patient identifiers are not part of the request; they come from the route and the
/// persisted visit.
/// </remarks>
public sealed class CreateDiagnosisRequestValidator : AbstractValidator<CreateDiagnosisRequest>
{
    public CreateDiagnosisRequestValidator()
    {
        RuleFor(request => request.DiagnosisName)
            .NotEmpty()
            .WithMessage("A diagnosis name is required.")
            .MaximumLength(MedicalRecordText.DiagnosisNameMaxLength);

        RuleFor(request => request.Description)
            .MaximumLength(MedicalRecordText.DiagnosisDescriptionMaxLength);

        RuleFor(request => request.DiagnosisDate)
            .NotEmpty()
            .WithMessage("A diagnosis date is required.");

        RuleFor(request => request.DiagnosisDate)
            .Must(diagnosisDate => diagnosisDate <= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            .WithMessage("The diagnosis date cannot be in the future.");
    }
}
