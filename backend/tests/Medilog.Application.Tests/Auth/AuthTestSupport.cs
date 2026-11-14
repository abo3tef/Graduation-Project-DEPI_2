using Medilog.Application.Common;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Features.Auth;
using Medilog.Application.Features.Auth.Validation;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Medilog.Infrastructure.Persistence.Context;
using Medilog.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Medilog.Application.Tests.Auth;

/// <summary>
/// Runs the real <see cref="AuthService"/> against a throwaway EF Core InMemory database, with the
/// real PBKDF2 hasher and a recording token issuer.
/// </summary>
/// <remarks>
/// The InMemory provider does not enforce the unique email indexes, so the per-table constraint is
/// asserted through configuration review; the cross-table rule is covered by the service tests.
/// Values are synthetic; no real patient or clinician data is used (AGENTS.md §5).
/// </remarks>
internal sealed class AuthFixture : IDisposable
{
    public const string Password = "Valid#Passw0rd";

    public AuthFixture()
    {
        DbContext = new MedilogDbContext(
            new DbContextOptionsBuilder<MedilogDbContext>()
                .UseInMemoryDatabase($"medilog-auth-{Guid.NewGuid():N}")
                .Options);

        Clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        Hasher = new PasswordHasher();
        TokenIssuer = new RecordingAccessTokenIssuer(Clock);
        Service = new AuthService(DbContext, Hasher, TokenIssuer, new LoginRequestValidator());
    }

    public MedilogDbContext DbContext { get; }

    public FakeTimeProvider Clock { get; }

    public PasswordHasher Hasher { get; }

    public RecordingAccessTokenIssuer TokenIssuer { get; }

    public AuthService Service { get; }

    public Doctor AddDoctor(
        string email = "doctor@medilog.test",
        AccountStatus status = AccountStatus.Active,
        bool verified = true,
        string? password = null)
    {
        var doctor = new Doctor
        {
            FullName = "Doctor Under Test",
            Email = email,
            PasswordHash = Hasher.Hash(password ?? Password),
            AccountStatus = status,
            EmailVerifiedAtUtc = verified ? Clock.GetUtcNow() : null,
        };

        DbContext.Doctors.Add(doctor);
        DbContext.SaveChanges();

        return doctor;
    }

    public Secretary AddSecretary(
        string email = "secretary@medilog.test",
        AccountStatus status = AccountStatus.Active,
        bool verified = true,
        string? password = null)
    {
        var secretary = new Secretary
        {
            FullName = "Secretary Under Test",
            Email = email,
            PasswordHash = Hasher.Hash(password ?? Password),
            AccountStatus = status,
            EmailVerifiedAtUtc = verified ? Clock.GetUtcNow() : null,
        };

        DbContext.Secretaries.Add(secretary);
        DbContext.SaveChanges();

        return secretary;
    }

    public Admin AddAdmin(
        string email = "admin@medilog.test",
        AccountStatus status = AccountStatus.Active,
        bool verified = true,
        string? password = null)
    {
        var admin = new Admin
        {
            FullName = "Admin Under Test",
            Email = email,
            PasswordHash = Hasher.Hash(password ?? Password),
            AccountStatus = status,
            EmailVerifiedAtUtc = verified ? Clock.GetUtcNow() : null,
        };

        DbContext.Admins.Add(admin);
        DbContext.SaveChanges();

        return admin;
    }

    public void Dispose() => DbContext.Dispose();
}

/// <summary>
/// Records the identity the service hands to the token issuer instead of producing a JWT, so tests
/// can assert the claims contract inputs without depending on token serialization.
/// </summary>
internal sealed class RecordingAccessTokenIssuer : IAccessTokenIssuer
{
    private readonly TimeProvider _clock;

    public RecordingAccessTokenIssuer(TimeProvider clock)
    {
        _clock = clock;
    }

    public List<(Guid UserId, UserRole Role, Guid? DoctorId)> Calls { get; } = [];

    public IssuedAccessToken IssueAccessToken(Guid userId, UserRole role, Guid? doctorId)
    {
        Calls.Add((userId, role, doctorId));

        return new IssuedAccessToken(
            $"test-token-{Calls.Count}",
            _clock.GetUtcNow().AddMinutes(1440));
    }
}

/// <summary>A controllable clock for deterministic expiry and timestamp assertions.</summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    public FakeTimeProvider(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; set; }

    public override DateTimeOffset GetUtcNow() => UtcNow;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
