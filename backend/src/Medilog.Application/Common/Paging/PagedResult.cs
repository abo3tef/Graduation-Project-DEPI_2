namespace Medilog.Application.Common.Paging;

/// <summary>
/// Lightweight page of results. Temporary F1 contract: it is intentionally small
/// so it can be aligned with the shared pagination contract (STATE.md Q-04)
/// once that is decided.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
