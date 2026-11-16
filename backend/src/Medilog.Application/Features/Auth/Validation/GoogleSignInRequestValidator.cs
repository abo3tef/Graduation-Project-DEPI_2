using FluentValidation;
using Medilog.Application.Features.Auth.Contracts;

namespace Medilog.Application.Features.Auth.Validation;

/// <summary>
/// Validates the shape of a Google sign-in request (F6-C1-T8). Whether the token is genuine,
/// unexpired and issued for this project is decided by <see cref="Common.Abstractions.IGoogleTokenValidator"/>
/// (AGENTS.md §3.2).
/// </summary>
public sealed class GoogleSignInRequestValidator : AbstractValidator<GoogleSignInRequest>
{
    public GoogleSignInRequestValidator()
    {
        RuleFor(request => request.IdToken)
            .NotEmpty()
            .WithMessage("A Google ID token is required.")
            .MaximumLength(8192)
            .WithMessage("The Google ID token is not valid.");
    }
}
