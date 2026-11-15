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

    /// <summary>
    /// Apaga el filtro de compañía mientras dure el <c>using</c>. Solo para código que acota por sí mismo a
    /// compañías ya autorizadas (p. ej. reportes de varias compañías) o que no tiene compañía activa (seeders).
    /// </summary>
    public static IDisposable DisableCompanyFilter(this IActiveUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        return unitOfWork.DisableFilter(CompanyScope.FilterName);
    }
}
