namespace Medilog.Application.Common.Paging;

/// <summary>
/// Shared page envelope for every list endpoint (STATE.md Q-04). The request
/// carries <c>pageNumber</c>; the response carries <c>page</c>.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
