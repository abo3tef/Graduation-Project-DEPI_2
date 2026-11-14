using FluentValidation;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Validation;
using Medilog.Application.Features.Auth.Contracts;

namespace Medilog.Application.Features.Auth;

/// <summary>
/// Sign-in use case: verifies credentials, account state, and issues the access token.
/// </summary>
/// <remarks>
/// <para>
/// The role, account id and doctor id all come from the account record, never from the request
/// (AGENTS.md §5). Unknown email and wrong password produce exactly the same exception, so the
/// response cannot be used to enumerate accounts.
/// </para>
/// <para>
/// Only the password check is expensive; account state is checked afterwards so an inactive or
/// unverified account is only revealed to a caller who knows the correct password.
/// </para>
/// </remarks>
public sealed class AuthService : IAuthService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenIssuer _accessTokenIssuer;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthService(
        IApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        IAccessTokenIssuer accessTokenIssuer,
        IValidator<LoginRequest> loginValidator)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _accessTokenIssuer = accessTokenIssuer;
        _loginValidator = loginValidator;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_loginValidator, request, cancellationToken);

        var email = AuthRules.NormalizeEmail(request.Email);
        var account = await AuthRules.FindAccountAsync(_dbContext, email, cancellationToken);

        if (account is null)
        {
            // Same work as a real verification, so a missing email is not detectable by timing.
            _passwordHasher.Verify(request.Password, AuthRules.TimingEqualizationHash);

            throw new AuthenticationFailedException(AuthRules.InvalidCredentialsMessage);
        }

        if (!_passwordHasher.Verify(request.Password, account.PasswordHash))
        {
            throw new AuthenticationFailedException(AuthRules.InvalidCredentialsMessage);
        }

        AuthRules.EnsureCanSignIn(account);

        var token = _accessTokenIssuer.IssueAccessToken(account.Id, account.Role, account.DoctorId);

        return new AuthResponse(
            token.Token,
            token.ExpiresAtUtc,
            new AuthUserResponse(account.Id, account.FullName, account.Email, account.Role, account.DoctorId));
    }
}
