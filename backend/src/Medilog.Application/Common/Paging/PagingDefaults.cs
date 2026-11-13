namespace Medilog.Application.Common.Paging;

/// <summary>
/// Shared paging limits for every list endpoint (STATE.md Q-04). A feature must
/// not define its own default or maximum page size.
/// </summary>
public static class PagingDefaults
{
    public const int MinPageNumber = 1;

    public const int DefaultPageNumber = 1;

    public const int DefaultPageSize = 10;

    public const int MaxPageSize = 50;
}
