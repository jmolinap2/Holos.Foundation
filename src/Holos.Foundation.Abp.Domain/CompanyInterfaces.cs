namespace Holos.Foundation.Abp.Domain;

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
/// Entrada (DTO) que pide una compañía concreta. Si pide una distinta de la activa se rechaza.
/// Se implementa de forma explícita: <c>long? IRequestsCompany.RequestedCompanyId =&gt; CompanyId;</c>
/// </summary>
public interface IRequestsCompany
{
    long? RequestedCompanyId { get; }
}

/// <summary>Entidad o entrada con un código de negocio (único dentro de la compañía).</summary>
public interface IHasCode
{
    string Code { get; }
}
