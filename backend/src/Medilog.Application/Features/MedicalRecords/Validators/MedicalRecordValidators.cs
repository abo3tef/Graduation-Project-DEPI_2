using FluentValidation;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.MedicalRecords.Requests;

namespace Medilog.Application.Features.MedicalRecords.Validators;

// Shape-level rules only. The doctor/secretary boundary and the ownership scope
// are business rules enforced by the service, not duplicated here.

public sealed class GetMedicalProfileRequestValidator : AbstractValidator<GetMedicalProfileRequest>
{
    public GetMedicalProfileRequestValidator()
    {
        RuleFor(request => request.PatientId).NotEmpty();
    }
}

public sealed class GetMedicalHistoryRequestValidator : AbstractValidator<GetMedicalHistoryRequest>
{
    public GetMedicalHistoryRequestValidator()
    {
        RuleFor(request => request.PatientId).NotEmpty();

        RuleFor(request => request.PageNumber).GreaterThanOrEqualTo(1);

        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, PagingDefaults.MaxPageSize);
    }
}
