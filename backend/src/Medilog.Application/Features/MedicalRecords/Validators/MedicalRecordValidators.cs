using FluentValidation;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.MedicalRecords.Requests;

namespace Medilog.Application.Features.MedicalRecords.Validators;

// Shape-level rules only. The doctor/secretary boundary and the ownership scope
// are business rules enforced by the service, not duplicated here. Paging rules
// come from the shared PagedQueryValidator (Q-04).

public sealed class GetMedicalProfileRequestValidator : AbstractValidator<GetMedicalProfileRequest>
{
    public GetMedicalProfileRequestValidator()
    {
        RuleFor(request => request.PatientId).NotEmpty();
    }
}

public sealed class GetMedicalHistoryRequestValidator : PagedQueryValidator<GetMedicalHistoryRequest>
{
    public GetMedicalHistoryRequestValidator()
    {
        RuleFor(request => request.PatientId).NotEmpty();
    }
}

public sealed class GetPatientTimelineRequestValidator
    : PagedQueryValidator<GetPatientTimelineRequest>
{
    private static readonly string[] AllowedEventTypes =
    [
        PatientTimelineEventTypes.Visit,
        PatientTimelineEventTypes.Diagnosis,
        PatientTimelineEventTypes.Medication,
        PatientTimelineEventTypes.MedicalNote
    ];

    public GetPatientTimelineRequestValidator()
    {
        RuleFor(request => request.PatientId)
            .NotEmpty();

        RuleFor(request => request.EventType)
            .Must(type => type is null ||
                AllowedEventTypes.Contains(
                    type,
                    StringComparer.OrdinalIgnoreCase))
            .WithMessage("Invalid timeline event type.");

        RuleFor(request => request.From)
            .LessThanOrEqualTo(request => request.To)
            .When(request => request.From.HasValue &&
                             request.To.HasValue)
            .WithMessage("From must be before or equal to To.");
    }
}
