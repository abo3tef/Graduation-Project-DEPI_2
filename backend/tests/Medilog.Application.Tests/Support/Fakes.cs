using Medilog.Application.Common;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.Appointments.Lookups;
using Medilog.Application.Features.Appointments.Responses;
using Medilog.Application.Interfaces;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;

namespace Medilog.Application.Tests.Support;

internal sealed class FakeCurrentUser : ICurrentUser
{
    public Guid UserId { get; set; }

    public UserRole Role { get; set; }
}

internal sealed class FakeClock : IClinicClock
{
    public DateTime LocalNow { get; set; } = new(2030, 1, 1, 8, 0, 0);

    public HashSet<DateTime> NonExistentTimes { get; } = [];

    public bool IsNonExistentLocalTime(DateTime localTime) => NonExistentTimes.Contains(localTime);
}

internal sealed class FakeTransaction : IAppointmentTransaction
{
    public Guid DoctorId { get; init; }

    public bool Committed { get; private set; }

    public bool Disposed { get; private set; }

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        Committed = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// In-memory <see cref="IAppointmentStore"/>. It mirrors the contract the real
/// store must honor (slot-holding filter, scoping by doctor ids) so the service
/// rules can be tested without a database.
/// </summary>
internal sealed class FakeAppointmentStore : IAppointmentStore
{
    public List<Doctor> Doctors { get; } = [];

    public List<Secretary> Secretaries { get; } = [];

    public List<Patient> Patients { get; } = [];

    public List<Appointment> Appointments { get; } = [];

    public Dictionary<Guid, HashSet<Guid>> Assignments { get; } = [];

    public List<FakeTransaction> Transactions { get; } = [];

    public AppointmentListCriteria? LastCriteria { get; private set; }

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

    public Task<DoctorInfo?> GetDoctorAsync(Guid doctorId, CancellationToken cancellationToken) =>
        Task.FromResult(Doctors.Where(d => d.Id == doctorId)
            .Select(d => (DoctorInfo?)new DoctorInfo(d.Id, d.AccountStatus == AccountStatus.Active)).FirstOrDefault());

    public Task<PatientInfo?> GetPatientAsync(Guid patientId, CancellationToken cancellationToken) =>
        Task.FromResult(Patients.Where(p => p.Id == patientId)
            .Select(p => (PatientInfo?)new PatientInfo(p.Id, p.DoctorId)).FirstOrDefault());

    public Task<bool> IsDoctorAssignedAsync(Guid secretaryId, Guid doctorId, CancellationToken cancellationToken) =>
        Task.FromResult(Assignments.TryGetValue(secretaryId, out var doctors) && doctors.Contains(doctorId));

    public Task<IReadOnlyList<Guid>> GetAssignedDoctorIdsAsync(Guid secretaryId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(
            Assignments.TryGetValue(secretaryId, out var doctors) ? doctors.ToList() : []);

    public Task<Guid?> GetAppointmentDoctorIdAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        Task.FromResult(Appointments.Where(a => a.Id == appointmentId).Select(a => (Guid?)a.DoctorId).FirstOrDefault());

    public Task<Appointment?> GetAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        Task.FromResult(Appointments.FirstOrDefault(a => a.Id == appointmentId));

    public Task<IReadOnlyList<Appointment>> GetSlotHoldingAppointmentsAsync(
        Guid doctorId,
        DateOnly date,
        Guid? excludeAppointmentId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Appointment>>(Appointments
            .Where(a => a.DoctorId == doctorId
                && a.AppointmentDate == date
                && a.Status is not (AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
                && (excludeAppointmentId is null || a.Id != excludeAppointmentId))
            .ToList());

    public Task<AppointmentResponse?> GetDetailsAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        Task.FromResult(Appointments.Where(a => a.Id == appointmentId).Select(ToResponse).FirstOrDefault());

    public Task<PagedResult<AppointmentResponse>> ListAsync(
        AppointmentListCriteria criteria,
        CancellationToken cancellationToken)
    {
        LastCriteria = criteria;

        var query = Appointments.Where(a => criteria.DoctorIds.Contains(a.DoctorId));

        if (criteria.PatientId.HasValue)
        {
            query = query.Where(a => a.PatientId == criteria.PatientId);
        }

        if (criteria.DateFrom.HasValue)
        {
            query = query.Where(a => a.AppointmentDate >= criteria.DateFrom);
        }

        if (criteria.DateTo.HasValue)
        {
            query = query.Where(a => a.AppointmentDate <= criteria.DateTo);
        }

        if (criteria.Status.HasValue)
        {
            query = query.Where(a => a.Status == criteria.Status);
        }

        var all = query.OrderBy(a => a.AppointmentDate).ThenBy(a => a.StartTime).ThenBy(a => a.Id).ToList();
        var items = all.Skip((criteria.PageNumber - 1) * criteria.PageSize).Take(criteria.PageSize)
            .Select(ToResponse).ToList();

        return Task.FromResult(new PagedResult<AppointmentResponse>(items, criteria.PageNumber, criteria.PageSize, all.Count));
    }

    public List<(string Kind, Guid? DoctorId, string? Search, int Limit)> LookupCalls { get; } = [];

    public Task<IReadOnlyList<DoctorLookupResponse>> GetActiveDoctorLookupsAsync(
        IReadOnlyCollection<Guid> doctorIds,
        CancellationToken cancellationToken)
    {
        LookupCalls.Add(("doctors", null, null, 0));

        return Task.FromResult<IReadOnlyList<DoctorLookupResponse>>(Doctors
            .Where(d => doctorIds.Contains(d.Id) && d.AccountStatus == AccountStatus.Active)
            .OrderBy(d => d.FullName).ThenBy(d => d.Id)
            .Select(d => new DoctorLookupResponse(d.Id, d.FullName, d.Specialization))
            .ToList());
    }

    public Task<IReadOnlyList<PatientLookupResponse>> SearchPatientsAsync(
        Guid doctorId,
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        LookupCalls.Add(("patients", doctorId, search, limit));

        var query = Patients.Where(p => p.DoctorId == doctorId);

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(p =>
                p.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (p.Phone?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return Task.FromResult<IReadOnlyList<PatientLookupResponse>>(query
            .OrderBy(p => p.FullName).ThenBy(p => p.Id)
            .Take(limit)
            .Select(p => new PatientLookupResponse(p.Id, p.FullName, p.Phone))
            .ToList());
    }

    public Task AddAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        Appointments.Add(appointment);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IAppointmentTransaction> BeginDoctorTransactionAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        var transaction = new FakeTransaction { DoctorId = doctorId };
        Transactions.Add(transaction);
        return Task.FromResult<IAppointmentTransaction>(transaction);
    }

    private AppointmentResponse ToResponse(Appointment a)
    {
        var patient = Patients.First(p => p.Id == a.PatientId);
        var doctor = Doctors.First(d => d.Id == a.DoctorId);

        return new AppointmentResponse(
            a.Id, a.PatientId, patient.FullName, patient.Phone, a.DoctorId, doctor.FullName,
            a.AppointmentDate, a.StartTime, a.EndTime, a.Reason, a.Status, a.Notes,
            a.CancellationReason, a.CreatedBy, a.CreatedAtUtc);
    }
}
