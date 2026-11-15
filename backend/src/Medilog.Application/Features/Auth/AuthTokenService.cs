using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Medilog.Application.Features.Auth;

/// <summary>
/// Issues and validates the single-use tokens behind email verification and password reset
/// (decisions D-33/D-36, F6-C1-T4/T6/T7).
/// </summary>
/// <remarks>
/// <para>
/// Only the SHA-256 hash of a token is stored; the raw value is written into the emailed link once
/// and never persisted or logged (AGENTS.md §5). Issuing a token consumes every previous
/// unconsumed token for the same account and purpose, so an old link stops working immediately.
/// </para>
/// <para>
/// Validation is deliberately uniform: an unknown, already-used and expired token produce the same
/// <c>auth.token_invalid</c> failure so a caller learns nothing about other accounts.
/// </para>
/// </remarks>
public sealed class AuthTokenService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly IEmailService _emailService;
    private readonly IAuthLinkBuilder _linkBuilder;
    private readonly AuthTokenLifetimes _lifetimes;
    private readonly TimeProvider _clock;

    public AuthTokenService(
        IApplicationDbContext dbContext,
        ISecureTokenGenerator tokenGenerator,
        IEmailService emailService,
        IAuthLinkBuilder linkBuilder,
        AuthTokenLifetimes lifetimes,
        TimeProvider clock)
    {
        _dbContext = dbContext;
        _tokenGenerator = tokenGenerator;
        _emailService = emailService;
        _linkBuilder = linkBuilder;
        _lifetimes = lifetimes;
        _clock = clock;
    }

    /// <summary>Issues a verification token, invalidates the previous one and emails the link.</summary>
    public async Task IssueEmailVerificationAsync(
        Guid userId,
        UserType userType,
        string fullName,
        string email,
        CancellationToken cancellationToken)
    {
        var rawToken = await StoreTokenAsync(userId, userType, AuthTokenPurpose.EmailVerification, cancellationToken);
        var link = _linkBuilder.BuildEmailVerificationLink(rawToken);
        var body = AuthEmailTemplates.VerificationBody(
            fullName,
            link,
            _lifetimes.EmailVerification);

        await _emailService.SendAsync(
            new EmailMessage(email, AuthEmailTemplates.VerificationSubject, body),
            cancellationToken);
    }

    /// <summary>Issues a password-reset token, invalidates the previous one and emails the link.</summary>
    public async Task IssuePasswordResetAsync(
        Guid userId,
        UserType userType,
        string fullName,
        string email,
        CancellationToken cancellationToken)
    {
        var rawToken = await StoreTokenAsync(userId, userType, AuthTokenPurpose.PasswordReset, cancellationToken);
        var link = _linkBuilder.BuildPasswordResetLink(rawToken);
        var body = AuthEmailTemplates.ResetBody(
            fullName,
            link,
            _lifetimes.PasswordReset);

        await _emailService.SendAsync(
            new EmailMessage(email, AuthEmailTemplates.ResetSubject, body),
            cancellationToken);
    }

    /// <summary>
    /// Returns the stored token for a raw value, or fails with <c>auth.token_invalid</c> when it is
    /// unknown, already used, expired, or was issued for another purpose.
    /// </summary>
    /// <remarks>
    /// The returned entity is still unconsumed and tracked; the caller marks it consumed in the same
    /// <c>SaveChangesAsync</c> that applies the account change, so a token cannot be used twice.
    /// </remarks>
    public async Task<AuthToken> RequireValidTokenAsync(
        string rawToken,
        AuthTokenPurpose purpose,
        CancellationToken cancellationToken)
    {
        var tokenHash = _tokenGenerator.HashToken(rawToken);

        var token = await _dbContext.AuthTokens
            .FirstOrDefaultAsync(
                candidate => candidate.TokenHash == tokenHash && candidate.Purpose == purpose,
                cancellationToken);

        if (token is null || token.ConsumedAtUtc is not null || token.ExpiresAtUtc <= _clock.GetUtcNow())
        {
            throw new AuthenticationFailedException(AuthRules.InvalidTokenMessage, ErrorCodes.AuthTokenInvalid);
        }

        return token;
    }

    private async Task<string> StoreTokenAsync(
        Guid userId,
        UserType userType,
        AuthTokenPurpose purpose,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();

        // A new link replaces every previous unused link (D-33); both changes are one transaction.
        var previousTokens = await _dbContext.AuthTokens
            .Where(candidate =>
                candidate.UserId == userId &&
                candidate.UserType == userType &&
                candidate.Purpose == purpose &&
                candidate.ConsumedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var previousToken in previousTokens)
        {
            previousToken.ConsumedAtUtc = now;
        }

        var rawToken = _tokenGenerator.GenerateToken();

        _dbContext.AuthTokens.Add(new AuthToken
        {
            UserId = userId,
            UserType = userType,
            Purpose = purpose,
            TokenHash = _tokenGenerator.HashToken(rawToken),
            ExpiresAtUtc = now.Add(_lifetimes.For(purpose)),
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return rawToken;
    }
}
