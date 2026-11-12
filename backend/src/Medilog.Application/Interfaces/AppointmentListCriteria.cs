using Medilog.Domain.Enums;

namespace Medilog.Application.Interfaces;

/// <summary>
/// Already-scoped list criteria handed to the store. <see cref="DoctorIds"/> is
/// the set of doctors the caller is allowed to see; the store must never return
/// appointments of any other doctor.
/// </summary>
public sealed record AppointmentListCriteria(
    IReadOnlyCollection<Guid> DoctorIds,
    Guid? PatientId,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    AppointmentStatus? Status,
    int PageNumber,
    int PageSize);
