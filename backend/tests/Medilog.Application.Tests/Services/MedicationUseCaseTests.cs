using Medilog.Application.Common.Exceptions;
using Medilog.Application.Features.MedicalRecords.Contracts;
using Medilog.Application.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Medilog.Application.Tests.Services;

/// <summary>
/// Behavioural tests for the medication-line use cases, run against the real EF Core model on a
/// throwaway InMemory database (decision D-15).
/// </summary>
public sealed class MedicationUseCaseTests
{
    [Fact]
    public async Task Add_copies_the_visit_and_patient_from_the_persisted_visit()
    {
        // Arrange
        var doctor = TestData.Doctor();
        using var fixture = new MedicalVisitServiceFixture(doctor.Id);
        var patient = TestData.Patient(doctor.Id);
        var visit = TestData.Visit(patient.Id, doctor.Id);
        fixture.DbContext.AddRange(doctor, patient, visit);
        await fixture.SaveChangesAsync();

        var request = CreateRequest();

        // Act
        var response = await fixture.Service.AddMedicationAsync(visit.Id, request);

        // Assert
        Assert.Equal(visit.Id, response.VisitId);
        Assert.Equal(patient.Id, response.PatientId);

        var persisted = await fixture.DbContext.Medications.SingleAsync();
        Assert.Equal(visit.Id, persisted.VisitId);
        Assert.Equal(patient.Id, persisted.PatientId);
        Assert.Equal(request.MedicationName, persisted.MedicationName);
        Assert.Equal(request.Dosage, persisted.Dosage);
        Assert.Equal(request.Frequency, persisted.Frequency);
        Assert.Equal(request.Duration, persisted.Duration);
        Assert.Equal(request.Instructions, persisted.Instructions);
    }

    [Fact]
    public async Task Add_trims_required_text_and_normalizes_optional_whitespace()
    {
        // Arrange
        var doctor = TestData.Doctor();
        using var fixture = new MedicalVisitServiceFixture(doctor.Id);
        var patient = TestData.Patient(doctor.Id);
        var visit = TestData.Visit(patient.Id, doctor.Id);
        fixture.DbContext.AddRange(doctor, patient, visit);
        await fixture.SaveChangesAsync();

        var request = new CreateMedicationRequest(
            "  Amoxicillin  ",
            " 500 mg ",
            " Every 8 hours ",
            "   ",
            "   ");

        // Act
        var response = await fixture.Service.AddMedicationAsync(visit.Id, request);

        // Assert
        Assert.Equal("Amoxicillin", response.MedicationName);
        Assert.Equal("500 mg", response.Dosage);
        Assert.Equal("Every 8 hours", response.Frequency);
        Assert.Null(response.Duration);
        Assert.Null(response.Instructions);
    }

    [Fact]
    public async Task Invalid_request_is_rejected_before_any_persistence()
    {
        // Arrange
        var doctor = TestData.Doctor();
        using var fixture = new MedicalVisitServiceFixture(doctor.Id);
        var patient = TestData.Patient(doctor.Id);
        var visit = TestData.Visit(patient.Id, doctor.Id);
        fixture.DbContext.AddRange(doctor, patient, visit);
        await fixture.SaveChangesAsync();

        var request = CreateRequest() with { MedicationName = string.Empty };

        // Act
        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => fixture.Service.AddMedicationAsync(visit.Id, request));

