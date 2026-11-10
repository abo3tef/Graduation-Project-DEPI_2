using AutoMapper;
using Medilog.Application.Features.MedicalRecords;
using Medilog.Application.Features.MedicalRecords.Contracts;
using Medilog.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Medilog.Application.Tests.Mapping;

public sealed class MedicalRecordsMappingProfileTests
{
    private static readonly IMapper Mapper = new MapperConfiguration(
        configuration => configuration.AddProfile<MedicalRecordsMappingProfile>(),
        NullLoggerFactory.Instance)
        .CreateMapper();

    [Fact]
    public void Configuration_is_valid()
    {
        var configuration = new MapperConfiguration(
            mapperConfiguration => mapperConfiguration.AddProfile<MedicalRecordsMappingProfile>(),
            NullLoggerFactory.Instance);

        configuration.AssertConfigurationIsValid();
    }

    [Fact]
    public void Visit_is_mapped_with_diagnoses_ordered_by_date_then_id()
    {
        var visit = new MedicalVisit
        {
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            VisitDate = DateTimeOffset.UtcNow,
        };

        var laterDiagnosis = new Diagnosis
        {
            VisitId = visit.Id,
            PatientId = visit.PatientId,
            DiagnosisName = "Later",
            DiagnosisDate = new DateOnly(2026, 11, 9),
        };

        var earlierDiagnosis = new Diagnosis
        {
            VisitId = visit.Id,
            PatientId = visit.PatientId,
            DiagnosisName = "Earlier",
            DiagnosisDate = new DateOnly(2026, 11, 1),
        };

        visit.Diagnoses.Add(laterDiagnosis);
        visit.Diagnoses.Add(earlierDiagnosis);

        var response = Mapper.Map<MedicalVisitResponse>(visit);

        Assert.Equal(visit.Id, response.Id);
        Assert.Equal(visit.PatientId, response.PatientId);
        Assert.Equal(visit.DoctorId, response.DoctorId);
        Assert.Equal(
            new[] { earlierDiagnosis.Id, laterDiagnosis.Id },
            response.Diagnoses.Select(diagnosis => diagnosis.Id));
    }

    [Fact]
    public void Create_request_cannot_set_identifiers_or_navigation_properties()
    {
        var request = new CreateMedicalVisitRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Complaint",
            "Symptoms",
            "Notes");

        var visit = Mapper.Map<MedicalVisit>(request);

        Assert.Equal(Guid.Empty, visit.DoctorId);
        Assert.Equal(Guid.Empty, visit.PatientId);
        Assert.Null(visit.Patient);
        Assert.Null(visit.Doctor);
        Assert.Null(visit.Appointment);
        Assert.Empty(visit.Diagnoses);
        Assert.Equal(request.ChiefComplaint, visit.ChiefComplaint);
    }

    [Fact]
    public void Update_request_cannot_change_patient_doctor_or_appointment()
    {
        var visit = new MedicalVisit
        {
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            AppointmentId = Guid.NewGuid(),
            VisitDate = DateTimeOffset.UtcNow.AddDays(-1),
        };

        var originalPatientId = visit.PatientId;
        var originalDoctorId = visit.DoctorId;
        var originalAppointmentId = visit.AppointmentId;

        var request = new UpdateMedicalVisitRequest(
            DateTimeOffset.UtcNow,
            "Updated complaint",
            null,
            null);

        Mapper.Map(request, visit);

        Assert.Equal(originalPatientId, visit.PatientId);
        Assert.Equal(originalDoctorId, visit.DoctorId);
        Assert.Equal(originalAppointmentId, visit.AppointmentId);
        Assert.Equal("Updated complaint", visit.ChiefComplaint);
    }
}
