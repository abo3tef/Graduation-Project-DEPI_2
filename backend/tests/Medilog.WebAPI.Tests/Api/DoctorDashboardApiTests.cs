using System.Net;
using System.Net.Http.Json;
using Medilog.Application.Common.Security;
using Medilog.Application.Features.Dashboard.Contracts;
using Medilog.Application.Interfaces;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Medilog.WebAPI.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Medilog.WebAPI.Tests.Api;

/// <summary>
/// End-to-end integration tests for the Doctor Dashboard endpoint (GET /api/dashboard/doctor).
/// </summary>
public sealed class DoctorDashboardApiTests
{
    private const string DashboardUrl = "/api/dashboard/doctor";

    [Fact]
    public async Task Get_dashboard_returns_200_with_metrics_and_schedule_for_authenticated_doctor()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        DateOnly today;
        using (var scope = factory.Services.CreateScope())
        {
            var clock = scope.ServiceProvider.GetRequiredService<IClinicClock>();
            today = DateOnly.FromDateTime(clock.LocalNow);
        }

        var doctor = ApiData.Doctor(doctorId);
        doctor.FullName = "Dr. Tarek Mostafa";
        doctor.Specialization = "Cardiology";

        var patient = ApiData.Patient(patientId, doctorId);
        patient.FullName = "Mohamed Salah";

        var apptToday = new Appointment
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            PatientId = patientId,
            CreatedBy = doctorId,
            AppointmentDate = today,
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(10, 30),
            Status = AppointmentStatus.Confirmed,
            Reason = "Checkup"
        };

        var apptCancelled = new Appointment
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorId,
            PatientId = patientId,
            CreatedBy = doctorId,
            AppointmentDate = today,
            StartTime = new TimeOnly(11, 0),
            EndTime = new TimeOnly(11, 30),
            Status = AppointmentStatus.Cancelled,
            Reason = "Cancelled by patient"
        };

        var visitToday = ApiData.Visit(Guid.NewGuid(), patientId, doctorId);
        visitToday.VisitDate = DateTimeOffset.UtcNow;

        var visitPast = ApiData.Visit(Guid.NewGuid(), patientId, doctorId);
        visitPast.VisitDate = DateTimeOffset.UtcNow.AddDays(-10);

        await factory.SeedAsync(db =>
        {
            db.Add(doctor);
            db.Add(patient);
            db.AddRange(apptToday, apptCancelled);
            db.AddRange(visitToday, visitPast);
        });

        using var client = factory.CreateClientFor(doctorId, Roles.Doctor);

        // Act
        var response = await client.GetAsync(DashboardUrl);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dashboard = await response.Content.ReadFromJsonAsync<DoctorDashboardResponse>();
        Assert.NotNull(dashboard);
        Assert.Equal(doctorId, dashboard!.DoctorId);
        Assert.Equal("Dr. Tarek Mostafa", dashboard.DoctorName);
        Assert.Equal("Cardiology", dashboard.Specialization);

        // Metrics
        Assert.Equal(1, dashboard.Metrics.TodayAppointmentsCount); // 1 active (cancelled excluded)
        Assert.Equal(1, dashboard.Metrics.TodayVisitsCount);
        Assert.Equal(2, dashboard.Metrics.TotalVisitsCount);
        Assert.Equal(1, dashboard.Metrics.TotalPatientsCount);
        Assert.Null(dashboard.Metrics.TotalRevenue);

        // Schedule table includes both appointments (including cancelled)
        Assert.Equal(2, dashboard.TodayAppointments.Count);
        Assert.Contains(dashboard.TodayAppointments, a => a.AppointmentId == apptToday.Id && a.Status == "Confirmed");
        Assert.Contains(dashboard.TodayAppointments, a => a.AppointmentId == apptCancelled.Id && a.Status == "Cancelled");
    }

    [Fact]
    public async Task Get_dashboard_without_authentication_returns_401()
    {
        // Arrange
        using var factory = new WebApiFactory();
        using var anonymousClient = factory.CreateClient();

        // Act
        var response = await anonymousClient.GetAsync(DashboardUrl);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_dashboard_as_secretary_returns_403_forbidden()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var secretaryId = Guid.NewGuid();
        using var secretaryClient = factory.CreateClientFor(secretaryId, Roles.Secretary);

        // Act
        var response = await secretaryClient.GetAsync(DashboardUrl);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_dashboard_when_doctor_record_not_found_returns_404()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var nonExistentDoctorId = Guid.NewGuid();
        using var doctorClient = factory.CreateClientFor(nonExistentDoctorId, Roles.Doctor);

        // Act
        var response = await doctorClient.GetAsync(DashboardUrl);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

