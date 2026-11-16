using FluentValidation;
using Medilog.Application.Features.Auth.Contracts;

namespace Medilog.Application.Features.Auth.Validation;

/// <summary>
/// Validates the shape of a sign-in request.
/// </summary>
/// <remarks>
/// Only input shape is checked here; whether the account exists, is verified and is active is a
/// state rule enforced by <see cref="AuthService"/> (AGENTS.md §3.2). The password rules are
/// deliberately weaker than the registration policy: an existing account may predate a policy
/// change, and rejecting the request on shape would reveal policy details.
/// </remarks>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
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
            .MaximumLength(128)
            .WithMessage("The password is too long.");
    }
}
