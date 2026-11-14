using Medilog.Application.Common;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Features.Dashboard;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Medilog.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Medilog.Application.Tests.Services;

public sealed class DoctorDashboardServiceTests : IDisposable
{
    private readonly MedilogDbContext _dbContext;
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeClock _clock;
    private readonly DoctorDashboardService _service;

    public DoctorDashboardServiceTests()
    {
        var options = new DbContextOptionsBuilder<MedilogDbContext>()
            .UseInMemoryDatabase($"medilog-dashboard-{Guid.NewGuid():N}")
            .Options;

        _dbContext = new MedilogDbContext(options);
        _currentUser = new FakeCurrentUser();
        _clock = new FakeClock
        {
            LocalNow = new DateTime(2026, 11, 13, 10, 0, 0),
            TimeZone = TimeZoneInfo.Utc
        };

        _service = new DoctorDashboardService(_dbContext, _currentUser, _clock);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetDoctorDashboardAsync_ReturnsAggregatedMetrics_ForActingDoctor()
    {
        // Arrange
        var doctorId = Guid.NewGuid();
        var otherDoctorId = Guid.NewGuid();

        var doctor = new Doctor
        {
            Id = doctorId,
            FullName = "Dr. Ahmed Mohamed",
            Specialization = "Cardiology",
            Email = "ahmed@example.com",
            Phone = "01000000001",
            AccountStatus = AccountStatus.Active
        };

        var otherDoctor = new Doctor
        {
            Id = otherDoctorId,
            FullName = "Dr. Omar Khaled",
            Specialization = "Dermatology",
            Email = "omar@example.com",
            Phone = "01000000002",
            AccountStatus = AccountStatus.Active
        };

        var patient1 = new Patient
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            FullName = "Mahmoud Hassan",
            DateOfBirth = new DateOnly(1990, 5, 10),
            Gender = Gender.Male
        };

        var patient2 = new Patient
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            FullName = "Fatma Ali",
            DateOfBirth = new DateOnly(1995, 8, 20),
            Gender = Gender.Female
        };

        var otherPatient = new Patient
        {
            Id = Guid.NewGuid(),
            DoctorId = otherDoctorId,
            FullName = "Sara Tarek",
            DateOfBirth = new DateOnly(2000, 1, 1),
            Gender = Gender.Female
        };

        var today = new DateOnly(2026, 11, 13);
        var yesterday = new DateOnly(2026, 11, 12);
        var tomorrow = new DateOnly(2026, 11, 14);

