using AutoMapper;
using Medilog.Application.Features.MedicalRecords.Contracts;
using Medilog.Application.Features.MedicalRecords.Validation;
using Medilog.Domain.Entities;

namespace Medilog.Application.Features.MedicalRecords;

/// <summary>
/// AutoMapper profile for the medical-records feature.
/// </summary>
/// <remarks>
/// Every map is explicit (AGENTS.md §3.3). Client input can only reach a new entity or the four
/// mutable members of an existing visit; identifiers, ownership fields, navigation properties and
/// child collections are always ignored and are set by the application service instead.
/// </remarks>
public sealed class MedicalRecordsMappingProfile : Profile
{
    public MedicalRecordsMappingProfile()
    {
        CreateMap<CreateMedicalVisitRequest, MedicalVisit>()
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.DoctorId, options => options.Ignore())
            .ForMember(destination => destination.PatientId, options => options.Ignore())
            .ForMember(destination => destination.Patient, options => options.Ignore())
            .ForMember(destination => destination.Doctor, options => options.Ignore())
            .ForMember(destination => destination.Appointment, options => options.Ignore())
            .ForMember(destination => destination.Diagnoses, options => options.Ignore())
            .ForMember(destination => destination.Medications, options => options.Ignore())
            .ForMember(destination => destination.VitalSigns, options => options.Ignore())
            .ForMember(destination => destination.MedicalNotes, options => options.Ignore())
            .ForMember(destination => destination.LaboratoryResults, options => options.Ignore())
            .ForMember(
                destination => destination.VisitDate,
                options => options.MapFrom(source => source.VisitDate.ToUniversalTime()))
            .ForMember(
                destination => destination.ChiefComplaint,
                options => options.MapFrom(source => MedicalRecordText.Normalize(source.ChiefComplaint)))
            .ForMember(
                destination => destination.Symptoms,
                options => options.MapFrom(source => MedicalRecordText.Normalize(source.Symptoms)))
            .ForMember(
                destination => destination.ClinicalNotes,
                options => options.MapFrom(source => MedicalRecordText.Normalize(source.ClinicalNotes)));

        CreateMap<UpdateMedicalVisitRequest, MedicalVisit>()
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.PatientId, options => options.Ignore())
            .ForMember(destination => destination.DoctorId, options => options.Ignore())
            .ForMember(destination => destination.AppointmentId, options => options.Ignore())
            .ForMember(destination => destination.Patient, options => options.Ignore())
            .ForMember(destination => destination.Doctor, options => options.Ignore())
            .ForMember(destination => destination.Appointment, options => options.Ignore())
            .ForMember(destination => destination.Diagnoses, options => options.Ignore())
            .ForMember(destination => destination.Medications, options => options.Ignore())
            .ForMember(destination => destination.VitalSigns, options => options.Ignore())
            .ForMember(destination => destination.MedicalNotes, options => options.Ignore())
            .ForMember(destination => destination.LaboratoryResults, options => options.Ignore())
            .ForMember(
                destination => destination.VisitDate,
                options => options.MapFrom(source => source.VisitDate.ToUniversalTime()))
            .ForMember(
                destination => destination.ChiefComplaint,
                options => options.MapFrom(source => MedicalRecordText.Normalize(source.ChiefComplaint)))
            .ForMember(
                destination => destination.Symptoms,
                options => options.MapFrom(source => MedicalRecordText.Normalize(source.Symptoms)))
            .ForMember(
                destination => destination.ClinicalNotes,
                options => options.MapFrom(source => MedicalRecordText.Normalize(source.ClinicalNotes)));

        CreateMap<MedicalVisit, MedicalVisitResponse>()
            .ForCtorParam(
                nameof(MedicalVisitResponse.Diagnoses),
                options => options.MapFrom(
                    source => source.Diagnoses
                        .OrderBy(diagnosis => diagnosis.DiagnosisDate)
                        .ThenBy(diagnosis => diagnosis.Id)));

        CreateMap<Diagnosis, DiagnosisResponse>();

        CreateMap<CreateDiagnosisRequest, Diagnosis>()
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.VisitId, options => options.Ignore())
            .ForMember(destination => destination.PatientId, options => options.Ignore())
            .ForMember(destination => destination.Visit, options => options.Ignore())
            .ForMember(destination => destination.Patient, options => options.Ignore())
            .ForMember(
                destination => destination.DiagnosisName,
                options => options.MapFrom(source => source.DiagnosisName.Trim()))
            .ForMember(
                destination => destination.Description,
                options => options.MapFrom(source => MedicalRecordText.Normalize(source.Description)));

        CreateMap<UpdateDiagnosisRequest, Diagnosis>()
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.VisitId, options => options.Ignore())
            .ForMember(destination => destination.PatientId, options => options.Ignore())
            .ForMember(destination => destination.Visit, options => options.Ignore())
            .ForMember(destination => destination.Patient, options => options.Ignore())
            .ForMember(
                destination => destination.DiagnosisName,
                options => options.MapFrom(source => source.DiagnosisName.Trim()))
            .ForMember(
                destination => destination.Description,
                options => options.MapFrom(source => MedicalRecordText.Normalize(source.Description)));
    }
}
