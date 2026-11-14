using Medilog.Domain.Enums;
using Medilog.Infrastructure.Persistence.Context;
using Medilog.Infrastructure.Persistence.Seed;
using Medilog.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Medilog.Application.Tests.Auth;

/// <summary>
/// Verifies the development-only seed accounts (F6-C1-T5, decision D-40).
/// </summary>
public sealed class SeedTests
{
    private const string SeedPassword = "Medilog@Dev2026";

    [Fact]
    public async Task Seed_creates_one_active_verified_account_per_role()
    {
        // Arrange
        using var context = CreateContext();
        var seeder = CreateSeeder(context, enabled: true);

        // Act
        var created = await seeder.SeedAsync();

        // Assert
        Assert.Equal(3, created);
        Assert.Equal(3, await context.Admins.CountAsync() + await context.Secretaries.CountAsync() + await context.Doctors.CountAsync());

        var admin = await context.Admins.SingleAsync();
        var doctor = await context.Doctors.SingleAsync();
        var secretary = await context.Secretaries.SingleAsync();

        Assert.Equal(DevelopmentSeeder.AdminEmail, admin.Email);
        Assert.Equal(DevelopmentSeeder.DoctorEmail, doctor.Email);
        Assert.Equal(DevelopmentSeeder.SecretaryEmail, secretary.Email);

        var hasher = new PasswordHasher();

        foreach (var (email, status, verifiedAt, hash) in new[]
        {
            (admin.Email, admin.AccountStatus, admin.EmailVerifiedAtUtc, admin.PasswordHash),
            (doctor.Email, doctor.AccountStatus, doctor.EmailVerifiedAtUtc, doctor.PasswordHash),
            (secretary.Email, secretary.AccountStatus, secretary.EmailVerifiedAtUtc, secretary.PasswordHash),
        })
        {
            Assert.False(string.IsNullOrWhiteSpace(email));
            Assert.Equal(AccountStatus.Active, status);
            Assert.NotNull(verifiedAt);
            Assert.True(hasher.Verify(SeedPassword, hash));
        }
    }

    [Fact]
    public async Task Seed_is_idempotent()
    {
        // Arrange
        using var context = CreateContext();
        var seeder = CreateSeeder(context, enabled: true);

        // Act
        var firstRun = await seeder.SeedAsync();
        var secondRun = await seeder.SeedAsync();

        // Assert
        Assert.Equal(3, firstRun);
        Assert.Equal(0, secondRun);
        Assert.Equal(1, await context.Admins.CountAsync());
        Assert.Equal(1, await context.Doctors.CountAsync());
        Assert.Equal(1, await context.Secretaries.CountAsync());
    }

    [Fact]
    public async Task Seed_does_nothing_when_disabled()
    {
        // Arrange
        using var context = CreateContext();
        var seeder = CreateSeeder(context, enabled: false);

        // Act
        var created = await seeder.SeedAsync();

        // Assert
        Assert.Equal(0, created);
        Assert.Empty(context.Admins);
        Assert.Empty(context.Doctors);
        Assert.Empty(context.Secretaries);
    }

    [Fact]
    public async Task Seed_without_a_password_fails_instead_of_creating_unusable_accounts()
    {
        // Arrange
        using var context = CreateContext();
        var seeder = new DevelopmentSeeder(
            context,
            new PasswordHasher(),
            new SeedOptions { Enabled = true, Password = "  " },
            TimeProvider.System,
            NullLogger<DevelopmentSeeder>.Instance);

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync());
        Assert.Empty(context.Admins);
    }

    private static MedilogDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<MedilogDbContext>()
            .UseInMemoryDatabase($"medilog-seed-{Guid.NewGuid():N}")
            .Options);

    private static DevelopmentSeeder CreateSeeder(MedilogDbContext context, bool enabled) =>
        new(
            context,
            new PasswordHasher(),
            new SeedOptions { Enabled = enabled, Password = SeedPassword },
            TimeProvider.System,
            NullLogger<DevelopmentSeeder>.Instance);
}
