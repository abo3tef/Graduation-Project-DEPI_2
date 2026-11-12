using Medilog.Application.Common;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.Appointments.Lookups;
using Medilog.Application.Features.Appointments.Responses;
using Medilog.Application.Interfaces;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Medilog.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace Medilog.Infrastructure.Persistence.Appointments;

/// <summary>
/// EF Core implementation of the Application's <see cref="IAppointmentStore"/>.
/// All filtering, counting, ordering and paging run in the database, and read
/// models are projected to the minimum columns (no medical data is loaded).
/// </summary>
internal sealed class AppointmentStore : IAppointmentStore
{
    // MySQL error numbers for "lock wait timeout exceeded" and "deadlock found".
    private const int LockWaitTimeoutError = 1205;
    private const int DeadlockError = 1213;

    private readonly MedilogDbContext _db;

    public AppointmentStore(MedilogDbContext db)
    {
        _db = db;
    }

    public Task<ActorInfo?> GetActorAsync(UserRole role, Guid userId, CancellationToken cancellationToken) =>
        role switch
        {
            UserRole.Doctor => _db.Doctors
                .AsNoTracking()
                .Where(doctor => doctor.Id == userId)
                .Select(doctor => new ActorInfo(doctor.Id, doctor.AccountStatus == AccountStatus.Active))
                .FirstOrDefaultAsync(cancellationToken),
            UserRole.Secretary => _db.Secretaries
                .AsNoTracking()
                .Where(secretary => secretary.Id == userId)
                .Select(secretary => new ActorInfo(secretary.Id, secretary.AccountStatus == AccountStatus.Active))
                .FirstOrDefaultAsync(cancellationToken),
            _ => Task.FromResult<ActorInfo?>(null)
        };

