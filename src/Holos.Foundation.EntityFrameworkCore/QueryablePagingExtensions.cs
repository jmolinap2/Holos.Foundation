using System.Linq.Expressions;
using Abp.Application.Services.Dto;
using Microsoft.EntityFrameworkCore;

namespace Holos.Foundation.EntityFrameworkCore;

/// <summary>
/// Paginación de consultas ordenadas para servicios de aplicación. Reemplaza el bloque repetido
/// <c>CountAsync</c> + <c>Skip/Take</c> + <c>ToListAsync</c> + mapeo.
/// </summary>
public static class QueryablePagingExtensions
{
    /// <summary>
    /// Pagina una consulta ya ordenada y mapea solo las filas de la página. Lee sin seguimiento de cambios.
    /// Si la primera página viene incompleta, el total ya se conoce y se evita la consulta COUNT.
    /// </summary>
    public static async Task<PagedResultDto<TDto>> ToPagedResultAsync<TEntity, TDto>(
        this IOrderedQueryable<TEntity> query,
        IPagedResultRequest input,
        Func<TEntity, TDto> map,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(map);

        var items = await Page(query, input).AsNoTracking().ToListAsync(cancellationToken);
        var total = await TotalAsync(query, input, items.Count, cancellationToken);
        return new PagedResultDto<TDto>(total, items.Select(map).ToList());
    }

    /// <summary>
    /// Igual que <see cref="ToPagedResultAsync{TEntity,TDto}(IOrderedQueryable{TEntity},IPagedResultRequest,Func{TEntity,TDto},CancellationToken)"/>
    /// pero proyecta en SQL: solo se leen las columnas del DTO, no la entidad completa.
    /// </summary>
    public static async Task<PagedResultDto<TDto>> ToPagedProjectionAsync<TEntity, TDto>(
        this IOrderedQueryable<TEntity> query,
        IPagedResultRequest input,
        Expression<Func<TEntity, TDto>> projection,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(projection);

        var items = await Page(query, input).AsNoTracking().Select(projection).ToListAsync(cancellationToken);
        var total = await TotalAsync(query, input, items.Count, cancellationToken);
        return new PagedResultDto<TDto>(total, items);
    }

    private static IQueryable<TEntity> Page<TEntity>(IOrderedQueryable<TEntity> query, IPagedResultRequest input) =>
        query.Skip(Math.Max(0, input.SkipCount)).Take(Math.Max(0, input.MaxResultCount));

    private static async Task<int> TotalAsync<TEntity>(
        IOrderedQueryable<TEntity> query, IPagedResultRequest input, int returned, CancellationToken cancellationToken)
    {
        // Primera página incompleta: no hay más filas que las devueltas.
        if (input.SkipCount <= 0 && input.MaxResultCount > 0 && returned < input.MaxResultCount)
            return returned;
        return await query.CountAsync(cancellationToken);
    }
}
