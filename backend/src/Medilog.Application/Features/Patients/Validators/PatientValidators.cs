using FluentValidation;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.Patients.Requests;
using Medilog.Application.Interfaces;
using Medilog.Domain.Entities;

namespace Medilog.Application.Features.Patients.Validators;

// Shape-level rules only (AGENTS.md §3.2). Ownership, scope, the secretary field restriction and
// the delete guard are enforced by the service; the domain re-checks the field invariants.

/// <summary>The field rules shared by create and update so both accept exactly the same values.</summary>
public abstract class PatientFieldsValidator<T> : AbstractValidator<T>
    where T : IPatientFields
{
    protected PatientFieldsValidator(IClinicClock clock)
    {
        RuleFor(request => request.FullName).NotEmpty().MaximumLength(Patient.FullNameMaxLength);

        RuleFor(request => request.DateOfBirth).NotNull();

        RuleFor(request => request.DateOfBirth)
            .Must(value => value >= Patient.EarliestDateOfBirth)
            .WithMessage("The date of birth cannot be before 1900.")
            .Must(value => value <= DateOnly.FromDateTime(clock.LocalNow))
            .WithMessage("The date of birth cannot be in the future.")
            .When(request => request.DateOfBirth.HasValue);

        RuleFor(request => request.Gender).NotNull().IsInEnum();

        RuleFor(request => request.Phone)
            .MaximumLength(Patient.PhoneMaxLength)
            .Must(PatientRules.IsValidPhone)
            .WithMessage("The phone number format is not valid.");

        RuleFor(request => request.Address).MaximumLength(Patient.AddressMaxLength);
        RuleFor(request => request.EmergencyContact).MaximumLength(Patient.EmergencyContactMaxLength);
        RuleFor(request => request.BloodType).IsInEnum().When(request => request.BloodType.HasValue);
    }
}

public sealed class CreatePatientRequestValidator : PatientFieldsValidator<CreatePatientRequest>
{
    public CreatePatientRequestValidator(IClinicClock clock)
        : base(clock)
    {
        RuleFor(request => request.DoctorId).NotEqual(Guid.Empty).When(request => request.DoctorId.HasValue);
    }
}

public sealed class UpdatePatientRequestValidator : PatientFieldsValidator<UpdatePatientRequest>
{
    public UpdatePatientRequestValidator(IClinicClock clock)
        : base(clock)
    {
    }
}

public sealed class ListPatientsQueryValidator : PagedQueryValidator<ListPatientsQuery>
{
    public ListPatientsQueryValidator()
    {
        RuleFor(query => query.Search).MaximumLength(PatientRules.SearchMaxLength);
        RuleFor(query => query.Gender).IsInEnum().When(query => query.Gender.HasValue);
        RuleFor(query => query.DoctorId).NotEqual(Guid.Empty).When(query => query.DoctorId.HasValue);
    }
}

public sealed class PatientPickerQueryValidator : AbstractValidator<PatientPickerQuery>
{
    public PatientPickerQueryValidator()
    {
        RuleFor(query => query.Search).MaximumLength(PatientRules.SearchMaxLength);
        RuleFor(query => query.Limit).InclusiveBetween(1, PatientRules.MaxLookupLimit);
        RuleFor(query => query.DoctorId).NotEqual(Guid.Empty).When(query => query.DoctorId.HasValue);
    }
}
