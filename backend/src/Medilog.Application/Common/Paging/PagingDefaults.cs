namespace Medilog.Application.Common.Paging;

/// <summary>
/// Temporary paging limits for F1 list endpoints. To be replaced by the
/// team-approved shared values (STATE.md Q-04).
/// </summary>
public static class PagingDefaults
{
    public const int DefaultPageNumber = 1;

    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;
}
