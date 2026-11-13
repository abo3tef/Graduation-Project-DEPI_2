namespace Medilog.Application.Common.Paging;

/// <summary>
/// Paging shape shared by every list query. Filters stay feature-specific;
/// paging does not.
/// </summary>
public interface IPagedQuery
{
    int PageNumber { get; }

    int PageSize { get; }
}
