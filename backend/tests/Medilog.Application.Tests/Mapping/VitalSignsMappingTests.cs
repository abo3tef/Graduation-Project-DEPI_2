using AutoMapper;
using Medilog.Application.Features.MedicalRecords;
using Medilog.Application.Features.MedicalRecords.Contracts;
using Medilog.Application.Features.MedicalRecords.Responses;
using Medilog.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Medilog.Application.Tests.Mapping;

public sealed class VitalSignsMappingTests
{
    [Fact]
    public void Configuration_and_response_mapping_are_valid()
    {
        var configuration = new MapperConfiguration(config =>
        {
            config.AddProfile<MedicalRecordsMappingProfile>();
            config.AddProfile<VitalSignsMappingProfile>();
        }, NullLoggerFactory.Instance);
        configuration.AssertConfigurationIsValid();
        var entity = new VitalSigns
        {
            VisitId = Guid.NewGuid(), BloodPressure = "120/80", HeartRate = 72,
            Temperature = 37m, RespiratoryRate = 16, Weight = 70m, Height = 175m, SpO2 = 98,
        };

        var result = configuration.CreateMapper().Map<VisitVitalSignsResponse>(entity);

        Assert.Equal(new VisitVitalSignsResponse(entity.Id, "120/80", 72, 37m, 16, 70m, 175m, 98), result);
    }

    [Fact]
    public void Replacement_clears_nulls_and_preserves_identity_and_navigation()
    {
        var mapper = new MapperConfiguration(config => config.AddProfile<VitalSignsMappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
        var visit = new MedicalVisit();
        var entity = new VitalSigns
        {
            VisitId = visit.Id, Visit = visit, BloodPressure = "120/80", HeartRate = 72,
            Temperature = 37m, RespiratoryRate = 16, Weight = 70m, Height = 175m, SpO2 = 98,
        };
        var id = entity.Id;

        mapper.Map(new SaveVitalSignsRequest(null, 80, null, null, null, null, null), entity);

        Assert.Equal(id, entity.Id);
        Assert.Equal(visit.Id, entity.VisitId);
        Assert.Same(visit, entity.Visit);
        Assert.Null(entity.BloodPressure);
        Assert.Equal(80, entity.HeartRate);
        Assert.Null(entity.Temperature);
        Assert.Null(entity.RespiratoryRate);
        Assert.Null(entity.Weight);
        Assert.Null(entity.Height);
        Assert.Null(entity.SpO2);
        Assert.DoesNotContain(typeof(SaveVitalSignsRequest).GetProperties(),
            property => property.Name is "Id" or "VisitId" or "PatientId" or "DoctorId" or "Visit");
    }
}
