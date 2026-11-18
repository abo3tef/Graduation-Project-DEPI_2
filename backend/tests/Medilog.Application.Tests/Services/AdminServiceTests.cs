using Medilog.Application.Common;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.Admin.Requests;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Medilog.Application.Tests.Services;

/// <summary>
/// Business-rule tests for F11-C1. The rules are derived from the approved task file and
/// STATE.md Q-16/Q-22; all data is synthetic (AGENTS.md §5).
/// </summary>
public sealed class AdminServiceTests
{
    [Fact]
    public async Task List_filters_searches_orders_and_pages_with_the_shared_envelope()
    {
        var (fixture, admin) = await NewFixtureAsync();
        var doctorA = AdminTestData.Doctor("a-doctor@medilog.test");
        var doctorB = AdminTestData.Doctor("b-doctor@medilog.test");
        doctorB.AccountStatus = AccountStatus.Inactive;
        var secretary = AdminTestData.Secretary("secretary@medilog.test");
        fixture.DbContext.AddRange(doctorA, doctorB, secretary);
        await fixture.SaveChangesAsync();

        var firstPage = await fixture.Service.ListAsync(
            new ListAdminUsersQuery(PageNumber: 1, PageSize: 2), CancellationToken.None);
        var doctors = await fixture.Service.ListAsync(
            new ListAdminUsersQuery(Role: "Doctor", PageSize: 50), CancellationToken.None);
        var inactive = await fixture.Service.ListAsync(
            new ListAdminUsersQuery(Status: "Inactive", PageSize: 50), CancellationToken.None);
        var search = await fixture.Service.ListAsync(
            new ListAdminUsersQuery(Search: "Secretary Under Test"), CancellationToken.None);
        var byId = await fixture.Service.ListAsync(
            new ListAdminUsersQuery(Search: admin.Id.ToString()), CancellationToken.None);
        var beyond = await fixture.Service.ListAsync(
            new ListAdminUsersQuery(PageNumber: 99, PageSize: 2), CancellationToken.None);

        Assert.Equal(4, firstPage.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(2, firstPage.Items.Count);
        var expectedOrder = new[]
            {
                (admin.Email, admin.Id),
                (doctorA.Email, doctorA.Id),
                (doctorB.Email, doctorB.Id),
                (secretary.Email, secretary.Id),
            }
            .OrderBy(user => user.Email, StringComparer.Ordinal)
            .ThenBy(user => user.Id)
            .Select(user => user.Email)
            .ToArray();
        Assert.Equal(expectedOrder.Take(2), firstPage.Items.Select(user => user.Email));

        Assert.Equal(2, doctors.TotalCount);
        Assert.All(doctors.Items, user => Assert.Equal(UserRole.Doctor, user.Role));
        Assert.Equal(1, inactive.TotalCount);
        Assert.Equal(doctorB.Id, Assert.Single(inactive.Items).Id);
        Assert.Equal(secretary.Id, Assert.Single(search.Items).Id);
        Assert.Equal(admin.Id, Assert.Single(byId.Items).Id);
        Assert.Empty(beyond.Items);
        Assert.Equal(4, beyond.TotalCount);
        Assert.Equal(2, beyond.TotalPages);
    }

    [Theory]
    [InlineData(null, null, 0, 10)]
    [InlineData(null, null, 1, PagingDefaults.MaxPageSize + 1)]
    [InlineData("Nurse", null, 1, 10)]
    [InlineData(null, "Maybe", 1, 10)]
    public async Task List_rejects_invalid_paging_and_filters(
        string? role,
        string? status,
        int pageNumber,
        int pageSize)
    {
        var (fixture, _) = await NewFixtureAsync();
        var query = new ListAdminUsersQuery(
            Role: role,
            Status: status,
            PageNumber: pageNumber,
            PageSize: pageSize);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => fixture.Service.ListAsync(query, CancellationToken.None));

        Assert.Equal(ErrorCodes.ValidationFailed, exception.Code);
    }