        // Appointments for acting doctor
        var appt1 = new Appointment
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            PatientId = patient1.Id,
            Patient = patient1,
            AppointmentDate = today,
            StartTime = new TimeOnly(11, 0),
            EndTime = new TimeOnly(11, 30),
            Status = AppointmentStatus.Scheduled,
            Reason = "Checkup"
        };

        var appt2 = new Appointment
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            PatientId = patient2.Id,
            Patient = patient2,
            AppointmentDate = today,
            StartTime = new TimeOnly(9, 30),
            EndTime = new TimeOnly(10, 0),
            Status = AppointmentStatus.Confirmed,
            Reason = "Consultation"
        };

        var appt3 = new Appointment
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            PatientId = patient1.Id,
            Patient = patient1,
            AppointmentDate = today,
            StartTime = new TimeOnly(12, 0),
            EndTime = new TimeOnly(12, 30),
            Status = AppointmentStatus.Completed,
            Reason = "Followup"
        };

        var apptCancelled = new Appointment
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            PatientId = patient2.Id,
            Patient = patient2,
            AppointmentDate = today,
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(14, 30),
            Status = AppointmentStatus.Cancelled,
            Reason = "Patient requested cancel"
        };

        var apptTomorrow = new Appointment
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            PatientId = patient1.Id,
            Patient = patient1,
            AppointmentDate = tomorrow,
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(10, 30),
            Status = AppointmentStatus.Confirmed
        };

        // Other doctor's appointment today (must not leak)
        var otherDoctorAppt = new Appointment
        {
            Id = Guid.NewGuid(),
            DoctorId = otherDoctorId,
            PatientId = otherPatient.Id,
            Patient = otherPatient,
            AppointmentDate = today,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 30),
            Status = AppointmentStatus.Confirmed
        };

        // Visits for acting doctor
        var visitToday = new MedicalVisit
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            PatientId = patient1.Id,
            VisitDate = new DateTimeOffset(2026, 11, 13, 8, 30, 0, TimeSpan.Zero),
            ChiefComplaint = "Chest pain"
        };

        var visitYesterday = new MedicalVisit
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            PatientId = patient2.Id,
            VisitDate = new DateTimeOffset(2026, 11, 12, 14, 0, 0, TimeSpan.Zero),
            ChiefComplaint = "Routine checkup"
        };

        // Other doctor's visit today (must not leak)
        var otherVisitToday = new MedicalVisit
        {
            Id = Guid.NewGuid(),
            DoctorId = otherDoctorId,
            PatientId = otherPatient.Id,
            VisitDate = new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero),
            ChiefComplaint = "Skin rash"
        };

        await _dbContext.Doctors.AddRangeAsync(doctor, otherDoctor);
        await _dbContext.Patients.AddRangeAsync(patient1, patient2, otherPatient);
        await _dbContext.Appointments.AddRangeAsync(appt1, appt2, appt3, apptCancelled, apptTomorrow, otherDoctorAppt);
        await _dbContext.MedicalVisits.AddRangeAsync(visitToday, visitYesterday, otherVisitToday);
        await _dbContext.SaveChangesAsync();

        _currentUser.UserId = doctorId;
        _currentUser.Role = UserRole.Doctor;

        // Act
        var result = await _service.GetDoctorDashboardAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(doctorId, result.DoctorId);
        Assert.Equal("Dr. Ahmed Mohamed", result.DoctorName);
        Assert.Equal("Cardiology", result.Specialization);

        // Metrics verification
        // Active appointments today: appt1 (Scheduled) + appt2 (Confirmed) + appt3 (Completed) = 3 (Cancelled excluded)
        Assert.Equal(3, result.Metrics.TodayAppointmentsCount);

        // Today's visits: only visitToday
        Assert.Equal(1, result.Metrics.TodayVisitsCount);

        // Total visits for doctor: visitToday + visitYesterday = 2
        Assert.Equal(2, result.Metrics.TotalVisitsCount);

        // Total patients in doctor's workspace: patient1 + patient2 = 2
        Assert.Equal(2, result.Metrics.TotalPatientsCount);

        // Total revenue must be null (awaiting Q-01)
        Assert.Null(result.Metrics.TotalRevenue);

        // Schedule table contains all 4 today's appointments (including cancelled), ordered by StartTime
        Assert.Equal(4, result.TodayAppointments.Count);
        Assert.Equal(appt2.Id, result.TodayAppointments[0].AppointmentId); // 09:30
        Assert.Equal("Fatma Ali", result.TodayAppointments[0].PatientName);
        Assert.Equal("Confirmed", result.TodayAppointments[0].Status);

        Assert.Equal(appt1.Id, result.TodayAppointments[1].AppointmentId); // 11:00
        Assert.Equal("Mahmoud Hassan", result.TodayAppointments[1].PatientName);
        Assert.Equal("Scheduled", result.TodayAppointments[1].Status);

        Assert.Equal(appt3.Id, result.TodayAppointments[2].AppointmentId); // 12:00
        Assert.Equal("Completed", result.TodayAppointments[2].Status);

        Assert.Equal(apptCancelled.Id, result.TodayAppointments[3].AppointmentId); // 14:00
        Assert.Equal("Cancelled", result.TodayAppointments[3].Status);
    }

    [Fact]
    public async Task GetDoctorDashboardAsync_WhenDoctorHasNoData_ReturnsZeroMetricsAndEmptySchedule()
    {
        // Arrange
        var doctorId = Guid.NewGuid();
        var doctor = new Doctor
        {
            Id = doctorId,
            FullName = "Dr. New Doctor",
            Specialization = "Pediatrics",
            Email = "new@example.com",
            Phone = "01000000009",
            AccountStatus = AccountStatus.Active
        };

        await _dbContext.Doctors.AddAsync(doctor);
        await _dbContext.SaveChangesAsync();

        _currentUser.UserId = doctorId;
        _currentUser.Role = UserRole.Doctor;

        // Act
        var result = await _service.GetDoctorDashboardAsync();

        // Assert
        Assert.Equal(doctorId, result.DoctorId);
        Assert.Equal("Dr. New Doctor", result.DoctorName);
        Assert.Equal(0, result.Metrics.TodayAppointmentsCount);
        Assert.Equal(0, result.Metrics.TodayVisitsCount);
        Assert.Equal(0, result.Metrics.TotalVisitsCount);
        Assert.Equal(0, result.Metrics.TotalPatientsCount);
        Assert.Null(result.Metrics.TotalRevenue);
        Assert.Empty(result.TodayAppointments);
    }

    [Fact]
    public async Task GetDoctorDashboardAsync_WhenUserIsNotDoctor_ThrowsForbiddenException()
    {
        // Arrange
        _currentUser.UserId = Guid.NewGuid();
        _currentUser.Role = UserRole.Secretary;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => _service.GetDoctorDashboardAsync());
        Assert.Equal("Only an authenticated doctor may access this endpoint.", ex.Message);
    }

    [Fact]
    public async Task GetDoctorDashboardAsync_WhenDoctorNotFoundInDatabase_ThrowsNotFoundException()
    {
        // Arrange
        var nonExistentDoctorId = Guid.NewGuid();
        _currentUser.UserId = nonExistentDoctorId;
        _currentUser.Role = UserRole.Doctor;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _service.GetDoctorDashboardAsync());
        Assert.Equal("The doctor was not found.", ex.Message);
        Assert.Equal(ErrorCodes.DoctorNotFound, ex.Code);
    }

    [Fact]
    public async Task GetDoctorDashboardAsync_WithCairoTimeZone_NormalDay_HandlesBoundariesAccurately()
    {
        // Arrange
        var cairoZone = ResolveCairoTimeZone();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        var doctor = new Doctor
        {
            Id = doctorId,
            FullName = "Dr. Cairo Doctor",
            Email = "cairo@example.com",
            AccountStatus = AccountStatus.Active
        };

        var patient = new Patient
        {
            Id = patientId,
            DoctorId = doctorId,
            FullName = "Tamer Hosny",
            DateOfBirth = new DateOnly(1985, 1, 1),
            Gender = Gender.Male
        };

        // Normal day in winter (standard time, UTC+2): 2026-11-13
        var normalDate = new DateOnly(2026, 11, 13);
        _clock.LocalNow = normalDate.ToDateTime(new TimeOnly(14, 0, 0));
        _clock.TimeZone = cairoZone;

        // In Cairo UTC+2:
        // 2026-11-13 00:00:00 local = 2026-11-12 22:00:00 UTC
        // 2026-11-13 23:59:59.9 local = 2026-11-13 21:59:59.9 UTC
        // 2026-11-14 00:00:00 local = 2026-11-13 22:00:00 UTC (tomorrow start, boundary exclusion)
        var visitExactlyAtMidnight = new MedicalVisit
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            PatientId = patientId,
            VisitDate = new DateTimeOffset(2026, 11, 12, 22, 0, 0, TimeSpan.Zero)
        };

        var visitAtEndOfDay = new MedicalVisit
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            PatientId = patientId,
            VisitDate = new DateTimeOffset(2026, 11, 13, 21, 59, 59, 900, TimeSpan.Zero)
        };

        var visitAtTomorrowMidnight = new MedicalVisit
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            PatientId = patientId,
            VisitDate = new DateTimeOffset(2026, 11, 13, 22, 0, 0, TimeSpan.Zero)
        };

        await _dbContext.Doctors.AddAsync(doctor);
        await _dbContext.Patients.AddAsync(patient);
        await _dbContext.MedicalVisits.AddRangeAsync(visitExactlyAtMidnight, visitAtEndOfDay, visitAtTomorrowMidnight);
        await _dbContext.SaveChangesAsync();

        _currentUser.UserId = doctorId;
        _currentUser.Role = UserRole.Doctor;

        // Act
        var result = await _service.GetDoctorDashboardAsync();

        // Assert
        // Half-open interval [startOfToday, startOfTomorrow) includes midnight and 23:59:59.9, but excludes tomorrow 00:00:00
        Assert.Equal(2, result.Metrics.TodayVisitsCount);
    }

    [Fact]
    public async Task GetDoctorDashboardAsync_WithCairoTimeZone_DstTransitionDay_HandlesInvalidMidnightSafely()
    {
        // Arrange
        var cairoZone = ResolveCairoTimeZone();
        var dstTransitionDate = FindDstSpringForwardDate(cairoZone, 2026);

        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        var doctor = new Doctor
        {
            Id = doctorId,
            FullName = "Dr. DST Doctor",
            Email = "dst@example.com",
            AccountStatus = AccountStatus.Active
        };

        var patient = new Patient
        {
            Id = patientId,
            DoctorId = doctorId,
            FullName = "Ahmed Helmy",
            DateOfBirth = new DateOnly(1980, 2, 2),
            Gender = Gender.Male
        };

        // Set clock to noon on the DST transition day
        _clock.LocalNow = dstTransitionDate.ToDateTime(new TimeOnly(12, 0, 0));
        _clock.TimeZone = cairoZone;

        // On this day in Cairo, 00:00:00 is invalid and jumps to 01:00:00 (UTC+3, which is 22:00:00 UTC previous day).
        var firstValidLocal = dstTransitionDate.ToDateTime(new TimeOnly(1, 0, 0));
        var visitAtFirstValidInstant = new MedicalVisit
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            PatientId = patientId,
            VisitDate = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(firstValidLocal, cairoZone), TimeSpan.Zero)
        };

        var visitDuringDay = new MedicalVisit
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            PatientId = patientId,
            VisitDate = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(dstTransitionDate.ToDateTime(new TimeOnly(15, 0, 0)), cairoZone), TimeSpan.Zero)
        };

        await _dbContext.Doctors.AddAsync(doctor);
        await _dbContext.Patients.AddAsync(patient);
        await _dbContext.MedicalVisits.AddRangeAsync(visitAtFirstValidInstant, visitDuringDay);
        await _dbContext.SaveChangesAsync();

        _currentUser.UserId = doctorId;
        _currentUser.Role = UserRole.Doctor;

        // Act
        var result = await _service.GetDoctorDashboardAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Metrics.TodayVisitsCount);
    }

    private static TimeZoneInfo ResolveCairoTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
        }
    }

    private static DateOnly FindDstSpringForwardDate(TimeZoneInfo timeZone, int year)
    {
        for (var month = 3; month <= 5; month++)
        {
            for (var day = 1; day <= DateTime.DaysInMonth(year, month); day++)
            {
                var date = new DateOnly(year, month, day);
                var midnight = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
                if (timeZone.IsInvalidTime(midnight))
                {
                    return date;
                }
            }
        }

        return new DateOnly(year, 4, 24);
    }
}
