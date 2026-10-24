using Microsoft.EntityFrameworkCore;

namespace Despacha.Api.Shared.Http;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>Página solicitada por query string; los valores fuera de rango se ajustan en lugar de fallar.</summary>
public readonly record struct PageQuery(int Page, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static PageQuery From(int? page, int? pageSize) =>
        new(Math.Max(page ?? 1, 1), Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));

    public int Skip => (Page - 1) * PageSize;
}

public static class PagingExtensions
{
    public static async Task<PagedResponse<TResult>> ToPageAsync<TSource, TResult>(
        this IQueryable<TSource> ordered,
        PageQuery page,
        Func<TSource, TResult> map,
        CancellationToken cancellationToken)
    {
        var total = await ordered.CountAsync(cancellationToken);
        var items = await ordered.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<TResult>(items.Select(map).ToList(), total, page.Page, page.PageSize);
    }
}
