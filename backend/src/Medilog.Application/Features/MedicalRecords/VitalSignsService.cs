using AutoMapper;
using FluentValidation;
using Medilog.Application.Common;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Paging;
using Medilog.Application.Common.Validation;
using Medilog.Application.Features.MedicalRecords.Contracts;
using Medilog.Application.Features.MedicalRecords.Responses;
using Medilog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Medilog.Application.Features.MedicalRecords;

public sealed class VitalSignsService(
    IApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IMapper mapper,
    IValidator<SaveVitalSignsRequest> saveValidator,
    IValidator<ListVitalSignsQuery> listValidator) : IVitalSignsService
{
    public async Task<VisitVitalSignsResponse> CreateAsync(
        Guid visitId, SaveVitalSignsRequest request, CancellationToken cancellationToken = default)
    {
        var doctorId = RequireActingDoctorId();
        await RequestValidation.ValidateAsync(saveValidator, request, cancellationToken);
        await RequireOwnedVisitAsync(visitId, doctorId, cancellationToken);

        var vitalSigns = mapper.Map<VitalSigns>(request);
        vitalSigns.VisitId = visitId;
        dbContext.VitalSigns.Add(vitalSigns);
        await dbContext.SaveChangesAsync(cancellationToken);
        return mapper.Map<VisitVitalSignsResponse>(vitalSigns);
    }

    public async Task<PagedResult<VisitVitalSignsResponse>> ListAsync(
        Guid visitId, ListVitalSignsQuery query, CancellationToken cancellationToken = default)
    {
        var doctorId = RequireActingDoctorId();
        await RequestValidation.ValidateAsync(listValidator, query, cancellationToken);
        await RequireOwnedVisitAsync(visitId, doctorId, cancellationToken);

        var scoped = dbContext.VitalSigns.AsNoTracking()
            .Where(value => value.VisitId == visitId && value.Visit.DoctorId == doctorId);
        var totalCount = await scoped.CountAsync(cancellationToken);
        // Long arithmetic avoids overflow for a valid but very large page number.
        var offset = ((long)query.PageNumber - 1) * query.PageSize;
        if (offset >= totalCount)
        {
            return new PagedResult<VisitVitalSignsResponse>([], query.PageNumber, query.PageSize, totalCount);
        }

        var values = await scoped.OrderBy(value => value.Id)
            .Skip((int)offset).Take(query.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<VisitVitalSignsResponse>(
            mapper.Map<List<VisitVitalSignsResponse>>(values), query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<VisitVitalSignsResponse> UpdateAsync(
        Guid visitId, Guid vitalSignsId, SaveVitalSignsRequest request,
        CancellationToken cancellationToken = default)
    {
        var doctorId = RequireActingDoctorId();
        await RequestValidation.ValidateAsync(saveValidator, request, cancellationToken);
        var vitalSigns = await dbContext.VitalSigns.FirstOrDefaultAsync(
            value => value.Id == vitalSignsId && value.VisitId == visitId && value.Visit.DoctorId == doctorId,
            cancellationToken) ?? throw new NotFoundException("The vital signs were not found.");

        // Map all seven measurements, including nulls, without changing identity or ownership.
        mapper.Map(request, vitalSigns);
        await dbContext.SaveChangesAsync(cancellationToken);
        return mapper.Map<VisitVitalSignsResponse>(vitalSigns);
    }

    private async Task RequireOwnedVisitAsync(Guid visitId, Guid doctorId, CancellationToken cancellationToken)
    {
        if (!await dbContext.MedicalVisits.AnyAsync(
                visit => visit.Id == visitId && visit.DoctorId == doctorId, cancellationToken))
        {
            throw new NotFoundException("The medical visit was not found.");
        }
    }

    private Guid RequireActingDoctorId()
    {
        if (currentUser.Role != UserRole.Doctor)
        {
            throw new ForbiddenException("Only an authenticated doctor may access medical records.");
        }

        return currentUser.UserId is { } doctorId && doctorId != Guid.Empty
            ? doctorId
            : throw new ForbiddenException("The authenticated doctor could not be identified.");
    }
}
