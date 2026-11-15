using FluentValidation;
using Medilog.Application.Features.Auth.Contracts;

namespace Medilog.Application.Features.Auth.Validation;

/// <summary>
/// Validates the shape of a forgot-password request. Whether the address has an account is not a
/// validation concern: the endpoint answers the same way either way (AGENTS.md §5).
/// </summary>
public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .WithMessage("An email address is required.")
            .MaximumLength(AuthRules.EmailMaxLength)
            .WithMessage("The email address is too long.")
            .EmailAddress()
            .WithMessage("The email address is not valid.");
    }
}

/// <summary>
/// Validates the shape of a reset-password request. Token validity (existence, expiry, purpose,
/// single use) is a state rule enforced by <see cref="AuthTokenService"/> (AGENTS.md §3.2).
/// </summary>
public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(request => request.Token)
            .NotEmpty()
            .WithMessage("A reset token is required.")
            .MaximumLength(512)
            .WithMessage("The reset token is not valid.");

        RuleFor(request => request.NewPassword)
            .NotEmpty()
            .WithMessage("A new password is required.")
            .Must(PasswordPolicy.IsSatisfiedBy)
            .WithMessage(PasswordPolicy.RequirementMessage);
    }
}
