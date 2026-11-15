using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;

namespace Medilog.WebAPI.Tests.Support;

/// <summary>
/// Replaces the Firebase token validator in HTTP tests so the Google sign-in rules can be proven
/// without a Firebase project or network access (F6-C1-T8, Q-12).
/// </summary>
internal sealed class FakeGoogleTokenValidator : IGoogleTokenValidator
{
    /// <summary>Identity returned for the next call. A null value simulates an invalid token.</summary>
    public GoogleTokenIdentity? NextIdentity { get; set; }

    public Task<GoogleTokenIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        if (NextIdentity is null)
        {
            throw new AuthenticationFailedException(
                "The Google sign-in token is not valid.",
                ErrorCodes.AuthInvalidGoogleToken);
        }

        return Task.FromResult(NextIdentity);
    }
}
