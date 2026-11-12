using Medilog.Application.Common;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.Appointments.Responses;
using Medilog.Domain.Entities;

namespace Medilog.Application.Interfaces;

/// <summary>Minimal facts about an acting doctor or secretary.</summary>
public sealed record ActorInfo(Guid Id, bool IsActive);

/// <summary>Minimal facts about a doctor needed to book an appointment.</summary>
public sealed record DoctorInfo(Guid Id, bool IsActive);

/// <summary>
/// Minimal administrative facts about a patient. No medical data is exposed
/// through this port.
/// </summary>
public sealed record PatientInfo(Guid Id, Guid? DoctorId);

/// <summary>
/// A database transaction holding the per-doctor booking lock. Disposing it
/// without calling <see cref="CommitAsync"/> rolls everything back.
/// </summary>
public interface IAppointmentTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Persistence port for the appointment use cases. Implemented in Infrastructure
/// so the Application layer stays free of EF Core.
/// </summary>
public interface IAppointmentStore
{
    Task<ActorInfo?> GetActorAsync(UserRole role, Guid userId, CancellationToken cancellationToken);

    Task<DoctorInfo?> GetDoctorAsync(Guid doctorId, CancellationToken cancellationToken);

    Task<PatientInfo?> GetPatientAsync(Guid patientId, CancellationToken cancellationToken);

    /// <summary>True when the secretary is assigned to the doctor (<c>doctor_secretaries</c>).</summary>
    Task<bool> IsDoctorAssignedAsync(Guid secretaryId, Guid doctorId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetAssignedDoctorIdsAsync(Guid secretaryId, CancellationToken cancellationToken);

    /// <summary>Returns only the doctor id of an appointment, without tracking it.</summary>
    Task<Guid?> GetAppointmentDoctorIdAsync(Guid appointmentId, CancellationToken cancellationToken);

    /// <summary>Loads a tracked appointment for modification.</summary>
    Task<Appointment?> GetAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken);

    /// <summary>
    /// Appointments of one doctor on one date that still hold their slot
    /// (Cancelled and No-Show excluded), optionally excluding one appointment.
    /// </summary>
    Task<IReadOnlyList<Appointment>> GetSlotHoldingAppointmentsAsync(
        Guid doctorId,
        DateOnly date,
        Guid? excludeAppointmentId,
        CancellationToken cancellationToken);

    Task<AppointmentResponse?> GetDetailsAsync(Guid appointmentId, CancellationToken cancellationToken);

    Task<PagedResult<AppointmentResponse>> ListAsync(
        AppointmentListCriteria criteria,
        CancellationToken cancellationToken);

    Task AddAsync(Appointment appointment, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts a transaction and takes a row lock on the doctor so that writes for
    /// the same doctor are serialized. Different doctors do not block each other.
    /// </summary>
    Task<IAppointmentTransaction> BeginDoctorTransactionAsync(Guid doctorId, CancellationToken cancellationToken);
}
