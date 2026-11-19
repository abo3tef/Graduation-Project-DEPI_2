using FluentValidation;
using Medilog.Application.Common;
using Medilog.Application.Features.Auth.Contracts;

namespace Medilog.Application.Features.Auth.Validation;

/// <summary>
/// Validates the shape of a registration request (F6-C1-T4, AGENTS.md §3.2).
/// </summary>
/// <remarks>
/// <para>
/// The role allow-list is enforced here as well as in the service, so a request for
/// <c>Admin</c> (or an unknown numeric role) can never create an administrator (AGENTS.md §5).
/// </para>
/// <para>
/// Limits mirror the persisted columns: name 200, email 320, phone 30, specialization 150. Whether
/// the email already exists is a state rule enforced by <see cref="AuthService"/>.
/// </para>
/// </remarks>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    private const int NameMaxLength = 200;
    private const int PhoneMaxLength = 30;
    private const int SpecializationMaxLength = 150;

    public RegisterRequestValidator()
    {
        RuleFor(request => request.FullName)
            .NotEmpty()
            .WithMessage("A full name is required.")
            .MaximumLength(NameMaxLength)
            .WithMessage("The full name is too long.");

        RuleFor(request => request.Email)
            .NotEmpty()
            .WithMessage("An email address is required.")
            .MaximumLength(AuthRules.EmailMaxLength)
            .WithMessage("The email address is too long.")
            .EmailAddress()
            .WithMessage("The email address is not valid.");

        RuleFor(request => request.Password)
            .NotEmpty()
            .WithMessage("A password is required.")
            .Must(PasswordPolicy.IsSatisfiedBy)
            .WithMessage(PasswordPolicy.RequirementMessage);

        RuleFor(request => request.Phone)
            .MaximumLength(PhoneMaxLength)
            .WithMessage("The phone number is too long.")
            .When(request => !string.IsNullOrWhiteSpace(request.Phone));

        RuleFor(request => request.Role)
            .Must(role => role is UserRole.Doctor or UserRole.Secretary)
            .WithMessage("Only Doctor or Secretary accounts can be registered.");

        RuleFor(request => request.Specialization)
            .MaximumLength(SpecializationMaxLength)
            .WithMessage("The specialization is too long.")
            .When(request => !string.IsNullOrWhiteSpace(request.Specialization));

        RuleFor(request => request.Specialization)
            .NotEmpty()
            .WithMessage("A specialization is required for a doctor.")
            .When(request => request.Role == UserRole.Doctor);

        RuleFor(request => request.Specialization)
            .Must(string.IsNullOrWhiteSpace)
            .WithMessage("A specialization can only be given for a doctor.")
            .When(request => request.Role == UserRole.Secretary);
    }
}
