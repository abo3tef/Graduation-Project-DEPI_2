using Medilog.Application.Common;
using Medilog.Application.Interfaces;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Medilog.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Medilog.Infrastructure.Persistence.AccountAdmin;

/// <summary>
/// EF Core implementation of the account-table move behind a Doctor↔Secretary role change.
/// </summary>
/// <remarks>
/// The three account tables have no shared discriminator, so the row must be deleted from one table
/// and reinserted with the same id in the other. Both statements run in one database transaction;
/// the change tracker is cleared between them because EF cannot track a deleted and an added entity
/// with the same key at once. The explicit creation timestamp is preserved by the shared audit hook.
/// Unconsumed one-time tokens follow the user's new account kind so an issued verification/reset link
/// keeps working. All decisions about whether the move is allowed stay in the Application service.
/// </remarks>
internal sealed class AdminAccountTransition : IAdminAccountTransition
{
    private readonly MedilogDbContext _dbContext;

    public AdminAccountTransition(MedilogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ChangeRoleAsync(
        AdminAccountSnapshot account,
        CancellationToken cancellationToken)
    {
        var oldUserType = ToUserType(account.CurrentRole);
        var newUserType = ToUserType(account.NewRole);

        // InMemory has no transaction support and is configured to treat the warning as an error;
        // relational databases get the real atomic delete + insert + timestamp-restore transaction.
        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        switch (account.CurrentRole)
        {
            case UserRole.Doctor:
                var doctor = await _dbContext.Doctors
                    .FirstOrDefaultAsync(entity => entity.Id == account.Id, cancellationToken)
                    ?? throw new InvalidOperationException("The doctor account no longer exists.");
                _dbContext.Doctors.Remove(doctor);
                break;

            case UserRole.Secretary:
                var secretary = await _dbContext.Secretaries
                    .FirstOrDefaultAsync(entity => entity.Id == account.Id, cancellationToken)
                    ?? throw new InvalidOperationException("The secretary account no longer exists.");
                _dbContext.Secretaries.Remove(secretary);
                break;

            default:
                throw new InvalidOperationException("Only Doctor and Secretary accounts can change tables.");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();

        var tokens = await _dbContext.AuthTokens
            .Where(token =>
                token.UserId == account.Id &&
                token.UserType == oldUserType &&
                token.ConsumedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.UserType = newUserType;
        }

        if (account.NewRole == UserRole.Doctor)
        {
            _dbContext.Doctors.Add(new Doctor
            {
                Id = account.Id,
                FullName = account.FullName,
                Email = account.Email,
                PasswordHash = account.PasswordHash,
                Phone = account.Phone,
                Specialization = account.Specialization,
                AccountStatus = account.Status,
                EmailVerifiedAtUtc = account.EmailVerifiedAtUtc,
                CreatedAtUtc = account.CreatedAtUtc,
            });
        }
        else
        {
            _dbContext.Secretaries.Add(new Secretary
            {
                Id = account.Id,
                FullName = account.FullName,
                Email = account.Email,
                PasswordHash = account.PasswordHash,
                Phone = account.Phone,
                AccountStatus = account.Status,
                EmailVerifiedAtUtc = account.EmailVerifiedAtUtc,
                CreatedAtUtc = account.CreatedAtUtc,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static UserType ToUserType(UserRole role) => role switch
    {
        UserRole.Doctor => UserType.Doctor,
        UserRole.Secretary => UserType.Secretary,
        UserRole.Admin => UserType.Admin,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown account role.")
    };
}
