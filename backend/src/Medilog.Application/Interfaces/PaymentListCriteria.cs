using Medilog.Domain.Enums;

namespace Medilog.Application.Interfaces;

/// <summary>
/// Already-scoped list criteria handed to the store. <see cref="DoctorIds"/> is
/// the set of doctors the caller is allowed to see; the store must never return
/// payments of any other doctor.
/// </summary>
public sealed record PaymentListCriteria(
    IReadOnlyCollection<Guid> DoctorIds,
    string? Search,
    PaymentStatus? Status,
    PaymentMethod? Method,
    Guid? PatientId,
    Guid? AppointmentId,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    int PageNumber,
    int PageSize);