    [Fact]
    public async Task Every_use_case_rechecks_the_authenticated_active_admin()
    {
        var (fixture, admin) = await NewFixtureAsync();
        fixture.CurrentUser.Role = UserRole.Doctor;

        var wrongRole = await Assert.ThrowsAsync<ForbiddenException>(
            () => fixture.Service.ListAsync(new ListAdminUsersQuery(), CancellationToken.None));
        Assert.Equal(ErrorCodes.IdentityInvalid, wrongRole.Code);

        fixture.CurrentUser.Role = UserRole.Admin;
        admin.AccountStatus = AccountStatus.Inactive;
        await fixture.SaveChangesAsync();

        var inactive = await Assert.ThrowsAsync<ForbiddenException>(
            () => fixture.Service.ListAsync(new ListAdminUsersQuery(), CancellationToken.None));
        Assert.Equal(ErrorCodes.IdentityInvalid, inactive.Code);
    }

    [Fact]
    public async Task An_admin_cannot_deactivate_themselves_or_remove_the_last_active_admin()
    {
        var (fixture, admin) = await NewFixtureAsync();

        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => fixture.Service.SetStatusAsync(
                admin.Id,
                new SetAdminUserStatusRequest(AccountStatus.Inactive),
                CancellationToken.None));

        Assert.Equal(ErrorCodes.AdminSelfDeactivationForbidden, exception.Code);
        var stillActive = await fixture.DbContext.Admins.FindAsync(admin.Id);
        Assert.NotNull(stillActive);
        Assert.Equal(AccountStatus.Active, stillActive!.AccountStatus);
        Assert.Equal(1, await fixture.DbContext.Admins.AsQueryable().CountAsync(entity =>
            entity.AccountStatus == AccountStatus.Active));
    }

    [Fact]
    public async Task Status_changes_update_the_target_and_tolerate_a_no_op()
    {
        var (fixture, _) = await NewFixtureAsync();
        var doctor = AdminTestData.Doctor();
        fixture.DbContext.Doctors.Add(doctor);
        await fixture.SaveChangesAsync();

        var updated = await fixture.Service.SetStatusAsync(
            doctor.Id,
            new SetAdminUserStatusRequest(AccountStatus.Inactive),
            CancellationToken.None);
        var unchanged = await fixture.Service.SetStatusAsync(
            doctor.Id,
            new SetAdminUserStatusRequest(AccountStatus.Inactive),
            CancellationToken.None);

        Assert.Equal(AccountStatus.Inactive, updated.Status);
        Assert.Equal(AccountStatus.Inactive, unchanged.Status);
        Assert.Equal(AccountStatus.Inactive,
            (await fixture.DbContext.Doctors.FindAsync(doctor.Id))!.AccountStatus);
    }

    [Fact]
    public async Task Status_change_to_an_unknown_user_is_404()
    {
        var (fixture, _) = await NewFixtureAsync();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.Service.SetStatusAsync(
                Guid.NewGuid(),
                new SetAdminUserStatusRequest(AccountStatus.Inactive),
                CancellationToken.None));

        Assert.Equal(ErrorCodes.AdminUserNotFound, exception.Code);
    }

    [Fact]
    public async Task Doctor_can_change_to_secretary_without_losing_identity_or_credentials()
    {
        var (fixture, _) = await NewFixtureAsync();
        var doctor = AdminTestData.Doctor();
        var unconsumed = new AuthToken
        {
            UserId = doctor.Id,
            UserType = UserType.Doctor,
            Purpose = AuthTokenPurpose.EmailVerification,
            TokenHash = "hash-only",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
        };
        var consumed = new AuthToken
        {
            UserId = doctor.Id,
            UserType = UserType.Doctor,
            Purpose = AuthTokenPurpose.PasswordReset,
            TokenHash = "old-hash-only",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
            ConsumedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
        };
        fixture.DbContext.AddRange(doctor, unconsumed, consumed);
        await fixture.SaveChangesAsync();

        // The shared audit hook assigns the account's creation instant on insert; the role move
        // must carry exactly that stored value into the new table.
        var originalCreatedAt = doctor.CreatedAtUtc;

        var response = await fixture.Service.ChangeRoleAsync(
            doctor.Id,
            new ChangeAdminUserRoleRequest(UserRole.Secretary),
            CancellationToken.None);

        Assert.Equal(doctor.Id, response.Id);
        Assert.Equal(UserRole.Secretary, response.Role);
        Assert.Equal(doctor.Email, response.Email);
        Assert.Equal(originalCreatedAt, response.CreatedAtUtc);
        Assert.True(response.EmailVerified);

        var secretary = await fixture.DbContext.Secretaries.FindAsync(doctor.Id);
        Assert.NotNull(secretary);
        Assert.Equal(doctor.PasswordHash, secretary!.PasswordHash);
        Assert.Equal(doctor.Phone, secretary.Phone);
        Assert.Equal(doctor.AccountStatus, secretary.AccountStatus);
        Assert.Equal(originalCreatedAt, secretary.CreatedAtUtc);
        Assert.Null(await fixture.DbContext.Doctors.FindAsync(doctor.Id));

        Assert.Equal(UserType.Secretary,
            (await fixture.DbContext.AuthTokens.FindAsync(unconsumed.Id))!.UserType);
        Assert.Equal(UserType.Doctor,
            (await fixture.DbContext.AuthTokens.FindAsync(consumed.Id))!.UserType);
    }

    [Fact]
    public async Task Secretary_can_change_to_doctor_when_no_conflicting_data_exists()
    {
        var (fixture, _) = await NewFixtureAsync();
        var secretary = AdminTestData.Secretary();
        secretary.Phone = "01000000000";
        fixture.DbContext.Secretaries.Add(secretary);
        await fixture.SaveChangesAsync();

        var response = await fixture.Service.ChangeRoleAsync(
            secretary.Id,
            new ChangeAdminUserRoleRequest(UserRole.Doctor),
            CancellationToken.None);

        Assert.Equal(secretary.Id, response.Id);
        Assert.Equal(UserRole.Doctor, response.Role);
        var doctor = await fixture.DbContext.Doctors.FindAsync(secretary.Id);
        Assert.NotNull(doctor);
        Assert.Equal(secretary.PasswordHash, doctor!.PasswordHash);
        Assert.Equal(secretary.Phone, doctor.Phone);
        Assert.Null(doctor.Specialization);
        Assert.Null(await fixture.DbContext.Secretaries.FindAsync(secretary.Id));
    }

    [Fact]
    public async Task Role_change_is_blocked_by_current_role_data_in_both_directions()
    {
        var (fixture, _) = await NewFixtureAsync();
        var doctor = AdminTestData.Doctor("busy-doctor@medilog.test");
        var otherDoctor = AdminTestData.Doctor("owner@medilog.test");
        var secretary = AdminTestData.Secretary("busy-secretary@medilog.test");
        fixture.DbContext.AddRange(doctor, otherDoctor, secretary);
        fixture.DbContext.Patients.Add(AdminTestData.Patient(doctor.Id));
        fixture.DbContext.Appointments.Add(new Appointment
        {
            PatientId = Guid.NewGuid(),
            DoctorId = otherDoctor.Id,
            CreatedBy = secretary.Id,
            AppointmentDate = new DateOnly(2030, 1, 3),
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(10, 30),
            Status = AppointmentStatus.Scheduled,
        });
        await fixture.SaveChangesAsync();

        var doctorConflict = await Assert.ThrowsAsync<ConflictException>(
            () => fixture.Service.ChangeRoleAsync(
                doctor.Id,
                new ChangeAdminUserRoleRequest(UserRole.Secretary),
                CancellationToken.None));
        var secretaryConflict = await Assert.ThrowsAsync<ConflictException>(
            () => fixture.Service.ChangeRoleAsync(
                secretary.Id,
                new ChangeAdminUserRoleRequest(UserRole.Doctor),
                CancellationToken.None));

        Assert.Equal(ErrorCodes.AdminRoleChangeBlocked, doctorConflict.Code);
        Assert.Equal(ErrorCodes.AdminRoleChangeBlocked, secretaryConflict.Code);
        Assert.NotNull(await fixture.DbContext.Doctors.FindAsync(doctor.Id));
        Assert.NotNull(await fixture.DbContext.Secretaries.FindAsync(secretary.Id));
    }

    [Fact]
    public async Task Secretary_with_assigned_doctors_cannot_change_role()
    {
        var (fixture, _) = await NewFixtureAsync();
        var secretary = AdminTestData.Secretary();
        var doctor = AdminTestData.Doctor("assigned-doctor@medilog.test");
        secretary.Doctors.Add(doctor);
        fixture.DbContext.AddRange(secretary, doctor);
        await fixture.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => fixture.Service.ChangeRoleAsync(
                secretary.Id,
                new ChangeAdminUserRoleRequest(UserRole.Doctor),
                CancellationToken.None));

        Assert.Equal(ErrorCodes.AdminRoleChangeBlocked, exception.Code);
        Assert.NotNull(await fixture.DbContext.Secretaries.FindAsync(secretary.Id));
    }

    [Fact]
    public async Task Role_change_protects_admins_self_and_invalid_requests()
    {
        var (fixture, admin) = await NewFixtureAsync();
        var otherAdmin = AdminTestData.Admin("second-admin@medilog.test");
        var doctor = AdminTestData.Doctor();
        fixture.DbContext.AddRange(otherAdmin, doctor);
        await fixture.SaveChangesAsync();

        var self = await Assert.ThrowsAsync<ForbiddenException>(
            () => fixture.Service.ChangeRoleAsync(
                admin.Id,
                new ChangeAdminUserRoleRequest(UserRole.Secretary),
                CancellationToken.None));
        var adminTarget = await Assert.ThrowsAsync<ForbiddenException>(
            () => fixture.Service.ChangeRoleAsync(
                otherAdmin.Id,
                new ChangeAdminUserRoleRequest(UserRole.Doctor),
                CancellationToken.None));
        var invalid = await Assert.ThrowsAsync<RequestValidationException>(
            () => fixture.Service.ChangeRoleAsync(
                doctor.Id,
                new ChangeAdminUserRoleRequest(UserRole.Admin),
                CancellationToken.None));
        var missing = await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.Service.ChangeRoleAsync(
                Guid.NewGuid(),
                new ChangeAdminUserRoleRequest(UserRole.Secretary),
                CancellationToken.None));

        Assert.Equal(ErrorCodes.AdminSelfRoleChangeForbidden, self.Code);
        Assert.Equal(ErrorCodes.AdminRoleChangeForbidden, adminTarget.Code);
        Assert.Equal(ErrorCodes.ValidationFailed, invalid.Code);
        Assert.Equal(ErrorCodes.AdminUserNotFound, missing.Code);
    }

    [Fact]
    public async Task Assignment_replacement_is_unique_bounded_and_rejects_unknown_doctors()
    {
        var (fixture, _) = await NewFixtureAsync();
        var secretary = AdminTestData.Secretary();
        var doctorA = AdminTestData.Doctor("a-doctor@medilog.test");
        var doctorB = AdminTestData.Doctor("b-doctor@medilog.test");
        fixture.DbContext.AddRange(secretary, doctorA, doctorB);
        await fixture.SaveChangesAsync();

        var assigned = await fixture.Service.ReplaceDoctorsAsync(
            secretary.Id,
            new UpdateSecretaryDoctorsRequest([doctorB.Id, doctorA.Id]),
            CancellationToken.None);
        var readBack = await fixture.Service.GetDoctorsAsync(secretary.Id, CancellationToken.None);
        var cleared = await fixture.Service.ReplaceDoctorsAsync(
            secretary.Id,
            new UpdateSecretaryDoctorsRequest([]),
            CancellationToken.None);
        var duplicate = await Assert.ThrowsAsync<RequestValidationException>(
            () => fixture.Service.ReplaceDoctorsAsync(
                secretary.Id,
                new UpdateSecretaryDoctorsRequest([doctorA.Id, doctorA.Id]),
                CancellationToken.None));
        var missing = await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.Service.ReplaceDoctorsAsync(
                secretary.Id,
                new UpdateSecretaryDoctorsRequest([Guid.NewGuid()]),
                CancellationToken.None));
        var missingSecretary = await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.Service.GetDoctorsAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal([doctorA.Id, doctorB.Id], assigned.Doctors.Select(doctor => doctor.Id));
        Assert.Equal(assigned.Doctors.Select(doctor => doctor.Id),
            readBack.Doctors.Select(doctor => doctor.Id));
        Assert.Empty(cleared.Doctors);
        Assert.Equal(ErrorCodes.ValidationFailed, duplicate.Code);
        Assert.Equal(ErrorCodes.DoctorNotFound, missing.Code);
        Assert.Equal(ErrorCodes.AdminSecretaryNotFound, missingSecretary.Code);
    }

    private static async Task<(AdminServiceFixture Fixture, Medilog.Domain.Entities.Admin Admin)>
        NewFixtureAsync()
    {
        var admin = AdminTestData.Admin();
        var fixture = new AdminServiceFixture(admin.Id);
        fixture.DbContext.Admins.Add(admin);
        await fixture.SaveChangesAsync();
        return (fixture, admin);
    }
}
