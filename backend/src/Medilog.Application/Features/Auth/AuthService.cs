using FluentValidation;
using Medilog.Application.Common;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Validation;
using Medilog.Application.Features.Auth.Contracts;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Medilog.Application.Features.Auth;

/// <summary>
/// Sign-in, registration, email-verification and password-recovery use cases
/// (F6-C1-T3/T4/T6/T7).
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
/// <para>
/// New accounts are created inactive and unverified; confirming the email address activates them
/// (Q-11). Registration returns no token, so the account cannot be used before verification.
/// </para>
/// </remarks>
public sealed class AuthService : IAuthService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenIssuer _accessTokenIssuer;
    private readonly AuthTokenService _authTokenService;
    private readonly TimeProvider _clock;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<VerifyEmailRequest> _verifyEmailValidator;
    private readonly IValidator<ResendVerificationRequest> _resendVerificationValidator;
    private readonly IValidator<ForgotPasswordRequest> _forgotPasswordValidator;
    private readonly IValidator<ResetPasswordRequest> _resetPasswordValidator;
    private readonly IValidator<GoogleSignInRequest> _googleSignInValidator;
    private readonly IGoogleTokenValidator _googleTokenValidator;

    public AuthService(
        IApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        IAccessTokenIssuer accessTokenIssuer,
        AuthTokenService authTokenService,
        TimeProvider clock,
        IGoogleTokenValidator googleTokenValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<RegisterRequest> registerValidator,
        IValidator<VerifyEmailRequest> verifyEmailValidator,
        IValidator<ResendVerificationRequest> resendVerificationValidator,
        IValidator<ForgotPasswordRequest> forgotPasswordValidator,
        IValidator<ResetPasswordRequest> resetPasswordValidator,
        IValidator<GoogleSignInRequest> googleSignInValidator)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _accessTokenIssuer = accessTokenIssuer;
        _authTokenService = authTokenService;
        _clock = clock;
        _googleTokenValidator = googleTokenValidator;
        _loginValidator = loginValidator;
        _registerValidator = registerValidator;
        _verifyEmailValidator = verifyEmailValidator;
        _resendVerificationValidator = resendVerificationValidator;
        _forgotPasswordValidator = forgotPasswordValidator;
        _resetPasswordValidator = resetPasswordValidator;
        _googleSignInValidator = googleSignInValidator;
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

        return CreateSession(account);
    }

    public async Task<AuthUserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_registerValidator, request, cancellationToken);

        var email = AuthRules.NormalizeEmail(request.Email);

        if (await AuthRules.FindAccountAsync(_dbContext, email, cancellationToken) is not null)
        {
            throw new ConflictException(
                AuthRules.EmailAlreadyRegisteredMessage,
                ErrorCodes.AuthEmailAlreadyRegistered);
        }

        var passwordHash = _passwordHasher.Hash(request.Password);
        var fullName = request.FullName.Trim();
        Guid accountId;
        UserType userType;

        if (request.Role == UserRole.Doctor)
        {
            var doctor = new Doctor
            {
                FullName = fullName,
                Email = email,
                PasswordHash = passwordHash,
                Phone = NormalizeOptional(request.Phone),
                Specialization = NormalizeOptional(request.Specialization),
                AccountStatus = AccountStatus.Inactive,
                EmailVerifiedAtUtc = null,
            };

            _dbContext.Doctors.Add(doctor);
            accountId = doctor.Id;
            userType = UserType.Doctor;
        }
        else
        {
            var secretary = new Secretary
            {
                FullName = fullName,
                Email = email,
                PasswordHash = passwordHash,
                Phone = NormalizeOptional(request.Phone),
                AccountStatus = AccountStatus.Inactive,
                EmailVerifiedAtUtc = null,
            };

            _dbContext.Secretaries.Add(secretary);
            accountId = secretary.Id;
            userType = UserType.Secretary;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // The account exists now, so a failure to send the email does not lose it: the user can
        // request another link through resend-verification.
        await _authTokenService.IssueEmailVerificationAsync(
            accountId,
            userType,
            fullName,
            email,
            cancellationToken);

        return new AuthUserResponse(
            accountId,
            fullName,
            email,
            request.Role,
            request.Role == UserRole.Doctor ? accountId : null,
            request.Role == UserRole.Doctor ? NormalizeOptional(request.Specialization) : null);
    }

    public async Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_verifyEmailValidator, request, cancellationToken);

        var token = await _authTokenService.RequireValidTokenAsync(
            request.Token,
            AuthTokenPurpose.EmailVerification,
            cancellationToken);

        var now = _clock.GetUtcNow();

        var accountExists = await AuthRules.MarkEmailVerifiedAsync(
            _dbContext,
            token.UserId,
            token.UserType,
            now,
            cancellationToken);

        if (!accountExists)
        {
            // The account was removed after the link was sent; treat the token as invalid rather
            // than confirming a token that protects nothing.
            throw new AuthenticationFailedException(
                AuthRules.InvalidTokenMessage,
                ErrorCodes.AuthTokenInvalid);
        }

        token.ConsumedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ResendVerificationAsync(
        ResendVerificationRequest request,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_resendVerificationValidator, request, cancellationToken);

        var email = AuthRules.NormalizeEmail(request.Email);
        var account = await AuthRules.FindAccountAsync(_dbContext, email, cancellationToken);

        // Unknown address, already verified, or an administrator-created account: nothing to send.
        // The caller always receives the same answer (AGENTS.md §5).
        if (account is null || account.IsEmailVerified)
        {
            return;
        }

        await _authTokenService.IssueEmailVerificationAsync(
            account.Id,
            account.UserType,
            account.FullName,
            account.Email,
            cancellationToken);
    }

    public async Task ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_forgotPasswordValidator, request, cancellationToken);

        var email = AuthRules.NormalizeEmail(request.Email);
        var account = await AuthRules.FindAccountAsync(_dbContext, email, cancellationToken);

        // An unknown address is not an error and produces no email, so the response is identical
        // for registered and unregistered emails (AGENTS.md §5).
        if (account is null)
        {
            return;
        }

        // The mail goes to the address on the account, never to a caller-supplied one.
        await _authTokenService.IssuePasswordResetAsync(
            account.Id,
            account.UserType,
            account.FullName,
            account.Email,
            cancellationToken);
    }

    public async Task ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_resetPasswordValidator, request, cancellationToken);

        var token = await _authTokenService.RequireValidTokenAsync(
            request.Token,
            AuthTokenPurpose.PasswordReset,
            cancellationToken);

        var now = _clock.GetUtcNow();

        // Only the hash is stored; the new password never leaves this method (D-34).
        var passwordHash = _passwordHasher.Hash(request.NewPassword);

        var accountExists = await AuthRules.SetPasswordHashAsync(
            _dbContext,
            token.UserId,
            token.UserType,
            passwordHash,
            cancellationToken);

        if (!accountExists)
        {
            // The account was removed after the link was sent; the token protects nothing now.
            throw new AuthenticationFailedException(
                AuthRules.InvalidTokenMessage,
                ErrorCodes.AuthTokenInvalid);
        }

        token.ConsumedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthResponse> SignInWithGoogleAsync(
        GoogleSignInRequest request,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_googleSignInValidator, request, cancellationToken);

        // Signature, issuer, audience and expiry are checked on the server; nothing from the
        // request is trusted beyond the supplied token (Q-12).
        var identity = await _googleTokenValidator.ValidateAsync(request.IdToken, cancellationToken);

        if (!identity.EmailVerified)
        {
            // An address Google itself has not verified must never be used to reach an account.
            throw new AuthenticationFailedException(
                "Google sign-in is not available for this account.",
                ErrorCodes.AuthInvalidGoogleToken);
        }

        var email = AuthRules.NormalizeEmail(identity.Email);
        var account = await AuthRules.FindAccountAsync(_dbContext, email, cancellationToken);

        if (account is null)
        {
            // No auto-creation: registration is the only way to get an account (Q-12).
            throw new AuthenticationFailedException(
                "Google sign-in is not available for this account.",
                ErrorCodes.AuthInvalidGoogleToken);
        }

        AuthRules.EnsureCanSignIn(account);

        return CreateSession(account);
    }

    /// <summary>
    /// Issues the access token and account summary for an account that passed every check.
    /// </summary>
    private AuthResponse CreateSession(AuthAccount account)
    {
        var token = _accessTokenIssuer.IssueAccessToken(account.Id, account.Role, account.DoctorId);

        return new AuthResponse(
            token.Token,
            token.ExpiresAtUtc,
            new AuthUserResponse(account.Id, account.FullName, account.Email, account.Role, account.DoctorId, account.Specialization));
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
