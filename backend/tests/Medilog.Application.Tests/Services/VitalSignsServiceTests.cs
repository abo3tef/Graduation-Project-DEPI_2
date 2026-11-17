using AutoMapper;
using Medilog.Application.Common;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Features.MedicalRecords;
using Medilog.Application.Features.MedicalRecords.Contracts;
using Medilog.Application.Features.MedicalRecords.Validation;
using Medilog.Application.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace Medilog.Application.Tests.Services;

public sealed class VitalSignsServiceTests
{
    [Theory]
    [InlineData(UserRole.Secretary, true)]
    [InlineData(UserRole.Doctor, false)]
    [InlineData(null, true)]
    public async Task Direct_service_calls_cannot_bypass_role_or_missing_identity(UserRole? role, bool hasId)
    {
        using var fixture = new MedicalVisitServiceFixture(hasId ? Guid.NewGuid() : null, role);
        var mapper = new MapperConfiguration(config => config.AddProfile<VitalSignsMappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
        var service = new VitalSignsService(fixture.DbContext, fixture.CurrentUser, mapper,
            new SaveVitalSignsRequestValidator(), new ListVitalSignsQueryValidator());
        var request = new SaveVitalSignsRequest(null, 72, null, null, null, null, null);
        var visitId = Guid.NewGuid();

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(visitId, request));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ListAsync(visitId, new()));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateAsync(visitId, Guid.NewGuid(), request));
        Assert.Empty(fixture.DbContext.VitalSigns);
    }

    [Fact]
    public async Task Direct_service_calls_validate_before_persistence()
    {
        using var fixture = new MedicalVisitServiceFixture(Guid.NewGuid());
        var mapper = new MapperConfiguration(config => config.AddProfile<VitalSignsMappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
        var service = new VitalSignsService(fixture.DbContext, fixture.CurrentUser, mapper,
            new SaveVitalSignsRequestValidator(), new ListVitalSignsQueryValidator());
        var invalid = new SaveVitalSignsRequest(null, 301, null, null, null, null, null);

        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateAsync(Guid.NewGuid(), invalid));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), invalid));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.ListAsync(Guid.NewGuid(), new(0, 10)));
        Assert.Empty(fixture.DbContext.VitalSigns);
    }
}
