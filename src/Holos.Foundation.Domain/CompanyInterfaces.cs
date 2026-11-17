namespace Holos.Foundation.Domain;

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

/// <summary>Una excepción de negocio que se expresa con una clave de localización y no con un texto fijo.</summary>
public interface ILocalizableException
{
    string LocalizationKey { get; }
    object[] LocalizationArguments { get; }
}
