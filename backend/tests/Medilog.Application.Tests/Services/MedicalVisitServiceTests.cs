using FluentValidation;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Security;
using Medilog.Application.Features.MedicalRecords.Contracts;
using Medilog.Domain.Entities;
using Medilog.Application.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Medilog.Application.Tests.Services;

/// <summary>
/// Behavioural tests for the medical-visit use cases, run against the real EF Core model on a
/// throwaway InMemory database (decision D-15).
/// </summary>
public sealed class MedicalVisitServiceTests
{
    [Fact]
    public async Task Invalid_request_is_rejected_before_any_persistence()
    {
        // Arrange
        using var fixture = new MedicalVisitServiceFixture(Guid.NewGuid());
        var request = new CreateMedicalVisitRequest(
            Guid.Empty,
            null,
            DateTimeOffset.UtcNow.AddHours(-1),
            null,
            null,
            null);

        // Act
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => fixture.Service.CreateVisitAsync(request));

        // Assert
        Assert.Contains(
            exception.Errors,
            failure => failure.PropertyName == nameof(CreateMedicalVisitRequest.PatientId));
        Assert.False(fixture.DbContext.ChangeTracker.HasChanges());
        Assert.Empty(fixture.DbContext.MedicalVisits);
    }

    [Fact]
    public async Task Create_for_a_patient_of_another_doctor_is_forbidden()
    {
        // Arrange
        var actingDoctorId = Guid.NewGuid();
        using var fixture = new MedicalVisitServiceFixture(actingDoctorId);

        var owningDoctor = TestData.Doctor();
        var patient = TestData.Patient(owningDoctor.Id);
        fixture.DbContext.AddRange(owningDoctor, patient);
        await fixture.SaveChangesAsync();

        var request = new CreateMedicalVisitRequest(
            patient.Id,
            null,
            DateTimeOffset.UtcNow.AddHours(-1),
            "Headache",
            null,
            null);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Service.CreateVisitAsync(request));
        Assert.Empty(fixture.DbContext.MedicalVisits);
    }

    [Fact]
    public async Task Create_with_an_appointment_of_another_patient_is_rejected()
    {
        // Arrange
        var doctor = TestData.Doctor();
        using var fixture = new MedicalVisitServiceFixture(doctor.Id);

        var patient = TestData.Patient(doctor.Id);
        var anotherPatient = TestData.Patient(doctor.Id);
        var appointmentOfAnotherPatient = TestData.Appointment(anotherPatient.Id, doctor.Id);
        fixture.DbContext.AddRange(doctor, patient, anotherPatient, appointmentOfAnotherPatient);
        await fixture.SaveChangesAsync();

        var request = new CreateMedicalVisitRequest(
            patient.Id,
            appointmentOfAnotherPatient.Id,
            DateTimeOffset.UtcNow.AddHours(-1),
            null,
            null,
            null);

        // Act
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => fixture.Service.CreateVisitAsync(request));

        // Assert
        Assert.Contains(
            exception.Errors,
            failure => failure.PropertyName == nameof(CreateMedicalVisitRequest.AppointmentId));
        Assert.Empty(fixture.DbContext.MedicalVisits);
    }

    [Fact]
    public async Task Create_with_an_already_linked_appointment_conflicts()
    {
        // Arrange
        var doctor = TestData.Doctor();
        using var fixture = new MedicalVisitServiceFixture(doctor.Id);

        var patient = TestData.Patient(doctor.Id);
        var appointment = TestData.Appointment(patient.Id, doctor.Id);
        var existingVisit = TestData.Visit(patient.Id, doctor.Id);
        existingVisit.AppointmentId = appointment.Id;
        fixture.DbContext.AddRange(doctor, patient, appointment, existingVisit);
        await fixture.SaveChangesAsync();

        var request = new CreateMedicalVisitRequest(
            patient.Id,
            appointment.Id,
            DateTimeOffset.UtcNow.AddHours(-1),
            null,
            null,
            null);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CreateVisitAsync(request));
        Assert.Single(fixture.DbContext.MedicalVisits);
    }

    [Fact]
    public async Task Create_links_a_valid_appointment()
    {
        // Arrange
        var doctor = TestData.Doctor();
        using var fixture = new MedicalVisitServiceFixture(doctor.Id);

        var patient = TestData.Patient(doctor.Id);
        var appointment = TestData.Appointment(patient.Id, doctor.Id);
        fixture.DbContext.AddRange(doctor, patient, appointment);
        await fixture.SaveChangesAsync();

        var request = new CreateMedicalVisitRequest(
            patient.Id,
            appointment.Id,
            DateTimeOffset.UtcNow.AddHours(-1),
            null,
            null,
            null);

        // Act
        var response = await fixture.Service.CreateVisitAsync(request);

        // Assert
        Assert.Equal(appointment.Id, response.AppointmentId);
        Assert.Equal(patient.Id, response.PatientId);
    }

    [Fact]
    public async Task Create_uses_the_authenticated_doctor_and_the_verified_patient()
    {
        // Arrange
        var doctor = TestData.Doctor();
        using var fixture = new MedicalVisitServiceFixture(doctor.Id);

        var patient = TestData.Patient(doctor.Id);
        fixture.DbContext.AddRange(doctor, patient);
        await fixture.SaveChangesAsync();

        var request = new CreateMedicalVisitRequest(
            patient.Id,
            null,
            DateTimeOffset.UtcNow.AddHours(-1),
            "Headache",
            null,
            null);

        // Act
        var response = await fixture.Service.CreateVisitAsync(request);

        // Assert
        var persisted = await fixture.DbContext.MedicalVisits.SingleAsync();
        Assert.Equal(doctor.Id, persisted.DoctorId);
        Assert.Equal(patient.Id, persisted.PatientId);
        Assert.Equal(persisted.DoctorId, response.DoctorId);
    }

    [Fact]
    public async Task Get_a_visit_of_another_doctor_is_not_found()
    {
        // Arrange
        var otherDoctorId = Guid.NewGuid();
        using var fixture = new MedicalVisitServiceFixture(Guid.NewGuid());

        var patient = TestData.Patient(otherDoctorId);
        var visit = TestData.Visit(patient.Id, otherDoctorId);
        fixture.DbContext.AddRange(patient, visit);
        await fixture.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Service.GetVisitAsync(visit.Id));
    }

    [Fact]
    public async Task Update_changes_only_the_mutable_fields()
    {
        // Arrange
        var doctor = TestData.Doctor();
        using var fixture = new MedicalVisitServiceFixture(doctor.Id);

        var patient = TestData.Patient(doctor.Id);
        var appointment = TestData.Appointment(patient.Id, doctor.Id);
        var visit = TestData.Visit(patient.Id, doctor.Id);
        visit.AppointmentId = appointment.Id;
        fixture.DbContext.AddRange(doctor, patient, appointment, visit);
        await fixture.SaveChangesAsync();

        var request = new UpdateMedicalVisitRequest(
            DateTimeOffset.UtcNow.AddHours(-2),
            "   ",
            "  cough  ",
            "   ");

        // Act
        var response = await fixture.Service.UpdateVisitAsync(visit.Id, request);

        // Assert
        Assert.Equal(patient.Id, response.PatientId);
        Assert.Equal(doctor.Id, response.DoctorId);
        Assert.Equal(appointment.Id, response.AppointmentId);
        Assert.Equal(request.VisitDate.ToUniversalTime(), response.VisitDate);
        Assert.Null(response.ChiefComplaint);
        Assert.Equal("cough", response.Symptoms);
        Assert.Null(response.ClinicalNotes);
    }

    [Fact]
    public async Task Update_a_visit_of_another_doctor_is_not_found()
    {
        // Arrange
        var otherDoctorId = Guid.NewGuid();
        using var fixture = new MedicalVisitServiceFixture(Guid.NewGuid());

        var patient = TestData.Patient(otherDoctorId);
        var visit = TestData.Visit(patient.Id, otherDoctorId);
        fixture.DbContext.AddRange(patient, visit);
        await fixture.SaveChangesAsync();

        var request = new UpdateMedicalVisitRequest(
            DateTimeOffset.UtcNow.AddHours(-2),
            null,
            null,
            null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.Service.UpdateVisitAsync(visit.Id, request));
    }

    [Fact]
    public async Task Add_diagnosis_copies_the_patient_from_the_visit()
    {
        // Arrange
        var doctor = TestData.Doctor();
        using var fixture = new MedicalVisitServiceFixture(doctor.Id);

        var patient = TestData.Patient(doctor.Id);
        var visit = TestData.Visit(patient.Id, doctor.Id);
        fixture.DbContext.AddRange(doctor, patient, visit);
        await fixture.SaveChangesAsync();

        var request = new CreateDiagnosisRequest(
            "  Influenza  ",
            "   ",
            DateOnly.FromDateTime(DateTime.UtcNow));

        // Act
        var response = await fixture.Service.AddDiagnosisAsync(visit.Id, request);

        // Assert
        var persisted = await fixture.DbContext.Diagnoses.SingleAsync();
        Assert.Equal(visit.PatientId, persisted.PatientId);
        Assert.Equal(visit.Id, persisted.VisitId);
        Assert.Equal("Influenza", persisted.DiagnosisName);
        Assert.Null(persisted.Description);
        Assert.Equal(visit.PatientId, response.PatientId);
    }

    [Fact]
    public async Task Visit_diagnoses_are_ordered_by_date_then_id()
    {
        // Arrange
        var doctor = TestData.Doctor();
        using var fixture = new MedicalVisitServiceFixture(doctor.Id);

        var patient = TestData.Patient(doctor.Id);
        var visit = TestData.Visit(patient.Id, doctor.Id);
        var later = TestData.Diagnosis(visit, "Later", new DateOnly(2026, 11, 9));
        var earlierFirst = TestData.Diagnosis(visit, "Earlier A", new DateOnly(2026, 11, 1));
        var earlierSecond = TestData.Diagnosis(visit, "Earlier B", new DateOnly(2026, 11, 1));
        fixture.DbContext.AddRange(doctor, patient, visit, later, earlierFirst, earlierSecond);
        await fixture.SaveChangesAsync();

        // Act
        var response = await fixture.Service.GetVisitAsync(visit.Id);

        // Assert
        var dates = response.Diagnoses.Select(diagnosis => diagnosis.DiagnosisDate).ToList();
        Assert.Equal(3, dates.Count);
        Assert.Equal(dates.OrderBy(date => date), dates);

        var sameDateIds = response.Diagnoses
            .Where(diagnosis => diagnosis.DiagnosisDate == new DateOnly(2026, 11, 1))
            .Select(diagnosis => diagnosis.Id)
            .ToList();
        Assert.Equal(sameDateIds.OrderBy(id => id), sameDateIds);
    }

    [Fact]
    public async Task Update_diagnosis_with_a_mismatched_visit_is_not_found()
    {
        // Arrange
        var doctor = TestData.Doctor();
        using var fixture = new MedicalVisitServiceFixture(doctor.Id);

        var patient = TestData.Patient(doctor.Id);
        var visit = TestData.Visit(patient.Id, doctor.Id);
        var otherVisit = TestData.Visit(patient.Id, doctor.Id);
        var diagnosis = TestData.Diagnosis(visit, "Influenza", new DateOnly(2026, 11, 1));
        fixture.DbContext.AddRange(doctor, patient, visit, otherVisit, diagnosis);
        await fixture.SaveChangesAsync();

        var request = new UpdateDiagnosisRequest(
            "Bronchitis",
            null,
            new DateOnly(2026, 11, 2));

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.Service.UpdateDiagnosisAsync(otherVisit.Id, diagnosis.Id, request));
    }

    [Fact]
    public async Task Secretary_role_is_refused_for_read_and_write()
    {
        // Arrange
        using var fixture = new MedicalVisitServiceFixture(Guid.NewGuid(), Roles.Secretary);

        var visit = TestData.Visit(Guid.NewGuid(), Guid.NewGuid());
        var createRequest = new CreateMedicalVisitRequest(
            Guid.NewGuid(),
            null,
            DateTimeOffset.UtcNow.AddHours(-1),
            null,
            null,
            null);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Service.GetVisitAsync(visit.Id));
        await Assert.ThrowsAsync<ForbiddenException>(
            () => fixture.Service.CreateVisitAsync(createRequest));
        Assert.Empty(fixture.DbContext.MedicalVisits);
    }
}
