using Medilog.Application.Common;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.Payments.Responses;
using Medilog.Application.Interfaces;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;

namespace Medilog.Application.Tests.Support;

/// <summary>
/// In-memory <see cref="IPaymentStore"/>. It mirrors the contract the real store
/// must honor (doctor scoping, search, deterministic ordering, payable
/// appointments) so the service rules can be tested without a database.
/// </summary>
internal sealed class FakePaymentStore : IPaymentStore
{
    public List<Doctor> Doctors { get; } = [];

    public List<Secretary> Secretaries { get; } = [];

    public List<Patient> Patients { get; } = [];

    public List<Appointment> Appointments { get; } = [];

    public List<Payment> Payments { get; } = [];

    public Dictionary<Guid, HashSet<Guid>> Assignments { get; } = [];

    public PaymentListCriteria? LastCriteria { get; private set; }

    public Task<ActorInfo?> GetActorAsync(UserRole role, Guid userId, CancellationToken cancellationToken)
    {
        ActorInfo? actor = role switch
        {
            UserRole.Doctor => Doctors.Where(d => d.Id == userId)
                .Select(d => new ActorInfo(d.Id, d.AccountStatus == AccountStatus.Active)).FirstOrDefault(),
            UserRole.Secretary => Secretaries.Where(s => s.Id == userId)
                .Select(s => new ActorInfo(s.Id, s.AccountStatus == AccountStatus.Active)).FirstOrDefault(),
            _ => null
        };

        return Task.FromResult(actor);
    }

    public Task<bool> IsDoctorAssignedAsync(Guid secretaryId, Guid doctorId, CancellationToken cancellationToken) =>
        Task.FromResult(Assignments.TryGetValue(secretaryId, out var doctors) && doctors.Contains(doctorId));

    public Task<IReadOnlyList<Guid>> GetAssignedDoctorIdsAsync(Guid secretaryId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(
            Assignments.TryGetValue(secretaryId, out var doctors) ? doctors.ToList() : []);

    public Task<PaymentAppointmentInfo?> GetAppointmentAsync(
        Guid appointmentId,
        CancellationToken cancellationToken)
    {
        var appointment = Appointments.FirstOrDefault(a => a.Id == appointmentId);

        if (appointment is null)
        {
            return Task.FromResult<PaymentAppointmentInfo?>(null);
        }

        var patient = Patients.First(p => p.Id == appointment.PatientId);

        return Task.FromResult<PaymentAppointmentInfo?>(new PaymentAppointmentInfo(
            appointment.Id,
            appointment.PatientId,
            patient.FullName,
            appointment.DoctorId,
            appointment.AppointmentDate,
            appointment.StartTime,
            Payments.Any(p => p.AppointmentId == appointment.Id)));
    }

    public Task<Guid?> GetPaymentDoctorIdAsync(Guid paymentId, CancellationToken cancellationToken) =>
        Task.FromResult(Payments.Where(p => p.Id == paymentId).Select(p => (Guid?)p.DoctorId).FirstOrDefault());

    public Task<Payment?> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken) =>
        Task.FromResult(Payments.FirstOrDefault(p => p.Id == paymentId));

    public Task<Payment?> GetPaymentWithDetailsAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = Payments.FirstOrDefault(p => p.Id == paymentId);

        if (payment is not null)
        {
            payment.Patient = Patients.First(p => p.Id == payment.PatientId);
            payment.Appointment = Appointments.First(a => a.Id == payment.AppointmentId);
        }

        return Task.FromResult(payment);
    }

    public Task<PagedResult<PaymentResponse>> ListAsync(
        PaymentListCriteria criteria,
        CancellationToken cancellationToken)
    {
        LastCriteria = criteria;

        var query = Payments.Where(p => criteria.DoctorIds.Contains(p.DoctorId));

        if (criteria.PatientId.HasValue)
        {
            query = query.Where(p => p.PatientId == criteria.PatientId);
        }

        if (criteria.AppointmentId.HasValue)
        {
            query = query.Where(p => p.AppointmentId == criteria.AppointmentId);
        }

        if (criteria.Status.HasValue)
        {
            query = query.Where(p => p.Status == criteria.Status);
        }

        if (criteria.Method.HasValue)
        {
            query = query.Where(p => p.Method == criteria.Method);
        }

        var appointments = Appointments.ToDictionary(a => a.Id);

        if (criteria.DateFrom.HasValue)
        {
            query = query.Where(p => appointments[p.AppointmentId].AppointmentDate >= criteria.DateFrom);
        }

        if (criteria.DateTo.HasValue)
        {
            query = query.Where(p => appointments[p.AppointmentId].AppointmentDate <= criteria.DateTo);
        }

        if (!string.IsNullOrEmpty(criteria.Search))
        {
            query = Guid.TryParse(criteria.Search, out var id)
                ? query.Where(p => p.Id == id || p.AppointmentId == id)
                : query.Where(p => Patients.First(patient => patient.Id == p.PatientId)
                    .FullName.Contains(criteria.Search, StringComparison.OrdinalIgnoreCase));
        }

        var all = query
            .OrderByDescending(p => p.PaidAtUtc)
            .ThenByDescending(p => p.Id)
            .ToList();

        var items = all
            .Skip((criteria.PageNumber - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(new PagedResult<PaymentResponse>(
            items,
            criteria.PageNumber,
            criteria.PageSize,
            all.Count));
    }

    public Task<IReadOnlyList<PayableAppointmentResponse>> GetPayableAppointmentsAsync(
        IReadOnlyCollection<Guid> doctorIds,
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        var paidAppointmentIds = Payments.Select(p => p.AppointmentId).ToHashSet();

        var query = Appointments.Where(a =>
            doctorIds.Contains(a.DoctorId)
            && a.Status != AppointmentStatus.Cancelled
            && a.Status != AppointmentStatus.NoShow
            && !paidAppointmentIds.Contains(a.Id));

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(a => Patients.First(p => p.Id == a.PatientId)
                .FullName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyList<PayableAppointmentResponse>>(query
            .OrderBy(a => a.AppointmentDate)
            .ThenBy(a => a.StartTime)
            .ThenBy(a => a.Id)
            .Take(limit)
            .Select(a => new PayableAppointmentResponse(
                a.Id,
                a.PatientId,
                Patients.First(p => p.Id == a.PatientId).FullName,
                a.DoctorId,
                a.AppointmentDate,
                a.StartTime))
            .ToList());
    }

    public Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        Payments.Add(payment);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private PaymentResponse ToResponse(Payment payment)
    {
        var patient = Patients.First(p => p.Id == payment.PatientId);
        var appointment = Appointments.First(a => a.Id == payment.AppointmentId);

        return new PaymentResponse(
            payment.Id,
            payment.PatientId,
            patient.FullName,
            payment.DoctorId,
            payment.AppointmentId,
            appointment.AppointmentDate,
            payment.Amount,
            payment.Method,
            payment.Status,
            payment.PaidAtUtc,
            payment.Notes);
    }
}
