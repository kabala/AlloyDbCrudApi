namespace AlloyDbCrudApi.Application.Contracts.Common;

public class PaginationQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;

    public int Skip => (Page - 1) * PageSize;
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }

    public static PagedResult<T> Create(IReadOnlyList<T> items, int total, PaginationQuery q)
        => new()
        {
            Items = items,
            Total = total,
            Page = q.Page,
            PageSize = q.PageSize,
        };
}
