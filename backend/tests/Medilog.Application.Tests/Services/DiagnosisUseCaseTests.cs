using Medilog.Application.Common.Exceptions;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Medilog.Application.Tests.Services;

/// <summary>
/// Behavioural tests for the diagnosis delete use case (D-23), run against the real EF Core model
/// on a throwaway InMemory database (decision D-15).
/// </summary>
public sealed class DiagnosisUseCaseTests
{
    [Fact]
    public async Task Delete_removes_only_that_diagnosis_and_keeps_sibling_records()
    {
        // Arrange
        var doctor = TestData.Doctor();
        using var fixture = new MedicalVisitServiceFixture(doctor.Id);
        var patient = TestData.Patient(doctor.Id);
        var visit = TestData.Visit(patient.Id, doctor.Id);
        var diagnosisToDelete = TestData.Diagnosis(visit, "Migraine", new DateOnly(2026, 11, 9));
        var diagnosisToKeep = TestData.Diagnosis(visit, "Tension headache", new DateOnly(2026, 11, 10));
        var medication = TestData.Medication(visit);
        var vitalSigns = new VitalSigns { VisitId = visit.Id, HeartRate = 72 };
        fixture.DbContext.AddRange(
            doctor,
            patient,
            visit,
            diagnosisToDelete,
            diagnosisToKeep,
            medication,
            vitalSigns);
        await fixture.SaveChangesAsync();

        // Act
        await fixture.Service.DeleteDiagnosisAsync(visit.Id, diagnosisToDelete.Id);

        // Assert
        var remaining = await fixture.DbContext.Diagnoses.SingleAsync();
        Assert.Equal(diagnosisToKeep.Id, remaining.Id);

        Assert.True(await fixture.DbContext.MedicalVisits.AnyAsync(candidate => candidate.Id == visit.Id));
        Assert.True(await fixture.DbContext.Medications.AnyAsync(candidate => candidate.Id == medication.Id));
        Assert.True(await fixture.DbContext.VitalSigns.AnyAsync(candidate => candidate.Id == vitalSigns.Id));

        var response = await fixture.Service.GetVisitAsync(visit.Id);
        Assert.Equal(
            new[] { diagnosisToKeep.Id },
            response.Diagnoses.Select(diagnosis => diagnosis.Id));
    }

    [Fact]
    public async Task Delete_of_another_doctors_diagnosis_is_not_found()
    {
        // Arrange
        var owningDoctor = TestData.Doctor();
        var actingDoctorId = Guid.NewGuid();
        using var fixture = new MedicalVisitServiceFixture(actingDoctorId);
        var patient = TestData.Patient(owningDoctor.Id);
        var visit = TestData.Visit(patient.Id, owningDoctor.Id);
        var diagnosis = TestData.Diagnosis(visit, "Migraine", new DateOnly(2026, 11, 9));
        fixture.DbContext.AddRange(owningDoctor, patient, visit, diagnosis);
        await fixture.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.Service.DeleteDiagnosisAsync(visit.Id, diagnosis.Id));
        Assert.Single(fixture.DbContext.Diagnoses);
    }

    [Fact]
    public async Task Delete_of_an_unknown_diagnosis_is_not_found()
    {
        // Arrange
        var doctor = TestData.Doctor();
        using var fixture = new MedicalVisitServiceFixture(doctor.Id);
        var patient = TestData.Patient(doctor.Id);
        var visit = TestData.Visit(patient.Id, doctor.Id);
        fixture.DbContext.AddRange(doctor, patient, visit);
        await fixture.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.Service.DeleteDiagnosisAsync(visit.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task Delete_of_a_diagnosis_from_another_visit_is_not_found()
    {
        // Arrange
        var doctor = TestData.Doctor();
        using var fixture = new MedicalVisitServiceFixture(doctor.Id);
        var patient = TestData.Patient(doctor.Id);
        var firstVisit = TestData.Visit(patient.Id, doctor.Id);
        var secondVisit = TestData.Visit(patient.Id, doctor.Id);
        var diagnosisOfSecondVisit = TestData.Diagnosis(
            secondVisit,
            "Migraine",
            new DateOnly(2026, 11, 9));
        fixture.DbContext.AddRange(doctor, patient, firstVisit, secondVisit, diagnosisOfSecondVisit);
        await fixture.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.Service.DeleteDiagnosisAsync(firstVisit.Id, diagnosisOfSecondVisit.Id));

        var remaining = await fixture.DbContext.Diagnoses.SingleAsync();
        Assert.Equal(diagnosisOfSecondVisit.Id, remaining.Id);
    }
}