        // Assert
        Assert.Contains(nameof(CreateMedicationRequest.MedicationName), exception.Errors.Keys);
        Assert.False(fixture.DbContext.ChangeTracker.HasChanges());
        Assert.Empty(fixture.DbContext.Medications);
    }

    [Fact]
    public async Task Add_to_another_doctors_visit_is_not_found()
    {
        // Arrange
        var owningDoctor = TestData.Doctor();
        var actingDoctorId = Guid.NewGuid();
        using var fixture = new MedicalVisitServiceFixture(actingDoctorId);
        var patient = TestData.Patient(owningDoctor.Id);
        var visit = TestData.Visit(patient.Id, owningDoctor.Id);
        fixture.DbContext.AddRange(owningDoctor, patient, visit);
        await fixture.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.Service.AddMedicationAsync(visit.Id, CreateRequest()));
        Assert.Empty(fixture.DbContext.Medications);
    }

    [Fact]
    public async Task Update_changes_only_the_mutable_fields()
    {
        // Arrange
        var doctor = TestData.Doctor();
        using var fixture = new MedicalVisitServiceFixture(doctor.Id);
        var patient = TestData.Patient(doctor.Id);
        var visit = TestData.Visit(patient.Id, doctor.Id);
        var medication = TestData.Medication(visit);
        fixture.DbContext.AddRange(doctor, patient, visit, medication);
        await fixture.SaveChangesAsync();

        var request = UpdateRequest();

        // Act
        var response = await fixture.Service.UpdateMedicationAsync(visit.Id, medication.Id, request);

        // Assert
        Assert.Equal(medication.Id, response.Id);
        Assert.Equal(visit.Id, response.VisitId);
        Assert.Equal(patient.Id, response.PatientId);
        Assert.Equal(request.MedicationName, response.MedicationName);
        Assert.Equal(request.Dosage, response.Dosage);
        Assert.Equal(request.Frequency, response.Frequency);
        Assert.Equal(request.Duration, response.Duration);
        Assert.Equal(request.Instructions, response.Instructions);
    }

    [Fact]
    public async Task Update_of_another_doctors_medication_is_not_found()
    {
        // Arrange
        var owningDoctor = TestData.Doctor();
        var actingDoctorId = Guid.NewGuid();
        using var fixture = new MedicalVisitServiceFixture(actingDoctorId);
        var patient = TestData.Patient(owningDoctor.Id);
        var visit = TestData.Visit(patient.Id, owningDoctor.Id);
        var medication = TestData.Medication(visit);
        fixture.DbContext.AddRange(owningDoctor, patient, visit, medication);
        await fixture.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.Service.UpdateMedicationAsync(visit.Id, medication.Id, UpdateRequest()));
    }

    [Fact]
    public async Task Update_of_a_medication_from_another_visit_is_not_found()
    {
        // Arrange
        var doctor = TestData.Doctor();
        using var fixture = new MedicalVisitServiceFixture(doctor.Id);
        var patient = TestData.Patient(doctor.Id);
        var firstVisit = TestData.Visit(patient.Id, doctor.Id);
        var secondVisit = TestData.Visit(patient.Id, doctor.Id);
        var medicationOfSecondVisit = TestData.Medication(secondVisit);
        fixture.DbContext.AddRange(doctor, patient, firstVisit, secondVisit, medicationOfSecondVisit);
        await fixture.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.Service.UpdateMedicationAsync(
                firstVisit.Id,
                medicationOfSecondVisit.Id,
                UpdateRequest()));
    }

    [Fact]
    public async Task Delete_removes_only_that_medication_line()
    {
        // Arrange
        var doctor = TestData.Doctor();
        using var fixture = new MedicalVisitServiceFixture(doctor.Id);
        var patient = TestData.Patient(doctor.Id);
        var visit = TestData.Visit(patient.Id, doctor.Id);
        var medicationToDelete = TestData.Medication(visit, "Amoxicillin");
        var medicationToKeep = TestData.Medication(visit, "Ibuprofen");
        fixture.DbContext.AddRange(doctor, patient, visit, medicationToDelete, medicationToKeep);
        await fixture.SaveChangesAsync();

        // Act
        await fixture.Service.DeleteMedicationAsync(visit.Id, medicationToDelete.Id);

        // Assert
        var remaining = await fixture.DbContext.Medications.SingleAsync();
        Assert.Equal(medicationToKeep.Id, remaining.Id);
        Assert.True(await fixture.DbContext.MedicalVisits.AnyAsync(candidate => candidate.Id == visit.Id));
    }

    [Fact]
    public async Task Delete_of_another_doctors_medication_is_not_found()
    {
        // Arrange
        var owningDoctor = TestData.Doctor();
        var actingDoctorId = Guid.NewGuid();
        using var fixture = new MedicalVisitServiceFixture(actingDoctorId);
        var patient = TestData.Patient(owningDoctor.Id);
        var visit = TestData.Visit(patient.Id, owningDoctor.Id);
        var medication = TestData.Medication(visit);
        fixture.DbContext.AddRange(owningDoctor, patient, visit, medication);
        await fixture.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.Service.DeleteMedicationAsync(visit.Id, medication.Id));
        Assert.Single(fixture.DbContext.Medications);
    }

    [Fact]
    public async Task Delete_of_an_unknown_medication_is_not_found()
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
            () => fixture.Service.DeleteMedicationAsync(visit.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task Get_visit_returns_medications_ordered_by_name_then_id()
    {
        // Arrange
        var doctor = TestData.Doctor();
        using var fixture = new MedicalVisitServiceFixture(doctor.Id);
        var patient = TestData.Patient(doctor.Id);
        var visit = TestData.Visit(patient.Id, doctor.Id);
        var beta = TestData.Medication(visit, "Beta");
        var alphaOne = TestData.Medication(visit, "Alpha");
        var alphaTwo = TestData.Medication(visit, "Alpha");
        fixture.DbContext.AddRange(doctor, patient, visit, beta, alphaOne, alphaTwo);
        await fixture.SaveChangesAsync();

        var expected = new[] { alphaOne, alphaTwo }
            .OrderBy(medication => medication.Id)
            .Select(medication => medication.Id)
            .Append(beta.Id)
            .ToArray();

        // Act
        var response = await fixture.Service.GetVisitAsync(visit.Id);

        // Assert
        Assert.Equal(expected, response.Medications.Select(medication => medication.Id));
    }

    private static CreateMedicationRequest CreateRequest() => new(
        "Amoxicillin",
        "500 mg",
        "Every 8 hours",
        "7 days",
        "Take after food");

    private static UpdateMedicationRequest UpdateRequest() => new(
        "Azithromycin",
        "250 mg",
        "Once daily",
        "5 days",
        "Take before food");
}
