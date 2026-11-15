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
