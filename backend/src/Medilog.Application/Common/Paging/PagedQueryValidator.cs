using FluentValidation;

namespace Medilog.Application.Common.Paging;

/// <summary>
/// Shared paging rules for list queries. Feature validators derive from this
/// type and add only their own filter rules.
/// </summary>
public abstract class PagedQueryValidator<T> : AbstractValidator<T>
    where T : IPagedQuery
{
    protected PagedQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(PagingDefaults.MinPageNumber);

        RuleFor(query => query.PageSize).InclusiveBetween(1, PagingDefaults.MaxPageSize);
    }
}
