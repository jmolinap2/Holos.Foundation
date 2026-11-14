using Abp.Domain.Uow;

namespace Holos.Foundation.Abp.EntityFrameworkCore;

public static class UnitOfWorkCompanyFilterExtensions
{
    /// <summary>
    /// Registra el filtro de compañía en ABP, activo por defecto. Para jobs, seeders o procesos sin compañía:
    /// <c>using (unitOfWorkManager.Current.DisableFilter(CompanyScope.FilterName)) { ... }</c>.
    /// </summary>
    public static void RegisterCompanyFilter(this IUnitOfWorkDefaultOptions options, bool enabledByDefault = true) =>
        options.RegisterFilter(CompanyScope.FilterName, enabledByDefault);
}
