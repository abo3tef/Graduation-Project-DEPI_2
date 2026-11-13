using System.Text.Json;
using Medilog.Application.Common.Paging;

namespace Medilog.Application.Tests.Paging;

/// <summary>
/// Pins the shared list contract approved in STATE.md Q-04: the wire names and
/// the <c>totalPages</c> calculation every list endpoint must return.
/// </summary>
public class PagingContractTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(1, 1, 1)]
    public void Total_pages_is_the_ceiling_of_count_over_page_size(int totalCount, int pageSize, int expected)
    {
        var result = new PagedResult<string>([], 1, pageSize, totalCount);

        Assert.Equal(expected, result.TotalPages);
    }

    [Fact]
    public void The_json_envelope_uses_the_shared_property_names()
    {
        var result = new PagedResult<string>(["a"], 2, 10, 11);

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("items", out var items));
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(2, root.GetProperty("page").GetInt32());
        Assert.Equal(10, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(11, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, root.GetProperty("totalPages").GetInt32());
        Assert.False(root.TryGetProperty("pageNumber", out _));
    }
}
