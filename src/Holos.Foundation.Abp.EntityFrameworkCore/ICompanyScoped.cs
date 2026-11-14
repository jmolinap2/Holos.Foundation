namespace Holos.Foundation.Abp.EntityFrameworkCore;

/// <summary>Entidad que siempre pertenece a una compañía.</summary>
public interface IMustHaveCompany
{
    long CompanyId { get; }
}

/// <summary>Entidad que puede pertenecer a una compañía o ser común a todas (<c>CompanyId</c> nulo).</summary>
public interface IMayHaveCompany
{
    long? CompanyId { get; }
}

/// <summary>
/// Lo que el <c>DbContext</c> expone para el filtro: si está activo y cuál es la compañía actual.
/// Debe ser propiedad de la instancia (no estática) para que EF la evalúe en cada consulta.
/// </summary>
public interface ICompanyFilterContext
{
    bool IsCompanyFilterEnabled { get; }

    long? CurrentCompanyId { get; }
}
