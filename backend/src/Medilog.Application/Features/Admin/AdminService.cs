using AutoMapper;
using FluentValidation;
using Medilog.Application.Common;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Paging;
using Medilog.Application.Common.Validation;
using Medilog.Application.Features.Admin.Requests;
using Medilog.Application.Features.Admin.Responses;
using Medilog.Application.Interfaces;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Medilog.Application.Features.Admin;

/// <inheritdoc cref="IAdminService"/>
public sealed class AdminService : IAdminService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly IAdminAccountTransition _accountTransition;
    private readonly IValidator<ListAdminUsersQuery> _listValidator;
    private readonly IValidator<SetAdminUserStatusRequest> _statusValidator;
    private readonly IValidator<ChangeAdminUserRoleRequest> _roleValidator;
    private readonly IValidator<UpdateSecretaryDoctorsRequest> _assignmentValidator;

    public AdminService(
        IApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IMapper mapper,
        IAdminAccountTransition accountTransition,
        IValidator<ListAdminUsersQuery> listValidator,
        IValidator<SetAdminUserStatusRequest> statusValidator,
        IValidator<ChangeAdminUserRoleRequest> roleValidator,
        IValidator<UpdateSecretaryDoctorsRequest> assignmentValidator)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _mapper = mapper;
        _accountTransition = accountTransition;
        _listValidator = listValidator;
        _statusValidator = statusValidator;
        _roleValidator = roleValidator;
        _assignmentValidator = assignmentValidator;
    }

    public async Task<PagedResult<AdminUserResponse>> ListAsync(
        ListAdminUsersQuery query,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_listValidator, query, cancellationToken);
        await RequireAdminAsync(cancellationToken);

        UserRole? role = AdminRules.TryParseRoleFilter(query.Role, out var parsedRole) && query.Role is not null
            && !string.IsNullOrWhiteSpace(query.Role)
                ? parsedRole
                : null;
        AccountStatus? status = AdminRules.TryParseStatusFilter(query.Status, out var parsedStatus)
            && query.Status is not null && !string.IsNullOrWhiteSpace(query.Status)
                ? parsedStatus
                : null;

        var users = BuildListQuery(role, status, query.Search);
        var totalCount = await users.CountAsync(cancellationToken);

        // A long skip avoids int overflow for an absurd page number; a page past the end is empty.
        var skip = (long)(query.PageNumber - 1) * query.PageSize;

        if (skip >= totalCount)
        {
            return new PagedResult<AdminUserResponse>([], query.PageNumber, query.PageSize, totalCount);
        }

        var page = await users
            .Skip((int)skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminUserResponse>(
            page.Select(row => ToResponse(ToRecord(row))).ToList(),
            query.PageNumber,
            query.PageSize,
            totalCount);
    }

    public async Task<AdminUserResponse> SetStatusAsync(
        Guid userId,
        SetAdminUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_statusValidator, request, cancellationToken);
        var actingAdminId = await RequireAdminAsync(cancellationToken);

        if (userId == actingAdminId && request.Status == AccountStatus.Inactive)
        {
            throw new ForbiddenException(
                "An administrator cannot deactivate their own account.",
                ErrorCodes.AdminSelfDeactivationForbidden);
        }

        var target = await FindUserAsync(userId, cancellationToken)
            ?? throw UserNotFound();

        if (target.Status == request.Status)
        {
            return ToResponse(target);
        }

        if (target.Role == UserRole.Admin && request.Status == AccountStatus.Inactive)
        {
            var otherActiveAdmins = await _dbContext.Admins.CountAsync(
                admin => admin.Id != userId && admin.AccountStatus == AccountStatus.Active,
                cancellationToken);

            if (otherActiveAdmins == 0)
            {
                throw new ConflictException(
                    "The last active administrator cannot be deactivated.",
                    ErrorCodes.AdminLastAdminProtected);
            }
        }

        AdminUserRecord updated;

        switch (target.Role)
        {
            case UserRole.Admin:
                var admin = await _dbContext.Admins.FirstAsync(entity => entity.Id == userId, cancellationToken);
                admin.AccountStatus = request.Status;
                updated = ToRecord(admin);
                break;

            case UserRole.Doctor:
                var doctor = await _dbContext.Doctors.FirstAsync(entity => entity.Id == userId, cancellationToken);
                doctor.AccountStatus = request.Status;
                updated = ToRecord(doctor);
                break;

            case UserRole.Secretary:
                var secretary = await _dbContext.Secretaries.FirstAsync(entity => entity.Id == userId, cancellationToken);
                secretary.AccountStatus = request.Status;
                updated = ToRecord(secretary);
                break;

            default:
                throw UserNotFound();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(updated);
    }

    public async Task<AdminUserResponse> ChangeRoleAsync(
        Guid userId,
        ChangeAdminUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_roleValidator, request, cancellationToken);
        var actingAdminId = await RequireAdminAsync(cancellationToken);

        if (userId == actingAdminId)
        {
            throw new ForbiddenException(
                "An administrator cannot change their own role.",
                ErrorCodes.AdminSelfRoleChangeForbidden);
        }

        var target = await FindUserAsync(userId, cancellationToken)
            ?? throw UserNotFound();

        if (target.Role == UserRole.Admin)
        {
            throw new ForbiddenException(
                "An administrator account cannot be changed to another role.",
                ErrorCodes.AdminRoleChangeForbidden);
        }

        if (target.Role == request.Role)
        {
            return ToResponse(target);
        }

        var snapshot = await LoadAccountAsync(target, cancellationToken);
        await EnsureRoleChangeHasNoConflictsAsync(snapshot, cancellationToken);

        await _accountTransition.ChangeRoleAsync(snapshot, cancellationToken);

        return ToResponse(new AdminUserRecord(
            snapshot.Id,
            snapshot.NewRole,
            snapshot.FullName,
            snapshot.Email,
            snapshot.Phone,
            snapshot.NewRole == UserRole.Doctor ? snapshot.Specialization : null,
            snapshot.Status,
            snapshot.EmailVerifiedAtUtc.HasValue,
            snapshot.CreatedAtUtc));
    }

    public async Task<SecretaryDoctorsResponse> GetDoctorsAsync(
        Guid secretaryId,
        CancellationToken cancellationToken)
    {
        await RequireAdminAsync(cancellationToken);

        var secretary = await _dbContext.Secretaries
            .AsNoTracking()
            .Include(entity => entity.Doctors)
            .FirstOrDefaultAsync(entity => entity.Id == secretaryId, cancellationToken)
            ?? throw SecretaryNotFound();

        return ToAssignmentResponse(secretaryId, secretary.Doctors);
    }

    public async Task<SecretaryDoctorsResponse> ReplaceDoctorsAsync(
        Guid secretaryId,
        UpdateSecretaryDoctorsRequest request,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_assignmentValidator, request, cancellationToken);
        await RequireAdminAsync(cancellationToken);

        var secretary = await _dbContext.Secretaries
            .Include(entity => entity.Doctors)
            .FirstOrDefaultAsync(entity => entity.Id == secretaryId, cancellationToken)
            ?? throw SecretaryNotFound();

        // Validation rejects duplicates; Distinct also protects the join table's unique pair if a
        // validator is ever bypassed by another caller.
        var doctorIds = request.DoctorIds.Distinct().ToList();
        var doctors = await _dbContext.Doctors
            .Where(doctor => doctorIds.Contains(doctor.Id))
            .ToListAsync(cancellationToken);

        if (doctors.Count != doctorIds.Count)
        {
            throw new NotFoundException("One of the doctors was not found.", ErrorCodes.DoctorNotFound);
        }

        secretary.Doctors.Clear();

        foreach (var id in doctorIds)
        {
            secretary.Doctors.Add(doctors.First(doctor => doctor.Id == id));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToAssignmentResponse(secretaryId, doctors);
    }

    /// <summary>
    /// Resolves the acting user from the validated token and confirms the Admin account is still
    /// active, so a deactivated Admin cannot keep administering through an old token.
    /// </summary>
    private async Task<Guid> RequireAdminAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId || userId == Guid.Empty ||
            _currentUser.Role != UserRole.Admin)
        {
            throw new ForbiddenException(
                "Only an administrator can manage accounts.",
                ErrorCodes.IdentityInvalid);
        }

        var isActiveAdmin = await _dbContext.Admins
            .AsNoTracking()
            .AnyAsync(
                admin => admin.Id == userId && admin.AccountStatus == AccountStatus.Active,
                cancellationToken);

        if (!isActiveAdmin)
        {
            throw new ForbiddenException(
                "Only an active administrator can manage accounts.",
                ErrorCodes.IdentityInvalid);
        }

        return userId;
    }

    /// <summary>
    /// The filtered, deterministically ordered query used by the paged list endpoint. Kept separate
    /// from the async call so its translation can be asserted against a relational provider.
    /// </summary>
    internal IQueryable<AdminUserRow> BuildListQuery(
        UserRole? role,
        AccountStatus? status,
        string? search)
    {
        var users = AllUsers();

        if (role.HasValue)
        {
            users = users.Where(user => user.Role == role.Value);
        }

        if (status.HasValue)
        {
            users = users.Where(user => user.Status == status.Value);
        }

        // Email is not unique across the three identity tables, so the id is the stable tie-breaker.
        return Filter(users, search)
            .OrderBy(user => user.Email)
            .ThenBy(user => user.Id);
    }

    /// <summary>One role-neutral, credential-free query over the three identity tables.</summary>
    /// <remarks>
    /// The three branches stay anonymous so the <c>UNION</c> is a plain server-side set operation.
    /// Only after the union do we project to <see cref="AdminUserRow"/>, whose member-initializer
    /// projection stays composable for the filters and deterministic ordering applied by callers.
    /// Mapping to <see cref="AdminUserRecord"/> happens in memory after paging.
    /// </remarks>
    internal IQueryable<AdminUserRow> AllUsers()
    {
        var admins = _dbContext.Admins.AsNoTracking().Select(admin => new
        {
            admin.Id,
            Role = UserRole.Admin,
            admin.FullName,
            admin.Email,
            Phone = (string?)null,
            Specialization = (string?)null,
            Status = admin.AccountStatus,
            EmailVerified = admin.EmailVerifiedAtUtc != null,
            admin.CreatedAtUtc,
        });

        var doctors = _dbContext.Doctors.AsNoTracking().Select(doctor => new
        {
            doctor.Id,
            Role = UserRole.Doctor,
            doctor.FullName,
            doctor.Email,
            doctor.Phone,
            doctor.Specialization,
            Status = doctor.AccountStatus,
            EmailVerified = doctor.EmailVerifiedAtUtc != null,
            doctor.CreatedAtUtc,
        });

        var secretaries = _dbContext.Secretaries.AsNoTracking().Select(secretary => new
        {
            secretary.Id,
            Role = UserRole.Secretary,
            secretary.FullName,
            secretary.Email,
            secretary.Phone,
            Specialization = (string?)null,
            Status = secretary.AccountStatus,
            EmailVerified = secretary.EmailVerifiedAtUtc != null,
            secretary.CreatedAtUtc,
        });

        return admins.Union(doctors).Union(secretaries).Select(user => new AdminUserRow
        {
            Id = user.Id,
            Role = user.Role,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Specialization = user.Specialization,
            Status = user.Status,
            EmailVerified = user.EmailVerified,
            CreatedAtUtc = user.CreatedAtUtc,
        });
    }

    private async Task<AdminUserRecord?> FindUserAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await AllUsers().FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
        return row is null ? null : ToRecord(row);
    }

    private static IQueryable<AdminUserRow> Filter(IQueryable<AdminUserRow> users, string? search)
    {
        var term = search?.Trim();

        if (string.IsNullOrEmpty(term))
        {
            return users;
        }

        if (Guid.TryParse(term, out var id))
        {
            return users.Where(user => user.Id == id);
        }

        return users.Where(user =>
            user.FullName.Contains(term) ||
            user.Email.Contains(term) ||
            (user.Phone != null && user.Phone.Contains(term)));
    }

    private async Task<AdminAccountSnapshot> LoadAccountAsync(
        AdminUserRecord target,
        CancellationToken cancellationToken)
    {
        if (target.Role == UserRole.Doctor)
        {
            var doctor = await _dbContext.Doctors
                .AsNoTracking()
                .FirstAsync(entity => entity.Id == target.Id, cancellationToken);

            return new AdminAccountSnapshot(
                doctor.Id,
                UserRole.Doctor,
                UserRole.Secretary,
                doctor.FullName,
                doctor.Email,
                doctor.PasswordHash,
                doctor.Phone,
                doctor.Specialization,
                doctor.AccountStatus,
                doctor.EmailVerifiedAtUtc,
                doctor.CreatedAtUtc);
        }

        if (target.Role == UserRole.Secretary)
        {
            var secretary = await _dbContext.Secretaries
                .AsNoTracking()
                .FirstAsync(entity => entity.Id == target.Id, cancellationToken);

            return new AdminAccountSnapshot(
                secretary.Id,
                UserRole.Secretary,
                UserRole.Doctor,
                secretary.FullName,
                secretary.Email,
                secretary.PasswordHash,
                secretary.Phone,
                null,
                secretary.AccountStatus,
                secretary.EmailVerifiedAtUtc,
                secretary.CreatedAtUtc);
        }

        throw new ForbiddenException(
            "An administrator account cannot be changed to another role.",
            ErrorCodes.AdminRoleChangeForbidden);
    }

    /// <summary>
    /// Q-22: data that would be orphaned or would give the new role unintended access blocks the
    /// transition. The checks run against the data source, before the atomic table replacement.
    /// </summary>
    private async Task EnsureRoleChangeHasNoConflictsAsync(
        AdminAccountSnapshot account,
        CancellationToken cancellationToken)
    {
        var id = account.Id;

        if (account.CurrentRole == UserRole.Doctor)
        {
            var hasDoctorData =
                await _dbContext.Patients.AnyAsync(entity => entity.DoctorId == id, cancellationToken) ||
                await _dbContext.MedicalVisits.AnyAsync(entity => entity.DoctorId == id, cancellationToken) ||
                await _dbContext.MedicalNotes.AnyAsync(entity => entity.DoctorId == id, cancellationToken) ||
                await _dbContext.MedicalDocuments.AnyAsync(entity => entity.UploadedBy == id, cancellationToken) ||
                await _dbContext.Appointments.AnyAsync(entity => entity.DoctorId == id, cancellationToken) ||
                await _dbContext.Payments.AnyAsync(entity => entity.DoctorId == id, cancellationToken) ||
                await _dbContext.Secretaries.AnyAsync(
                    entity => entity.Doctors.Any(doctor => doctor.Id == id), cancellationToken);

            if (hasDoctorData ||
                await _dbContext.Secretaries.AnyAsync(
                    entity => entity.Email == account.Email && entity.Id != id, cancellationToken))
            {
                throw RoleChangeBlocked();
            }

            return;
        }

        var hasSecretaryData =
            await _dbContext.Appointments.AnyAsync(entity => entity.CreatedBy == id, cancellationToken) ||
            await _dbContext.Payments.AnyAsync(entity => entity.CreatedBy == id, cancellationToken) ||
            await _dbContext.Secretaries.AnyAsync(
                entity => entity.Id == id && entity.Doctors.Any(), cancellationToken);

        if (hasSecretaryData ||
            await _dbContext.Doctors.AnyAsync(
                entity => entity.Email == account.Email && entity.Id != id, cancellationToken))
        {
            throw RoleChangeBlocked();
        }
    }

    private SecretaryDoctorsResponse ToAssignmentResponse(
        Guid secretaryId,
        IEnumerable<Doctor> doctors) =>
        new(
            secretaryId,
            doctors
                .OrderBy(doctor => doctor.Email)
                .ThenBy(doctor => doctor.Id)
                .Select(_mapper.Map<AdminDoctorResponse>)
                .ToList());

    private AdminUserResponse ToResponse(AdminUserRecord record) =>
        _mapper.Map<AdminUserResponse>(record);

    private static AdminUserRecord ToRecord(AdminUserRow row) => new(
        row.Id,
        row.Role,
        row.FullName,
        row.Email,
        row.Phone,
        row.Specialization,
        row.Status,
        row.EmailVerified,
        row.CreatedAtUtc);

    private static AdminUserRecord ToRecord(Medilog.Domain.Entities.Admin entity) => new(
        entity.Id, UserRole.Admin, entity.FullName, entity.Email, null, null,
        entity.AccountStatus, entity.EmailVerifiedAtUtc.HasValue, entity.CreatedAtUtc);

    private static AdminUserRecord ToRecord(Doctor entity) => new(
        entity.Id, UserRole.Doctor, entity.FullName, entity.Email, entity.Phone, entity.Specialization,
        entity.AccountStatus, entity.EmailVerifiedAtUtc.HasValue, entity.CreatedAtUtc);

    private static AdminUserRecord ToRecord(Secretary entity) => new(
        entity.Id, UserRole.Secretary, entity.FullName, entity.Email, entity.Phone, null,
        entity.AccountStatus, entity.EmailVerifiedAtUtc.HasValue, entity.CreatedAtUtc);

    private static NotFoundException UserNotFound() =>
        new("The user was not found.", ErrorCodes.AdminUserNotFound);

    private static NotFoundException SecretaryNotFound() =>
        new("The secretary was not found.", ErrorCodes.AdminSecretaryNotFound);

    private static ConflictException RoleChangeBlocked() =>
        new(
            "The role change is blocked by data that belongs to the current role.",
            ErrorCodes.AdminRoleChangeBlocked);
}
