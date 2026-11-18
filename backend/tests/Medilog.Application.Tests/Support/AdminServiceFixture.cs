using AutoMapper;
using Medilog.Application.Common;
using Medilog.Application.Features.Admin;
using Medilog.Application.Features.Admin.Validators;
using Medilog.Application.Interfaces;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Medilog.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Medilog.Application.Tests.Support;

/// <summary>
/// Builds <see cref="AdminService"/> on a throwaway EF Core InMemory database. The production
/// account-table transition is exercised through the HTTP tests; this fake keeps the unit tests
/// focused on the service's business decisions.
/// </summary>
internal sealed class AdminServiceFixture : IDisposable
{
    private static readonly IMapper Mapper = new MapperConfiguration(
            configuration => configuration.AddProfile<AdminMappingProfile>(),
            NullLoggerFactory.Instance)
        .CreateMapper();

    public AdminServiceFixture(Guid? actingAdminId = null)
    {
        DbContext = CreateContext();
        CurrentUser = new FakeCurrentUser { UserId = actingAdminId, Role = UserRole.Admin };
        Service = new AdminService(
            DbContext,
            CurrentUser,
            Mapper,
            new InMemoryAdminAccountTransition(DbContext),
            new ListAdminUsersQueryValidator(),
            new SetAdminUserStatusRequestValidator(),
            new ChangeAdminUserRoleRequestValidator(),
            new UpdateSecretaryDoctorsRequestValidator());
    }

    public MedilogDbContext DbContext { get; }

    public FakeCurrentUser CurrentUser { get; }

    public IAdminService Service { get; }

    public Task<int> SaveChangesAsync() => DbContext.SaveChangesAsync();

    public void Dispose() => DbContext.Dispose();

    private static MedilogDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MedilogDbContext>()
            .UseInMemoryDatabase($"medilog-f11-{Guid.NewGuid():N}")
            .Options;

        return new MedilogDbContext(options);
    }

    /// <summary>Mirrors the infrastructure port on InMemory so role-change rules can be asserted.</summary>
    private sealed class InMemoryAdminAccountTransition : IAdminAccountTransition
    {
        private readonly MedilogDbContext _db;

        public InMemoryAdminAccountTransition(MedilogDbContext db)
        {
            _db = db;
        }

        public async Task ChangeRoleAsync(
            AdminAccountSnapshot account,
            CancellationToken cancellationToken)
        {
            if (account.CurrentRole == UserRole.Doctor)
            {
                var doctor = await _db.Doctors.FirstAsync(
                    entity => entity.Id == account.Id, cancellationToken);
                _db.Doctors.Remove(doctor);
            }
            else if (account.CurrentRole == UserRole.Secretary)
            {
                var secretary = await _db.Secretaries.FirstAsync(
                    entity => entity.Id == account.Id, cancellationToken);
                _db.Secretaries.Remove(secretary);
            }
            else
            {
                throw new InvalidOperationException("Unsupported role change.");
            }

            await _db.SaveChangesAsync(cancellationToken);
            _db.ChangeTracker.Clear();

            var tokens = await _db.AuthTokens
                .Where(token => token.UserId == account.Id && token.ConsumedAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var token in tokens)
            {
                token.UserType = account.NewRole switch
                {
                    UserRole.Doctor => UserType.Doctor,
                    UserRole.Secretary => UserType.Secretary,
                    _ => token.UserType
                };
            }

            if (account.NewRole == UserRole.Doctor)
            {
                _db.Doctors.Add(new Doctor
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
                _db.Secretaries.Add(new Secretary
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

            // The explicit creation timestamp is preserved by the shared audit hook, exactly as
            // in the production transition.
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>Synthetic identity data for the admin tests.</summary>
internal static class AdminTestData
{
    public static Medilog.Domain.Entities.Admin Admin(
        string email = "admin@medilog.test",
        AccountStatus status = AccountStatus.Active) => new()
    {
        FullName = "Administrator",
        Email = email,
        PasswordHash = "test-only-hash",
        AccountStatus = status,
        EmailVerifiedAtUtc = DateTimeOffset.UtcNow,
    };

    public static Doctor Doctor(
        string email = "doctor@medilog.test",
        AccountStatus status = AccountStatus.Active) => new()
    {
        FullName = "Doctor Under Test",
        Email = email,
        PasswordHash = "test-only-hash",
        Specialization = "General Medicine",
        AccountStatus = status,
        EmailVerifiedAtUtc = DateTimeOffset.UtcNow,
    };

    public static Secretary Secretary(
        string email = "secretary@medilog.test",
        AccountStatus status = AccountStatus.Active) => new()
    {
        FullName = "Secretary Under Test",
        Email = email,
        PasswordHash = "test-only-hash",
        AccountStatus = status,
        EmailVerifiedAtUtc = DateTimeOffset.UtcNow,
    };

    public static Patient Patient(Guid doctorId) => new()
    {
        DoctorId = doctorId,
        FullName = "Patient Under Test",
        DateOfBirth = new DateOnly(1990, 4, 12),
        Gender = Gender.Female,
    };
}
