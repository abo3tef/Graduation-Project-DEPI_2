using Medilog.Application.Common.Abstractions;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Medilog.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Medilog.Infrastructure.Persistence.Seed;

/// <summary>
/// Creates the three development demo accounts: one admin, one doctor and one secretary
/// (decision D-40, F6-C1-T5).
/// </summary>
/// <remarks>
/// <para>
/// The accounts are pre-verified and active so the demo can sign in without an SMTP server. The
/// seeder is idempotent: an existing account is left untouched, so restarting the API never resets
/// a changed password or re-activates an account an administrator deactivated (F11).
/// </para>
/// <para>
/// This type is only invoked by the WebAPI host in the Development environment. It must never be
/// called in production, and it never logs a password.
/// </para>
/// </remarks>
public sealed class DevelopmentSeeder
{
    public const string AdminEmail = "admin@medilog.local";
    public const string DoctorEmail = "doctor@medilog.local";
    public const string SecretaryEmail = "secretary@medilog.local";

    private const string AdminName = "Medilog Admin";
    private const string DoctorName = "Dr. Demo Doctor";
    private const string DoctorSpecialization = "General Medicine";
    private const string SecretaryName = "Demo Secretary";

    private readonly MedilogDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly SeedOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<DevelopmentSeeder> _logger;

    public DevelopmentSeeder(
        MedilogDbContext dbContext,
        IPasswordHasher passwordHasher,
        SeedOptions options,
        TimeProvider clock,
        ILogger<DevelopmentSeeder> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Adds any missing demo account. Returns the number of accounts created.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Seeding is enabled but no <c>Seed:Password</c> is configured; the demo accounts cannot have
    /// a usable password, so failing early is safer than creating accounts nobody can sign in to.
    /// </exception>
    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return 0;
        }

        if (string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new InvalidOperationException(
                "Seed:Password must be configured when Seed:Enabled is true. " +
                "It is a development-only demo credential.");
        }

        var verifiedAt = _clock.GetUtcNow();
        var created = 0;

        if (!await _dbContext.Admins.AnyAsync(entity => entity.Email == AdminEmail, cancellationToken))
        {
            _dbContext.Admins.Add(new Admin
            {
                FullName = AdminName,
                Email = AdminEmail,
                PasswordHash = _passwordHasher.Hash(_options.Password),
                AccountStatus = AccountStatus.Active,
                EmailVerifiedAtUtc = verifiedAt,
            });

            created++;
        }

        if (!await _dbContext.Doctors.AnyAsync(entity => entity.Email == DoctorEmail, cancellationToken))
        {
            _dbContext.Doctors.Add(new Doctor
            {
                FullName = DoctorName,
                Email = DoctorEmail,
                PasswordHash = _passwordHasher.Hash(_options.Password),
                Specialization = DoctorSpecialization,
                AccountStatus = AccountStatus.Active,
                EmailVerifiedAtUtc = verifiedAt,
            });

            created++;
        }

        if (!await _dbContext.Secretaries.AnyAsync(entity => entity.Email == SecretaryEmail, cancellationToken))
        {
            _dbContext.Secretaries.Add(new Secretary
            {
                FullName = SecretaryName,
                Email = SecretaryEmail,
                PasswordHash = _passwordHasher.Hash(_options.Password),
                AccountStatus = AccountStatus.Active,
                EmailVerifiedAtUtc = verifiedAt,
            });

            created++;
        }

        if (created > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Created {Count} development seed account(s) for the local demo.",
                created);
        }

        return created;
    }
}
