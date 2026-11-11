using Medilog.Application.Features.MedicalRecords.Contracts;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;

namespace Medilog.WebAPI.Tests.Support;

/// <summary>
/// Synthetic data builders for the HTTP tests. No real patient data is used (AGENTS.md §5).
/// </summary>
internal static class ApiData
{
    public static Doctor Doctor(Guid id) => new()
    {
        Id = id,
        FullName = "Doctor Under Test",
        Email = $"{Guid.NewGuid():N}@medilog.test",
        PasswordHash = "test-only-placeholder",
        AccountStatus = AccountStatus.Active,
    };

    public static Patient Patient(Guid id, Guid? owningDoctorId) => new()
    {
        Id = id,
        DoctorId = owningDoctorId,
        FullName = "Patient Under Test",
        DateOfBirth = new DateOnly(1990, 4, 12),
        Gender = Gender.Female,
    };

    public static Appointment Appointment(Guid id, Guid patientId, Guid doctorId) => new()
    {
        Id = id,
        PatientId = patientId,
        DoctorId = doctorId,
        CreatedBy = doctorId,
        AppointmentDate = new DateOnly(2026, 11, 10),
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(9, 30),
        Status = AppointmentStatus.Scheduled,
    };

    public static MedicalVisit Visit(Guid id, Guid patientId, Guid doctorId) => new()
    {
        Id = id,
        PatientId = patientId,
        DoctorId = doctorId,
        VisitDate = DateTimeOffset.UtcNow.AddDays(-1),
        ChiefComplaint = "Original complaint",
        Symptoms = "Original symptoms",
        ClinicalNotes = "Original notes",
    };

    public static Diagnosis Diagnosis(Guid id, Guid visitId, Guid patientId, string name = "Migraine") => new()
    {
        Id = id,
        VisitId = visitId,
        PatientId = patientId,
        DiagnosisName = name,
        Description = $"{name} description",
        DiagnosisDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
    };

    public static CreateMedicalVisitRequest CreateVisit(
        Guid patientId,
        Guid? appointmentId = null) => new(
        patientId,
        appointmentId,
        DateTimeOffset.UtcNow.AddHours(-2),
        "Headache",
        "Since yesterday",
        "Rest and hydration");

    public static UpdateMedicalVisitRequest UpdateVisit() => new(
        DateTimeOffset.UtcNow.AddHours(-1),
        "Updated complaint",
        "Updated symptoms",
        "Updated notes");

    public static CreateDiagnosisRequest CreateDiagnosis() => new(
        "Migraine",
        "Throbbing unilateral pain",
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1));

    public static UpdateDiagnosisRequest UpdateDiagnosis() => new(
        "Tension headache",
        "Pressure-like pain",
        DateOnly.FromDateTime(DateTime.UtcNow));
}
