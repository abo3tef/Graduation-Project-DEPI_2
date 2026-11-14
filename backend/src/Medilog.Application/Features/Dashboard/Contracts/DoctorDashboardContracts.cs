namespace Medilog.Application.Features.Dashboard.Contracts;

/// <summary>
/// A summary of a single appointment on the doctor's schedule for today.
/// </summary>
public sealed record TodayAppointmentSummaryDto(
    Guid AppointmentId,
    Guid PatientId,
    string PatientName,
    TimeOnly StartTime,
    TimeOnly? EndTime,
    string? Reason,
    string Status);

/// <summary>
/// Metric counts and aggregated indicators for the acting doctor's clinic.
/// </summary>
public sealed record DoctorDashboardMetricsDto(
    int TodayAppointmentsCount,
    int TodayVisitsCount,
    int TotalVisitsCount,
    int TotalPatientsCount,
    decimal? TotalRevenue);

/// <summary>
/// Full dashboard response returned to the authenticated doctor.
/// </summary>
public sealed record DoctorDashboardResponse(
    Guid DoctorId,
    string DoctorName,
    string? Specialization,
    DoctorDashboardMetricsDto Metrics,
    IReadOnlyList<TodayAppointmentSummaryDto> TodayAppointments);

