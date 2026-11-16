using Medilog.Application.Common;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.Payments.Responses;
using Medilog.Application.Interfaces;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Medilog.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace Medilog.Infrastructure.Persistence.Payments;

/// <summary>
/// EF Core implementation of the Application's <see cref="IPaymentStore"/>.
/// All filtering, counting, ordering and paging run in the database, and read
/// models are projected to the minimum columns (no medical data is loaded).
/// </summary>
internal sealed class PaymentStore : IPaymentStore
{
    private const int DuplicateKeyError = 1062;

    private readonly MedilogDbContext _db;

    public PaymentStore(MedilogDbContext db)
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

    public Task<PaymentAppointmentInfo?> GetAppointmentAsync(
        Guid appointmentId,
        CancellationToken cancellationToken) =>
        _db.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.Id == appointmentId)
            .Select(appointment => new PaymentAppointmentInfo(
                appointment.Id,
                appointment.PatientId,
                appointment.Patient.FullName,
                appointment.DoctorId,
                appointment.AppointmentDate,
                appointment.StartTime,
                _db.Payments.Any(payment => payment.AppointmentId == appointment.Id)))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Guid?> GetPaymentDoctorIdAsync(Guid paymentId, CancellationToken cancellationToken) =>
        _db.Payments
            .AsNoTracking()
            .Where(payment => payment.Id == paymentId)
            .Select(payment => (Guid?)payment.DoctorId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Payment?> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken) =>
        _db.Payments.FirstOrDefaultAsync(payment => payment.Id == paymentId, cancellationToken);

    public Task<Payment?> GetPaymentWithDetailsAsync(Guid paymentId, CancellationToken cancellationToken) =>
        _db.Payments
            .AsNoTracking()
            .Include(payment => payment.Patient)
            .Include(payment => payment.Appointment)
            .FirstOrDefaultAsync(payment => payment.Id == paymentId, cancellationToken);

    public async Task<PagedResult<PaymentResponse>> ListAsync(
        PaymentListCriteria criteria,
        CancellationToken cancellationToken)
    {
        var doctorIds = criteria.DoctorIds.ToArray();

        var query = _db.Payments
            .AsNoTracking()
            .Where(payment => doctorIds.Contains(payment.DoctorId));

        if (criteria.PatientId.HasValue)
        {
            query = query.Where(payment => payment.PatientId == criteria.PatientId.Value);
        }

        if (criteria.AppointmentId.HasValue)
        {
            query = query.Where(payment => payment.AppointmentId == criteria.AppointmentId.Value);
        }

        if (criteria.Status.HasValue)
        {
            query = query.Where(payment => payment.Status == criteria.Status.Value);
        }

        if (criteria.Method.HasValue)
        {
            query = query.Where(payment => payment.Method == criteria.Method.Value);
        }

        if (criteria.DateFrom.HasValue)
        {
            query = query.Where(payment => payment.Appointment.AppointmentDate >= criteria.DateFrom.Value);
        }

        if (criteria.DateTo.HasValue)
        {
            query = query.Where(payment => payment.Appointment.AppointmentDate <= criteria.DateTo.Value);
        }

        if (!string.IsNullOrEmpty(criteria.Search))
        {
            query = Guid.TryParse(criteria.Search, out var id)
                ? query.Where(payment => payment.Id == id || payment.AppointmentId == id)
                : query.Where(payment => payment.Patient.FullName.Contains(criteria.Search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = (long)(criteria.PageNumber - 1) * criteria.PageSize;

        if (skip >= totalCount)
        {
            return new PagedResult<PaymentResponse>([], criteria.PageNumber, criteria.PageSize, totalCount);
        }

        // Deterministic order: the id is a stable tie-breaker when paid times collide
        // (and for rows that are not paid yet, where paid_at_utc is null).
        var items = await Project(query
                .OrderByDescending(payment => payment.PaidAtUtc)
                .ThenByDescending(payment => payment.Id)
                .Skip((int)skip)
                .Take(criteria.PageSize))
            .ToListAsync(cancellationToken);

        return new PagedResult<PaymentResponse>(items, criteria.PageNumber, criteria.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<PayableAppointmentResponse>> GetPayableAppointmentsAsync(
        IReadOnlyCollection<Guid> doctorIds,
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        var ids = doctorIds.ToArray();

        // Only appointments that can still be billed: a cancelled or no-show
        // appointment must never reach the payment picker.
        var query = _db.Appointments
            .AsNoTracking()
            .Where(appointment =>
                ids.Contains(appointment.DoctorId)
                && appointment.Status != AppointmentStatus.Cancelled
                && appointment.Status != AppointmentStatus.NoShow
                && !_db.Payments.Any(payment => payment.AppointmentId == appointment.Id));

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(appointment => appointment.Patient.FullName.Contains(search));
        }

        return await query
            .OrderBy(appointment => appointment.AppointmentDate)
            .ThenBy(appointment => appointment.StartTime)
            .ThenBy(appointment => appointment.Id)
            .Take(limit)
            .Select(appointment => new PayableAppointmentResponse(
                appointment.Id,
                appointment.PatientId,
                appointment.Patient.FullName,
                appointment.DoctorId,
                appointment.AppointmentDate,
                appointment.StartTime))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken) =>
        await _db.Payments.AddAsync(payment, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // The unique index on appointment_id is the final guard against two
            // concurrent creates for the same appointment.
            throw new ConflictException(
                "A payment already exists for this appointment.",
                ErrorCodes.PaymentAlreadyExists);
        }
    }

    private static bool IsDuplicateKey(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is MySqlException { Number: DuplicateKeyError })
            {
                return true;
            }
        }

        return false;
    }

    private static IQueryable<PaymentResponse> Project(IQueryable<Payment> query) =>
        query.Select(payment => new PaymentResponse(
            payment.Id,
            payment.PatientId,
            payment.Patient.FullName,
            payment.DoctorId,
            payment.AppointmentId,
            payment.Appointment.AppointmentDate,
            payment.Amount,
            payment.Method,
            payment.Status,
            payment.PaidAtUtc,
            payment.Notes));
}
