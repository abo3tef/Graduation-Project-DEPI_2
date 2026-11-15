using FluentValidation;
using Medilog.Application.Features.Auth.Contracts;

namespace Medilog.Application.Features.Auth.Validation;

/// <summary>
/// Validates the shape of a verification request. Whether the token exists, is unused and has not
/// expired is a state rule enforced by <see cref="AuthTokenService"/> (AGENTS.md §3.2).
/// </summary>
public sealed class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(request => request.Token)
            .NotEmpty()
            .WithMessage("A verification token is required.")
            .MaximumLength(512)
            .WithMessage("The verification token is not valid.");
    }
}

/// <summary>
/// Validates the shape of a resend-verification request. Nothing here reveals whether the account
/// exists; the response is the same either way (AGENTS.md §5).
/// </summary>
public sealed class ResendVerificationRequestValidator : AbstractValidator<ResendVerificationRequest>
{
    public ResendVerificationRequestValidator()
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
