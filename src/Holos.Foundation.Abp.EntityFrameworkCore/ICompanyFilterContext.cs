namespace Holos.Foundation.Abp.EntityFrameworkCore;

/// <summary>
/// Lo que el <c>DbContext</c> expone para el filtro: si está activo y cuál es la compañía actual.
/// Debe ser propiedad de la instancia (no estática) para que EF la evalúe en cada consulta.
/// </summary>
public interface ICompanyFilterContext
{
    bool IsCompanyFilterEnabled { get; }

    long? CurrentCompanyId { get; }
}
