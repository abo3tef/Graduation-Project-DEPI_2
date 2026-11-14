using Medilog.Application.Common;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Medilog.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Medilog.Application.Features.Auth;

/// <summary>
/// An account resolved from one of the three identity tables, in the shape authentication needs.
/// </summary>
/// <remarks>
/// Doctors, secretaries and admins live in separate tables (decision D-32), so this record is the
/// single representation the auth use cases work with. <see cref="DoctorId"/> is derived from the
/// role instead of being supplied by the caller.
/// </remarks>
internal sealed record AuthAccount(
    Guid Id,
    UserRole Role,
    string FullName,
    string Email,
    string PasswordHash,
    AccountStatus AccountStatus,
    DateTimeOffset? EmailVerifiedAtUtc)
{
    public bool IsActive => AccountStatus == AccountStatus.Active;

    public bool IsEmailVerified => EmailVerifiedAtUtc.HasValue;

    /// <summary>Doctors authenticate as themselves, so the claim equals the account id (D-35).</summary>
    public Guid? DoctorId => Role == UserRole.Doctor ? Id : null;
}

/// <summary>
/// Rules shared by the authentication use cases, so the same decision is not re-implemented in
/// several slices of F6.
/// </summary>
internal static class AuthRules
{
    /// <summary>Matches the persisted <c>email</c> column length.</summary>
    public const int EmailMaxLength = 320;

    /// <summary>The only answer given for an unknown email or a wrong password.</summary>
    public const string InvalidCredentialsMessage = "Invalid email or password.";

    /// <summary>
    /// A well-formed PBKDF2 hash (decision D-34) that matches no password. Verifying against it when
    /// the email is unknown performs the same expensive work as a real check, so the response time
    /// does not reveal whether an account exists.
    /// </summary>
    public const string TimingEqualizationHash =
        "v1$600000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    /// <summary>Trims and lower-cases an email; storage and comparison use this form (D-43).</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// Finds an account by its normalized email across doctors, secretaries and admins.
    /// </summary>
    /// <remarks>
    /// One account per email is enforced here as well as by the per-table unique indexes (D-43).
    /// The lookup is read-only, so no tracking is needed.
    /// </remarks>
    public static async Task<AuthAccount?> FindAccountAsync(
        IApplicationDbContext dbContext,
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        var doctor = await dbContext.Doctors
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Email == normalizedEmail, cancellationToken);

        if (doctor is not null)
        {
            return new AuthAccount(
                doctor.Id,
                UserRole.Doctor,
                doctor.FullName,
                doctor.Email,
                doctor.PasswordHash,
                doctor.AccountStatus,
                doctor.EmailVerifiedAtUtc);
        }

        var secretary = await dbContext.Secretaries
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Email == normalizedEmail, cancellationToken);

        if (secretary is not null)
        {
            return new AuthAccount(
                secretary.Id,
                UserRole.Secretary,
                secretary.FullName,
                secretary.Email,
                secretary.PasswordHash,
                secretary.AccountStatus,
                secretary.EmailVerifiedAtUtc);
        }

        var admin = await dbContext.Admins
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Email == normalizedEmail, cancellationToken);

        return admin is null
            ? null
            : new AuthAccount(
                admin.Id,
                UserRole.Admin,
                admin.FullName,
                admin.Email,
                admin.PasswordHash,
                admin.AccountStatus,
                admin.EmailVerifiedAtUtc);
    }

    /// <summary>
    /// Rejects accounts that exist with valid credentials but may not sign in (Q-11).
    /// </summary>
    /// <remarks>
    /// Called only after the password has been verified, so account state is never disclosed to
    /// someone who does not already know the password.
    /// </remarks>
    public static void EnsureCanSignIn(AuthAccount account)
    {
        if (!account.IsActive)
        {
            throw new ForbiddenException(
                "This account is inactive. Contact an administrator.",
                ErrorCodes.AuthAccountInactive);
        }

        if (!account.IsEmailVerified)
        {
            throw new ForbiddenException(
                "Verify your email address before signing in.",
                ErrorCodes.AuthEmailUnverified);
        }
    }
}
