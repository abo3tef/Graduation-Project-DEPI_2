using FluentValidation;
using Medilog.Application.Common;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.Admin.Requests;
using Medilog.Domain.Enums;

namespace Medilog.Application.Features.Admin.Validators;

// Shape-level rules only (AGENTS.md §3.2). Self protection, last-Admin protection, role-transition
// conflicts and assignment existence are business/data checks enforced by AdminService.

/// <summary>Validates filters and the shared paging shape of the user list.</summary>
public sealed class ListAdminUsersQueryValidator : PagedQueryValidator<ListAdminUsersQuery>
{
    public ListAdminUsersQueryValidator()
    {
        RuleFor(query => query.Role)
            .Must(value => AdminRules.TryParseRoleFilter(value, out _))
            .WithMessage("The role filter must be Admin, Doctor or Secretary.")
            .When(query => !string.IsNullOrWhiteSpace(query.Role));

        RuleFor(query => query.Status)
            .Must(value => AdminRules.TryParseStatusFilter(value, out _))
            .WithMessage("The status filter must be Active or Inactive.")
            .When(query => !string.IsNullOrWhiteSpace(query.Status));

        RuleFor(query => query.Search).MaximumLength(AdminRules.SearchMaxLength);
    }
}

/// <summary>A status request must name one of the two approved account states.</summary>
public sealed class SetAdminUserStatusRequestValidator : AbstractValidator<SetAdminUserStatusRequest>
{
    public SetAdminUserStatusRequestValidator()
    {
        RuleFor(request => request.Status).IsInEnum();
    }
}

/// <summary>Only Doctor↔Secretary is assignable (Q-22); Admin is never created or changed here.</summary>
public sealed class ChangeAdminUserRoleRequestValidator : AbstractValidator<ChangeAdminUserRoleRequest>
{
    public ChangeAdminUserRoleRequestValidator()
    {
        RuleFor(request => request.Role)
            .Must(role => role is UserRole.Doctor or UserRole.Secretary)
            .WithMessage("Only Doctor and Secretary roles can be assigned.");
    }
}

/// <summary>Bounded, duplicate-free doctor id list for one secretary (Q-16).</summary>
public sealed class UpdateSecretaryDoctorsRequestValidator : AbstractValidator<UpdateSecretaryDoctorsRequest>
{
    public UpdateSecretaryDoctorsRequestValidator()
    {
        RuleFor(request => request.DoctorIds)
            .NotNull()
            .Must(ids => ids.Count <= AdminRules.MaxDoctorAssignments)
            .WithMessage($"A secretary can be assigned to at most {AdminRules.MaxDoctorAssignments} doctors.")
            .When(request => request.DoctorIds is not null);

        RuleFor(request => request.DoctorIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("A doctor cannot be assigned twice to the same secretary.")
            .When(request => request.DoctorIds is not null);

        RuleForEach(request => request.DoctorIds)
            .NotEqual(Guid.Empty)
            .When(request => request.DoctorIds is not null);
    }
}
