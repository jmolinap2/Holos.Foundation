using Abp.Domain.Uow;

namespace Holos.Foundation.EntityFrameworkCore;

public static class UnitOfWorkCompanyFilterExtensions
{
    /// <summary>
    /// Registra el filtro de compañía en ABP, activo por defecto. Para jobs, seeders o procesos sin compañía:
    /// <c>using (unitOfWork.DisableCompanyFilter()) { ... }</c> (paquete Holos.Foundation.Application).
    /// </summary>
    public static void RegisterCompanyFilter(this IUnitOfWorkDefaultOptions options, bool enabledByDefault = true) =>
        options.RegisterFilter(CompanyScope.FilterName, enabledByDefault);
}
