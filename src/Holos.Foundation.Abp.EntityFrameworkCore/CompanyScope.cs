using System.Linq.Expressions;
using Holos.Foundation.Abp.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Holos.Foundation.Abp.EntityFrameworkCore;

/// <summary>
/// Compañía activa implícita: el mismo mecanismo que ABP aplica al inquilino, para la compañía.
/// El <c>DbContext</c> delega aquí tres puntos (¿filtrar?, expresión del filtro, inserción).
/// </summary>
public static class CompanyScope
{
    /// <summary>Nombre del filtro de datos de ABP (<c>RegisterFilter</c> / <c>DisableFilter</c>).</summary>
    public const string FilterName = "CompanyScope";

    private const string CompanyIdProperty = nameof(IMustHaveCompany.CompanyId);

    /// <summary>¿La entidad lleva compañía (obligatoria u opcional)?</summary>
    public static bool Applies(IReadOnlyTypeBase entityType) => Applies(entityType.ClrType);

    public static bool Applies(Type clrType) =>
        typeof(IMustHaveCompany).IsAssignableFrom(clrType) || typeof(IMayHaveCompany).IsAssignableFrom(clrType);

    /// <summary>
    /// Filtro de la compañía activa, combinado con <paramref name="existing"/> (por ejemplo el de inquilino y borrado lógico de ABP).
    /// Sin compañía activa y con el filtro activo no se devuelve nada (cierra por defecto).
    /// </summary>
    public static Expression<Func<TEntity, bool>>? Combine<TEntity>(
        ICompanyFilterContext context, Expression<Func<TEntity, bool>>? existing)
        where TEntity : class
    {
        if (!Applies(typeof(TEntity)))
            return existing;

        var company = CreateFilter<TEntity>(context);
        if (existing is null)
            return company;

        var parameter = existing.Parameters[0];
        var companyBody = new ParameterReplacer(company.Parameters[0], parameter).Visit(company.Body);
        return Expression.Lambda<Func<TEntity, bool>>(Expression.AndAlso(existing.Body, companyBody), parameter);
    }

    /// <summary>Solo el filtro de compañía, para usar directamente con <c>HasQueryFilter</c>.</summary>
    public static Expression<Func<TEntity, bool>> CreateFilter<TEntity>(ICompanyFilterContext context)
        where TEntity : class
    {
        // Se accede por la interfaz: así sirve también la implementación explícita.
        var contextConstant = Expression.Convert(Expression.Constant(context, context.GetType()), typeof(ICompanyFilterContext));
        var enabled = Expression.Property(contextConstant, nameof(ICompanyFilterContext.IsCompanyFilterEnabled));
        var current = Expression.Property(contextConstant, nameof(ICompanyFilterContext.CurrentCompanyId));

        var entity = Expression.Parameter(typeof(TEntity), "e");
        var columnType = typeof(IMustHaveCompany).IsAssignableFrom(typeof(TEntity)) ? typeof(long) : typeof(long?);
        var column = Expression.Call(
            typeof(EF), nameof(EF.Property), new[] { columnType }, entity, Expression.Constant(CompanyIdProperty));

        // CurrentCompanyId es long?; una columna long se compara como long?.
        Expression left = columnType == typeof(long) ? Expression.Convert(column, typeof(long?)) : column;
        var matches = Expression.Equal(left, current);
        var body = Expression.OrElse(Expression.Not(enabled), matches);
        return Expression.Lambda<Func<TEntity, bool>>(body, entity);
    }

    /// <summary>
    /// Al insertar: completa la compañía con la activa. Con el filtro activo, una compañía distinta a la activa se rechaza.
    /// </summary>
    public static void ApplyOnAdded(ICompanyFilterContext context, EntityEntry entry)
    {
        var entity = entry.Entity;
        if (entity is not IMustHaveCompany && entity is not IMayHaveCompany)
            return;

        var property = entry.Property(CompanyIdProperty);
        var current = context.CurrentCompanyId;
        var assigned = property.CurrentValue switch { long value when value != 0 => value, _ => (long?)null };

        if (assigned is null)
        {
            if (current is not null)
                property.CurrentValue = entity is IMustHaveCompany ? current.Value : current;
            return;
        }

        if (context.IsCompanyFilterEnabled && current is not null && assigned != current)
        {
            throw new InvalidOperationException(
                $"{entity.GetType().Name}.CompanyId={assigned} no coincide con la compañía activa ({current}).");
        }
    }

    /// <summary>
    /// Al modificar: la compañía de una fila no se cambia. Con el filtro activo, mover una fila a otra compañía
    /// (por ejemplo por un DTO que trae otro <c>CompanyId</c>) se rechaza.
    /// </summary>
    public static void ApplyOnModified(ICompanyFilterContext context, EntityEntry entry)
    {
        if (!context.IsCompanyFilterEnabled || (entry.Entity is not IMustHaveCompany && entry.Entity is not IMayHaveCompany))
            return;

        var property = entry.Property(CompanyIdProperty);
        if (property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
        {
            throw new InvalidOperationException(
                $"{entry.Entity.GetType().Name}.CompanyId no puede cambiarse ({property.OriginalValue} → {property.CurrentValue}).");
        }
    }

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
