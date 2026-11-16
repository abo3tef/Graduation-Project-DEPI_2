using Medilog.Application.Common;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.Payments.Responses;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;

namespace Medilog.Application.Interfaces;

/// <summary>
/// Minimal facts about an appointment that a payment is recorded against. No
/// medical data is exposed through this port.
/// </summary>
public sealed record PaymentAppointmentInfo(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    DateOnly AppointmentDate,
    TimeOnly StartTime,
    bool HasPayment);

/// <summary>
/// Persistence port for the payment use cases. Implemented in Infrastructure so
/// the Application layer stays free of EF Core.
/// </summary>
public interface IPaymentStore
{
    Task<ActorInfo?> GetActorAsync(UserRole role, Guid userId, CancellationToken cancellationToken);

    /// <summary>True when the secretary is assigned to the doctor (<c>doctor_secretaries</c>).</summary>
    Task<bool> IsDoctorAssignedAsync(Guid secretaryId, Guid doctorId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetAssignedDoctorIdsAsync(Guid secretaryId, CancellationToken cancellationToken);

    Task<PaymentAppointmentInfo?> GetAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken);

    /// <summary>Returns only the doctor id of a payment, without tracking it.</summary>
    Task<Guid?> GetPaymentDoctorIdAsync(Guid paymentId, CancellationToken cancellationToken);

    /// <summary>Loads a tracked payment for modification.</summary>
    Task<Payment?> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a non-tracked payment with its patient and appointment so the
    /// response can include the patient name and the appointment date.
    /// </summary>
    Task<Payment?> GetPaymentWithDetailsAsync(Guid paymentId, CancellationToken cancellationToken);

    Task<PagedResult<PaymentResponse>> ListAsync(
        PaymentListCriteria criteria,
        CancellationToken cancellationToken);

    /// <summary>
    /// Appointments in the given doctors' scope that can still be billed: the
    /// status is not <see cref="AppointmentStatus.Cancelled"/> or
    /// <see cref="AppointmentStatus.NoShow"/> and no payment exists yet,
    /// optionally filtered by patient name and bounded by <paramref name="limit"/>.
    /// </summary>
    Task<IReadOnlyList<PayableAppointmentResponse>> GetPayableAppointmentsAsync(
        IReadOnlyCollection<Guid> doctorIds,
        string? search,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Adds the payment; a concurrent duplicate for the same appointment becomes a 409.</summary>
    Task AddAsync(Payment payment, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
