using AutoMapper;
using Medilog.Application.Features.MedicalRecords.Contracts;
using Medilog.Application.Features.MedicalRecords.Responses;
using Medilog.Domain.Entities;

namespace Medilog.Application.Features.MedicalRecords;

public sealed class VitalSignsMappingProfile : Profile
{
    public VitalSignsMappingProfile()
    {
        CreateMap<VitalSigns, VisitVitalSignsResponse>();
        CreateMap<SaveVitalSignsRequest, VitalSigns>()
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.VisitId, options => options.Ignore())
            .ForMember(destination => destination.Visit, options => options.Ignore());
    }
}
