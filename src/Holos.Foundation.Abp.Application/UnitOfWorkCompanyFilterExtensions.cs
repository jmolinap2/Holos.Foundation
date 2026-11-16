using Abp.Domain.Uow;
using Holos.Foundation.Abp.Domain;

namespace Holos.Foundation.Abp.Application;

public static class UnitOfWorkCompanyFilterExtensions
{
    /// <summary>
    /// Apaga el filtro de compañía mientras dure el <c>using</c>. Solo para código que acota por sí mismo a
    /// compañías ya autorizadas (p. ej. reportes de varias compañías) o que no tiene compañía activa (seeders).
    /// </summary>
    public static IDisposable DisableCompanyFilter(this IActiveUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        return unitOfWork.DisableFilter(CompanyFilter.Name);
    }
}

public static class CompanyQueryExtensions
{
    /// <summary>
    /// Acota una consulta a compañías concretas. Úsala siempre junto a <see cref="UnitOfWorkCompanyFilterExtensions.DisableCompanyFilter"/>:
    /// al apagar el filtro global, este predicado explícito es lo único que impide ver filas de otras compañías.
    /// Una lista vacía no devuelve filas (falla cerrado).
    /// </summary>
    public static IQueryable<TEntity> ForCompanies<TEntity>(this IQueryable<TEntity> query, IEnumerable<long> companyIds)
        where TEntity : IMustHaveCompany
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(companyIds);
        var ids = companyIds.Distinct().ToArray();
        return query.Where(x => ids.Contains(x.CompanyId));
    }
}