    public Task<DoctorInfo?> GetDoctorAsync(Guid doctorId, CancellationToken cancellationToken) =>
        _db.Doctors
            .AsNoTracking()
            .Where(doctor => doctor.Id == doctorId)
            .Select(doctor => new DoctorInfo(doctor.Id, doctor.AccountStatus == AccountStatus.Active))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<PatientInfo?> GetPatientAsync(Guid patientId, CancellationToken cancellationToken) =>
        _db.Patients
            .AsNoTracking()
            .Where(patient => patient.Id == patientId)
            .Select(patient => new PatientInfo(patient.Id, patient.DoctorId))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> IsDoctorAssignedAsync(Guid secretaryId, Guid doctorId, CancellationToken cancellationToken) =>
        _db.Secretaries
            .AsNoTracking()
            .AnyAsync(
                secretary => secretary.Id == secretaryId && secretary.Doctors.Any(doctor => doctor.Id == doctorId),
                cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetAssignedDoctorIdsAsync(
        Guid secretaryId,
        CancellationToken cancellationToken) =>
        await _db.Secretaries
            .AsNoTracking()
            .Where(secretary => secretary.Id == secretaryId)
            .SelectMany(secretary => secretary.Doctors)
            .Select(doctor => doctor.Id)
            .ToListAsync(cancellationToken);

    public Task<Guid?> GetAppointmentDoctorIdAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        _db.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.Id == appointmentId)
            .Select(appointment => (Guid?)appointment.DoctorId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Appointment?> GetAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        _db.Appointments.FirstOrDefaultAsync(appointment => appointment.Id == appointmentId, cancellationToken);

    public async Task<IReadOnlyList<Appointment>> GetSlotHoldingAppointmentsAsync(
        Guid doctorId,
        DateOnly date,
        Guid? excludeAppointmentId,
        CancellationToken cancellationToken) =>
        await _db.Appointments
            .AsNoTracking()
            .Where(appointment =>
                appointment.DoctorId == doctorId
                && appointment.AppointmentDate == date
                && appointment.Status != AppointmentStatus.Cancelled
                && appointment.Status != AppointmentStatus.NoShow
                && (excludeAppointmentId == null || appointment.Id != excludeAppointmentId))
            .ToListAsync(cancellationToken);

    public Task<AppointmentResponse?> GetDetailsAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        Project(_db.Appointments.AsNoTracking().Where(appointment => appointment.Id == appointmentId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<AppointmentResponse>> ListAsync(
        AppointmentListCriteria criteria,
        CancellationToken cancellationToken)
    {
        var doctorIds = criteria.DoctorIds.ToArray();

        var query = _db.Appointments
            .AsNoTracking()
            .Where(appointment => doctorIds.Contains(appointment.DoctorId));

        if (criteria.PatientId.HasValue)
        {
            query = query.Where(appointment => appointment.PatientId == criteria.PatientId.Value);
        }

        if (criteria.DateFrom.HasValue)
        {
            query = query.Where(appointment => appointment.AppointmentDate >= criteria.DateFrom.Value);
        }

        if (criteria.DateTo.HasValue)
        {
            query = query.Where(appointment => appointment.AppointmentDate <= criteria.DateTo.Value);
        }

        if (criteria.Status.HasValue)
        {
            query = query.Where(appointment => appointment.Status == criteria.Status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = (long)(criteria.PageNumber - 1) * criteria.PageSize;

        if (skip >= totalCount)
        {
            return new PagedResult<AppointmentResponse>([], criteria.PageNumber, criteria.PageSize, totalCount);
        }

        // Deterministic order: the id is a stable tie-breaker for equal date/time.
        var items = await Project(query
                .OrderBy(appointment => appointment.AppointmentDate)
                .ThenBy(appointment => appointment.StartTime)
                .ThenBy(appointment => appointment.Id)
                .Skip((int)skip)
                .Take(criteria.PageSize))
            .ToListAsync(cancellationToken);

        return new PagedResult<AppointmentResponse>(items, criteria.PageNumber, criteria.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<DoctorLookupResponse>> GetActiveDoctorLookupsAsync(
        IReadOnlyCollection<Guid> doctorIds,
        CancellationToken cancellationToken)
    {
        var ids = doctorIds.ToArray();

        return await _db.Doctors
            .AsNoTracking()
            .Where(doctor => ids.Contains(doctor.Id) && doctor.AccountStatus == AccountStatus.Active)
            .OrderBy(doctor => doctor.FullName)
            .ThenBy(doctor => doctor.Id)
            .Select(doctor => new DoctorLookupResponse(doctor.Id, doctor.FullName, doctor.Specialization))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PatientLookupResponse>> SearchPatientsAsync(
        Guid doctorId,
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = _db.Patients
            .AsNoTracking()
            .Where(patient => patient.DoctorId == doctorId);

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(patient =>
                patient.FullName.Contains(search)
                || (patient.Phone != null && patient.Phone.Contains(search)));
        }

        // Only the minimum administrative columns are read; the limit is applied in the database.
        return await query
            .OrderBy(patient => patient.FullName)
            .ThenBy(patient => patient.Id)
            .Take(limit)
            .Select(patient => new PatientLookupResponse(patient.Id, patient.FullName, patient.Phone))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Appointment appointment, CancellationToken cancellationToken) =>
        await _db.Appointments.AddAsync(appointment, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);

    public async Task<IAppointmentTransaction> BeginDoctorTransactionAsync(
        Guid doctorId,
        CancellationToken cancellationToken)
    {
        var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // The locking read must be the first statement of the transaction so
            // that later reads see everything committed before the lock was granted.
            await _db.Database.ExecuteSqlAsync(
                $"SELECT `id` FROM `doctors` WHERE `id` = {doctorId.ToString("D")} FOR UPDATE",
                cancellationToken);
        }
        catch (Exception exception) when (IsLockFailure(exception))
        {
            await transaction.DisposeAsync();
            throw new ConflictException("The doctor's schedule is busy. Please try again.", ErrorCodes.ScheduleBusy);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }

        return new AppointmentTransaction(transaction);
    }

    private static bool IsLockFailure(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is MySqlException { Number: LockWaitTimeoutError or DeadlockError })
            {
                return true;
            }
        }

        return false;
    }

    private static IQueryable<AppointmentResponse> Project(IQueryable<Appointment> query) =>
        query.Select(appointment => new AppointmentResponse(
            appointment.Id,
            appointment.PatientId,
            appointment.Patient.FullName,
            appointment.Patient.Phone,
            appointment.DoctorId,
            appointment.Doctor.FullName,
            appointment.AppointmentDate,
            appointment.StartTime,
            appointment.EndTime,
            appointment.Reason,
            appointment.Status,
            appointment.Notes,
            appointment.CancellationReason,
            appointment.CreatedBy,
            appointment.CreatedAtUtc));
}
