using Medilog.Application.Common.Paging;
using Medilog.Application.Features.MedicalRecords.Contracts;
using Medilog.Application.Features.MedicalRecords.Responses;

namespace Medilog.Application.Features.MedicalRecords;

public interface IVitalSignsService
{
    Task<VisitVitalSignsResponse> CreateAsync(Guid visitId, SaveVitalSignsRequest request,
        CancellationToken cancellationToken = default);
    Task<PagedResult<VisitVitalSignsResponse>> ListAsync(Guid visitId, ListVitalSignsQuery query,
        CancellationToken cancellationToken = default);
    Task<VisitVitalSignsResponse> UpdateAsync(Guid visitId, Guid vitalSignsId, SaveVitalSignsRequest request,
        CancellationToken cancellationToken = default);
}
