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

public sealed class ListPatientNotesRequestValidator : PagedQueryValidator<ListPatientNotesRequest>
{
    public ListPatientNotesRequestValidator()
    {
        RuleFor(request => request.PatientId).NotEmpty();
    }
}

public sealed class ListPatientLabResultsRequestValidator : PagedQueryValidator<ListPatientLabResultsRequest>
{
    public ListPatientLabResultsRequestValidator()
    {
        RuleFor(request => request.PatientId).NotEmpty();
    }
}

public sealed class ListPatientTimelineRequestValidator : PagedQueryValidator<ListPatientTimelineRequest>
{
    public ListPatientTimelineRequestValidator()
    {
        RuleFor(request => request.PatientId).NotEmpty();
    }
}

public sealed class ListPatientDocumentsRequestValidator : PagedQueryValidator<ListPatientDocumentsRequest>
{
    public ListPatientDocumentsRequestValidator()
    {
        RuleFor(request => request.PatientId).NotEmpty();
    }
}
