using Medilog.Application.Common;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Features.Dashboard.Contracts;
using Medilog.Application.Interfaces;
using Medilog.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Medilog.Application.Features.Dashboard;

/// <summary>
/// Implements the doctor dashboard service by aggregating persisted appointment,
/// visit, and patient data scoped strictly to the acting doctor.
/// </summary>
public sealed class DoctorDashboardService : IDoctorDashboardService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IClinicClock _clinicClock;

    public DoctorDashboardService(
        IApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IClinicClock clinicClock)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _clinicClock = clinicClock;
    }

    public async Task<DoctorDashboardResponse> GetDoctorDashboardAsync(CancellationToken cancellationToken = default)
    {
        var doctorId = RequireActingDoctorId();

        var doctor = await _dbContext.Doctors
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == doctorId, cancellationToken)
            ?? throw new NotFoundException("The doctor was not found.", ErrorCodes.DoctorNotFound);

        var todayLocal = DateOnly.FromDateTime(_clinicClock.LocalNow);

        var appointmentsQuery = _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.DoctorId == doctorId && a.AppointmentDate == todayLocal);

        // Active appointments today (excluding Cancelled per approved assumption)
        var todayAppointmentsCount = await appointmentsQuery
            .CountAsync(a => a.Status != AppointmentStatus.Cancelled, cancellationToken);

        // Full schedule for today including Cancelled so the doctor sees their status
        var todayAppointments = await appointmentsQuery
            .OrderBy(a => a.StartTime)
            .ThenBy(a => a.Id)
            .Select(a => new TodayAppointmentSummaryDto(
                a.Id,
                a.PatientId,
                a.Patient.FullName,
                a.StartTime,
                a.EndTime,
                a.Reason,
                a.Status.ToString()))
            .ToListAsync(cancellationToken);

        // Today's visits: consultation instant within today's wall-clock boundaries in clinic timezone [startUtc, endUtc)
        var (startUtc, endUtc) = GetDayUtcRange(todayLocal, _clinicClock.TimeZone);
        var todayVisitsCount = await _dbContext.MedicalVisits
            .CountAsync(v => v.DoctorId == doctorId && v.VisitDate >= startUtc && v.VisitDate < endUtc, cancellationToken);

        // Total visits for this doctor across all time
        var totalVisitsCount = await _dbContext.MedicalVisits
            .CountAsync(v => v.DoctorId == doctorId, cancellationToken);

        // Total patients in doctor's workspace (D-17: Patient.DoctorId == doctorId)
        var totalPatientsCount = await _dbContext.Patients
            .CountAsync(p => p.DoctorId == doctorId, cancellationToken);

        // Total revenue: Isolated query returning null until Q-01 is decided and payments are implemented
        var totalRevenue = await GetTotalRevenueAsync(doctorId, cancellationToken);

        var metrics = new DoctorDashboardMetricsDto(
            todayAppointmentsCount,
            todayVisitsCount,
            totalVisitsCount,
            totalPatientsCount,
            totalRevenue);

        return new DoctorDashboardResponse(
            doctor.Id,
            doctor.FullName,
            doctor.Specialization,
            metrics,
            todayAppointments);
    }

    private Guid RequireActingDoctorId()
    {
        if (_currentUser.Role != UserRole.Doctor || _currentUser.UserId is not { } doctorId || doctorId == Guid.Empty)
        {
            throw new ForbiddenException("Only an authenticated doctor may access this endpoint.");
        }

        return doctorId;
    }

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) GetDayUtcRange(DateOnly date, TimeZoneInfo timeZone)
    {
        var startUtc = GetUtcStartOfDay(date, timeZone);
        var endUtc = GetUtcStartOfDay(date.AddDays(1), timeZone);

        return (startUtc, endUtc);
    }

    private static DateTimeOffset GetUtcStartOfDay(DateOnly date, TimeZoneInfo timeZone)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);

        // If the start of the day falls in a DST gap (e.g. 00:00 skipped to 01:00),
        // move forward to the first valid wall-clock moment in this day.
        while (timeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        if (timeZone.IsAmbiguousTime(local))
        {
            // When falling back (DST ends), two offsets exist. The earlier UTC instant
            // corresponding to the beginning of the local day has the larger offset.
            var offsets = timeZone.GetAmbiguousTimeOffsets(local);
            var maxOffset = offsets.Max();
            return new DateTimeOffset(local, maxOffset).ToUniversalTime();
        }

        var utc = TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    private static Task<decimal?> GetTotalRevenueAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        // Reserved for Q-01: No payment entity or table exists yet.
        // When Q-01 is decided and Payments are implemented, aggregate payments for doctorId here.
        return Task.FromResult<decimal?>(null);
    }
}

